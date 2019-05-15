﻿using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using MessagePack;

namespace BrainVR.Eyetracking.PupilLabs
{
    [Serializable]
    public class PupilConnection
    {
        public bool IsConnected;
        public string IP = "127.0.0.1";
        public string IPHeader = ">tcp://127.0.0.1:";
        public int PORT = 50020;
        public string Subport = "59485";
        public bool IsLocal = true;
        public float ConfidenceThreshold = 0.6f;

        private Dictionary<string, SubscriberSocket> _subscriptionSocketForTopic;
        private Dictionary<string, SubscriberSocket> SubscriptionSocketForTopic => _subscriptionSocketForTopic ?? (_subscriptionSocketForTopic =
                                                                                       new Dictionary<string, SubscriberSocket>());
        public RequestSocket RequestSocket;
        private bool _contextExists = false;

        private readonly TimeSpan _timeout = new TimeSpan(0, 0, 1); //1sec
        public void InitializeRequestSocket()
        {
            IPHeader = ">tcp://" + IP + ":";

            Debug.Log("Attempting to connect to : " + IPHeader + PORT);

            if (!_contextExists)
            {
                AsyncIO.ForceDotNet.Force();
                NetMQConfig.ManualTerminationTakeOver();
                NetMQConfig.ContextCreate(true);
                _contextExists = true;
            }

            RequestSocket = new RequestSocket(IPHeader + PORT);
            RequestSocket.SendFrame("SUB_PORT");
            IsConnected = RequestSocket.TryReceiveFrameString(_timeout, out Subport);
            if (!IsConnected) return;
            CheckPupilVersion();
            SetPupilTimestamp(Time.realtimeSinceStartup);
        }

        public string PupilVersion;
        public List<int> PupilVersionNumbers;

        public void CheckPupilVersion()
        {
            RequestSocket.SendFrame("v");
            if (!RequestSocket.TryReceiveFrameString(_timeout, out PupilVersion)) return;
            if (PupilVersion == null || PupilVersion == "Unknown command.") return;
            Debug.Log(PupilVersion);
            var split = PupilVersion.Split('.');
            PupilVersionNumbers = new List<int>();
            foreach (var item in split)
            {
                if (int.TryParse(item, out int number)) PupilVersionNumbers.Add(number);
            }
            Is3DCalibrationSupported();
        }

        public bool Is3DCalibrationSupported()
        {
            if ((PupilVersionNumbers.Count > 0) & (PupilVersionNumbers[0] >= 1)) return true;

            Debug.Log("Pupil version below 1 detected. V1 is required for 3D calibration");
            PupilTools.CalibrationMode = Calibration.Mode._2D;
            return false;
        }

        public void CloseSockets()
        {
            if (RequestSocket != null) RequestSocket.Close();

            foreach (var socketKey in SubscriptionSocketForTopic.Keys)
            {
                CloseSubscriptionSocket(socketKey);
            }
            UpdateSubscriptionSockets();
            TerminateContext();
            IsConnected = false;
        }

        private MemoryStream _mStream;
        public void InitializeSubscriptionSocket(string topic)
        {
            if (SubscriptionSocketForTopic.ContainsKey(topic)) return;
            SubscriptionSocketForTopic.Add(topic, new SubscriberSocket(IPHeader + Subport));
            SubscriptionSocketForTopic[topic].Subscribe(topic);

            //André: Is this necessary??
            //			subscriptionSocketForTopic[topic].Options.SendHighWatermark = PupilSettings.numberOfMessages;// 6;

            SubscriptionSocketForTopic[topic].ReceiveReady += (s, a) =>
            {
                var m = new NetMQMessage();

                while (a.Socket.TryReceiveMultipartMessage(ref m))
                {
                    // We read all the messages from the socket, but disregard the ones after a certain point
                    //				if ( i > PupilSettings.numberOfMessages ) // 6)
                    //					continue;

                    var msgType = m[0].ConvertToString();
                    _mStream = new MemoryStream(m[1].ToByteArray());
                    byte[] thirdFrame = null;
                    if (m.FrameCount >= 3) thirdFrame = m[2].ToByteArray();

                    if (PupilManager.Instance.Settings.debug.printMessageType) Debug.Log(msgType);
                    if (PupilManager.Instance.Settings.debug.printMessage) Debug.Log(MessagePackSerializer.ToJson(m[1].ToByteArray()));
                    if (PupilTools.ReceiveDataIsSet) PupilTools.ReceiveData(msgType, MessagePackSerializer.Deserialize<Dictionary<string, object>>(_mStream), thirdFrame);
                    
                    switch (msgType)
                    {
                        case "notify.calibration.successful":
                            PupilTools.CalibrationFinished();
                            Debug.Log(msgType);
                            break;
                        case "notify.calibration.failed":
                            PupilTools.CalibrationFailed();
                            Debug.Log(msgType);
                            break;
                        case "gaze":
                        case "gaze.2d.0.":
                        case "gaze.2d.1.":
                        case "pupil.0":
                        case "pupil.1":
                        case "gaze.3d.0.":
                        case "gaze.3d.1.":
                        case "gaze.3d.01.":
                            var dictionary = MessagePackSerializer.Deserialize<Dictionary<string, object>>(_mStream);
                            var confidence = PupilTools.FloatFromDictionary(dictionary, "confidence");
                            if (PupilTools.IsCalibrating)
                            {
                                var eyeID = PupilTools.StringFromDictionary(dictionary, "id");
                                PupilTools.UpdateCalibrationConfidence(eyeID, confidence);
                            }
                            else if (msgType.StartsWith("gaze"))
                            {
                                if (confidence > ConfidenceThreshold) PupilTools.gazeDictionary = dictionary;
                            }

                            break;
                        case "frame.eye.0":
                        case "frame.eye.1":
                            break;
                        default:
                            Debug.Log("No case to handle message with message type " + msgType);
                            break;
                    }
                }
            };
        }

        public void UpdateSubscriptionSockets()
        {
            var keys = new string[SubscriptionSocketForTopic.Count];
            SubscriptionSocketForTopic.Keys.CopyTo(keys, 0);
            foreach (var t in keys)
            {
                if (SubscriptionSocketForTopic[t].HasIn) SubscriptionSocketForTopic[t].Poll();
            }
            for (var i = subscriptionSocketToBeClosed.Count - 1; i >= 0; i--)
            {
                var toBeClosed = subscriptionSocketToBeClosed[i];
                if (SubscriptionSocketForTopic.ContainsKey(toBeClosed))
                {
                    SubscriptionSocketForTopic[toBeClosed].Close();
                    SubscriptionSocketForTopic.Remove(toBeClosed);
                }
                subscriptionSocketToBeClosed.Remove(toBeClosed);
            }
        }
        private List<string> subscriptionSocketToBeClosed = new List<string>();
        public void CloseSubscriptionSocket(string topic)
        {
            if (subscriptionSocketToBeClosed == null) subscriptionSocketToBeClosed = new List<string>();
            if (!subscriptionSocketToBeClosed.Contains(topic)) subscriptionSocketToBeClosed.Add(topic);
        }

        public bool sendRequestMessage(Dictionary<string, object> data)
        {
            if (RequestSocket == null || !IsConnected) return false;
            var m = new NetMQMessage();

            m.Append("notify." + data["subject"]);
            m.Append(MessagePackSerializer.Serialize<Dictionary<string, object>>(data));

            RequestSocket.SendMultipartMessage(m);
            return receiveRequestResponse();
        }

        public bool receiveRequestResponse()
        {
            // we are currently not doing anything with this
            var m = new NetMQMessage();
            return RequestSocket.TryReceiveMultipartMessage(_timeout, ref m);
        }

        public void SetPupilTimestamp(float time)
        {
            if (RequestSocket == null) return;
            RequestSocket.SendFrame("T " + time.ToString("0.00000000"));
            receiveRequestResponse();
        }

        public string GetPupilTimestamp()
        {
            if (RequestSocket == null) return "not connected";
            RequestSocket.SendFrame("t");
            var response = new NetMQMessage();
            RequestSocket.TryReceiveMultipartMessage(_timeout, ref response);
            return response.ToString();
        }

        public void TerminateContext()
        {
            if (!_contextExists) return;
            NetMQConfig.ContextTerminate();
            _contextExists = false;
        }
    }
}