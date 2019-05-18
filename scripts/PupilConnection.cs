using System;
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
        public PupilConnectionSettings Settings;
        public bool IsConnected;
        public string PupilVersion;
        public List<int> PupilVersionNumbers;

        private Dictionary<string, SubscriberSocket> _subscriptionSocketForTopic;
        private Dictionary<string, SubscriberSocket> SubscriptionSocketForTopic => _subscriptionSocketForTopic ?? (_subscriptionSocketForTopic =
                                                                                       new Dictionary<string, SubscriberSocket>());
        public RequestSocket RequestSocket;
        private bool _contextExists;
        private MemoryStream _mStream; //used by subscription sockets

        private readonly TimeSpan _timeout = new TimeSpan(0, 0, 1); //1sec

        public PupilConnection(PupilConnectionSettings settings)
        {
            Settings = settings;
        }
        #region connection setup
        public void InitializeRequestSocket()
        {
            Settings.IPHeader = ">tcp://" + Settings.IP + ":";
            Debug.Log("Attempting to connect to : " + Settings.IPHeader + Settings.PORT);
            if (!_contextExists)
            {
                AsyncIO.ForceDotNet.Force();
                NetMQConfig.ManualTerminationTakeOver();
                NetMQConfig.ContextCreate(true);
                _contextExists = true;
            }
            RequestSocket = new RequestSocket(Settings.IPHeader + Settings.PORT);
            RequestSocket.SendFrame("SUB_PORT");
            IsConnected = RequestSocket.TryReceiveFrameString(_timeout, out Settings.Subport);
            if (!IsConnected) return;
            GetAndSavePupilVersion();
        }
        public void CloseSockets()
        {
            RequestSocket?.Close();
            foreach (var socketKey in SubscriptionSocketForTopic.Keys)
            {
                CloseSubscriptionSocket(socketKey);
            }
            UpdateSubscriptionSockets();
            TerminateContext();
            IsConnected = false;
        }
        private List<string> _subscriptionSocketToBeClosed = new List<string>();
        public void InitializeSubscriptionSocket(string topic)
        {
            if (SubscriptionSocketForTopic.ContainsKey(topic)) return;
            SubscriptionSocketForTopic.Add(topic, new SubscriberSocket(Settings.IPHeader + Settings.Subport));
            SubscriptionSocketForTopic[topic].Subscribe(topic);

            Debug.Log("initializing conneciton " + topic);
            //André: Is this necessary??
            //subscriptionSocketForTopic[topic].Options.SendHighWatermark = PupilSettings.numberOfMessages;// 6;
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
                    if (PupilController.ReceiveDataIsSet) PupilController.ReceiveData(msgType, MessagePackSerializer.Deserialize<Dictionary<string, object>>(_mStream), thirdFrame);

                    switch (msgType)
                    {
                        case "notify.calibration.successful":
                            PupilController.CalibrationFinished();
                            Debug.Log(msgType);
                            break;
                        case "notify.calibration.failed":
                            PupilController.CalibrationFailed();
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
                            var confidence = PupilDataParser.FloatFromDictionary(dictionary, "confidence");
                            if (PupilController.IsCalibrating)
                            {
                                var eyeID = PupilDataParser.StringFromDictionary(dictionary, "id");
                                PupilController.UpdateCalibrationConfidence(eyeID, confidence);
                            }
                            else if (msgType.StartsWith("gaze") & confidence > Settings.ConfidenceThreshold) PupilController.gazeDictionary = dictionary;
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
            //polling all sockets for information
            //TODO weird phrasing :/
            foreach (var t in keys)
            {
                if (SubscriptionSocketForTopic[t].HasIn) SubscriptionSocketForTopic[t].Poll();
            }
            //Closing of sockets lined up to be closed
            for (var i = _subscriptionSocketToBeClosed.Count - 1; i >= 0; i--)
            {
                var toBeClosed = _subscriptionSocketToBeClosed[i];
                if (SubscriptionSocketForTopic.ContainsKey(toBeClosed))
                {
                    SubscriptionSocketForTopic[toBeClosed].Close();
                    SubscriptionSocketForTopic.Remove(toBeClosed);
                }
                _subscriptionSocketToBeClosed.Remove(toBeClosed);
            }
        }
        /// <summary>
        /// Adds string to the list of sockets to be closed. 
        /// </summary>
        /// <param name="topic"></param>
        public void CloseSubscriptionSocket(string topic)
        {
            if (_subscriptionSocketToBeClosed == null) _subscriptionSocketToBeClosed = new List<string>();
            if (!_subscriptionSocketToBeClosed.Contains(topic)) _subscriptionSocketToBeClosed.Add(topic);
        }
        /// <summary>
        /// Sends Data of type dicrionary<string, object> and returns ReceiveRequestResponse
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SendRequestMessage(Dictionary<string, object> data)
        {
            if (RequestSocket == null || !IsConnected) return false;
            var m = new NetMQMessage();
            m.Append("notify." + data["subject"]);
            m.Append(MessagePackSerializer.Serialize(data));
            RequestSocket.SendMultipartMessage(m);
            return ReceiveRequestResponse();
        }

        public bool SendFrame(string message)
        {
            if (RequestSocket == null) return false;
            RequestSocket.SendFrame(message);
            return ReceiveRequestResponse();
        }
        public bool ReceiveRequestResponse()
        {
            // we are currently not doing anything with this
            var m = new NetMQMessage();
            return RequestSocket.TryReceiveMultipartMessage(_timeout, ref m);
        }
        public void TerminateContext()
        {
            if (!_contextExists) return;
            NetMQConfig.ContextTerminate();
            _contextExists = false;
        }
        #endregion
        #region Public Getters
        public void GetAndSavePupilVersion()
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
            //validate that version numbers have been set
            if ((PupilVersionNumbers.Count > 0) & (PupilVersionNumbers[0] >= 1)) return true;
            Debug.Log("Pupil version below 1 detected. V1 is required for 3D calibration");
            PupilController.CalibrationMode = PupilCalibration.Mode._2D;
            return false;
        }
        public float? GetPupilTimestamp()
        {
            if (RequestSocket == null) Debug.Log("not connected");
            RequestSocket.SendFrame("t");
            var response = new NetMQMessage();
            RequestSocket.TryReceiveMultipartMessage(_timeout, ref response);
            if (response.FrameCount == 1)
            {
                //TODO try parse
                return float.Parse(response.First.ConvertToString());
            }
            Debug.Log("Received complex message " + response + ". Cannot parse to time");
            return null;

        }
        #endregion

    }
}