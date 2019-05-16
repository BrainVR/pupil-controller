// Original source are the examples of Pupil labs
//formerly PupilTools int he pupilLabs examples
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace BrainVR.Eyetracking.PupilLabs
{
    public class PupilController
    {
        //TODO redo to non static settings
        private static PupilSettings _settings {get {return PupilManager.Instance.Settings; }}
        private static PupilConnection _connection = new PupilConnection();

        #region Delegates
        //InspectorGUI repaint
        public delegate void GUIRepaintAction();
        public delegate void OnCalibrationStartDeleg();
        public delegate void OnCalibrationEndDeleg();
        public delegate void OnCalibrationFailedDeleg();
        public delegate void OnConnectedDelegate();
        public delegate void OnDisconnectingDelegate();
        public delegate void OnReceiveDataDelegate(string topic, Dictionary<string, object> dictionary, byte[] thirdFrame = null);

        public static event OnCalibrationStartDeleg OnCalibrationStarted;
        public static event OnCalibrationEndDeleg OnCalibrationEnded;
        public static event OnCalibrationFailedDeleg OnCalibrationFailed;
        public static event OnConnectedDelegate OnConnected;
        public static event OnDisconnectingDelegate OnDisconnecting;
        public static event OnReceiveDataDelegate OnReceiveData;
        #endregion
        #region EStatus
        private enum EStatus { Idle, ProcessingGaze, Calibration }
        private static EStatus _dataProcessState = EStatus.Idle;
        private static EStatus DataProcessState
        {
            get { return _dataProcessState; }
            set
            {
                _dataProcessState = value;
                PupilMarker.TryToSetActive(Calibration.Marker, _dataProcessState == EStatus.Calibration);
            }
        }
        private static EStatus previousState = EStatus.Idle;
        public static bool IsIdle
        {
            get { return DataProcessState == EStatus.Idle; }
            set { SetProcessState(!value, EStatus.Idle); }
        }
        public static bool IsGazing
        {
            get { return DataProcessState == EStatus.ProcessingGaze; }
            set { SetProcessState(!value, EStatus.ProcessingGaze); }
        }
        public static bool IsCalibrating
        {
            get { return DataProcessState == EStatus.Calibration; }
            set { SetProcessState(!value, EStatus.Calibration); }
        }
        private static void SetProcessState(bool toOldState, EStatus newState)
        {
            if (toOldState)
                DataProcessState = previousState;
            else
            {
                previousState = DataProcessState;
                DataProcessState = newState;
            }
        }
        #endregion

        #region Recording
        private static bool isRecording;
        public static void StartRecording()
        {
            var _p = _settings.recorder.GetRecordingPath().Substring(2);

            Send(new Dictionary<string, object> {
                { "subject","recording.should_start" }
                , { "session_name", _p }
                , { "record_eye",true}
            });

            isRecording = true;

            recordingString = "Timestamp,Identifier,PupilPositionX,PupilPositionY,PupilPositionZ,UnityWorldPositionX,UnityWorldPositionY,UnityWorldPositionZ\n";
        }
        private static string recordingString;
        public static void StopRecording()
        {
            Send(new Dictionary<string, object> { { "subject", "recording.should_stop" } });

            isRecording = false;
        }
        private static Vector3 unityWorldPosition;
        private static void AddToRecording(string identifier, Vector3 position, bool isViewportPosition = false)
        {
            var timestamp = PupilDataParser.FloatFromDictionary(gazeDictionary, "timestamp");

            unityWorldPosition = isViewportPosition ? _settings.currentCamera.ViewportToWorldPoint(position + Vector3.forward) : 
                _settings.currentCamera.cameraToWorldMatrix.MultiplyPoint3x4(position);

            if (!isViewportPosition)
                position.y *= -1;               // Pupil y axis is inverted

            recordingString += string.Format("{0},{1},{2},{3},{4},{5},{6},{7}\n"
                , timestamp.ToString("F4")
                , identifier
                , position.x.ToString("F4"), position.y.ToString("F4"), position.z.ToString("F4")
                , unityWorldPosition.x.ToString("F4"), unityWorldPosition.y.ToString("F4"), unityWorldPosition.z.ToString("F4")
            );
        }
        public static void SaveRecording(string toPath)
        {
            var filePath = toPath + "/" + "UnityGazeExport.csv";
            File.WriteAllText(filePath, recordingString);
        }
        #endregion

        #region Dictionary processing
        private static Dictionary<string, object> _gazeDictionary;
        public static Dictionary<string, object> gazeDictionary
        {
            get
            {
                return _gazeDictionary;
            }
            set
            {
                _gazeDictionary = value;
                UpdateGaze();
            }
        }

        private static string[] gazeKeys = { "gaze_point_3d", "norm_pos", "eye_centers_3d", "gaze_normals_3d" };
        private static string eyeDataKey;
        private static void UpdateGaze()
        {
            CheckModeConsistency();
            foreach (var key in gazeKeys)
            {
                if (!gazeDictionary.ContainsKey(key)) continue;
                switch (key)
                {
                    case "norm_pos": // 2D case
                        eyeDataKey = key + "_" + PupilDataParser.StringFromDictionary(gazeDictionary, "id"); // we add the identifier to the key
                        var position2D = PupilDataParser.Position(gazeDictionary[key], false);
                        PupilData.AddGazeToEyeData(eyeDataKey, position2D);
                        if (isRecording) AddToRecording(eyeDataKey, position2D, true);
                        break;
                    case "eye_centers_3d":
                    case "gaze_normals_3d":
                        // in case of eye_centers_3d and gaze_normals_3d, we get an dictionary with one positional object for each eye id (the key)
                        if (gazeDictionary[key] is Dictionary<object, object>)
                            foreach (var item in (gazeDictionary[key] as Dictionary<object, object>))
                            {
                                eyeDataKey = key + "_" + item.Key.ToString();
                                var position = PupilDataParser.Position(item.Value, true);
                                position.y *= -1f;                          // Pupil y axis is inverted
                                PupilData.AddGazeToEyeData(eyeDataKey, position);
                            }
                        break;
                    default:
                        var position3D = PupilDataParser.Position(gazeDictionary[key], true);
                        position3D.y *= -1f;                                // Pupil y axis is inverted
                        PupilData.AddGazeToEyeData(key, position3D);
                        if (isRecording) AddToRecording(key, position3D);
                        break;
                }
            }
        }

        private static void CheckModeConsistency()
        {
            var topic = PupilDataParser.StringFromDictionary(gazeDictionary, "topic");
            if (topic.StartsWith("gaze.2D") && CalibrationMode == Calibration.Mode._3D) Debug.Log("We are receiving 2D gaze information while expecting 3D data");
        }

        public static string TopicsForDictionary(Dictionary<string, object> dictionary)
        {
            var topics = "";
            foreach (string key in dictionary.Keys)
            {
                topics += key + ",";
            }
            return topics;
        }
        public static Dictionary<object, object> BaseData()
        {
            object o;
            gazeDictionary.TryGetValue("base_data", out o);
            return o as Dictionary<object, object>;
        }
        #endregion

        #region Connection
        public static bool IsConnected
        {
            get { return _connection.IsConnected; }
            set { _connection.IsConnected = value; }
        }
        public static IEnumerator Connect(bool retry = false, float retryDelay = 5f)
        {
            //Debug.Log("Waiting for 3 s to connect");
            //yield return new WaitForSeconds(3f);
            Debug.Log("Trying to connect");
            while (!IsConnected)
            {
                _connection.InitializeRequestSocket();
                if (IsConnected) continue;
                if (retry)
                {
                    Debug.Log("Could not connect, Re-trying in 5 seconds ! ");
                    yield return new WaitForSeconds(retryDelay);
                }
                else
                {
                    _connection.TerminateContext();
                    yield return null;
                }
            }
            Debug.Log(" Succesfully connected to Pupil! ");

            StartEyeProcesses();
            OnConnected?.Invoke();
        }
        public static void Disconnect()
        {
            if (OnDisconnecting != null) OnDisconnecting();
            if (IsCalibrating) StopCalibration();
            _connection.CloseSockets();
        }
        public static void SubscribeTo(string topic)
        {
            _connection.InitializeSubscriptionSocket(topic);
        }
        public static void UnSubscribeFrom(string topic)
        {
            _connection.CloseSubscriptionSocket(topic);
        }
        public static bool Send(Dictionary<string, object> dictionary)
        {
            return _connection.sendRequestMessage(dictionary);
        }
        #endregion

        #region Calibration
        public static Calibration Calibration
        {
            get { return _settings.calibration; }
        }
        private static Calibration.Mode _calibrationMode = Calibration.Mode._2D;
        public static Calibration.Mode CalibrationMode
        {
            get { return _calibrationMode; }
            set
            {
                if (IsConnected && !_connection.Is3DCalibrationSupported())
                    value = Calibration.Mode._2D;

                if (_calibrationMode == value) return;
                _calibrationMode = value;
                if (IsConnected) SetDetectionMode();
            }
        }
        public static Calibration.Type CalibrationType
        {
            get { return Calibration.currentCalibrationType; }
        }
        public static void StartCalibration()
        {
            if (IsGazing) PupilGazeTracker.Instance.StopVisualizingGaze();

            if (OnCalibrationStarted != null) OnCalibrationStarted();
            else Debug.Log("No 'calibration started' delegate set");
            Calibration.InitializeCalibration();
            IsCalibrating = true;
            SubscribeTo("notify.calibration.successful");
            SubscribeTo("notify.calibration.failed");
            SubscribeTo("pupil.");
            Send(new Dictionary<string, object> {
                { "subject","start_plugin" },
                {"name",CalibrationType.pluginName}
            });
            Send(new Dictionary<string, object> {
                { "subject","calibration.should_start" },
                {"hmd_video_frame_size",new float[] {1000,1000}},
                {"outlier_threshold", 35},
                {"translation_eye0",Calibration.rightEyeTranslation},
                {"translation_eye1",Calibration.leftEyeTranslation}
            });
            _calibrationData.Clear();
        }
        public static void StopCalibration()
        {
            IsCalibrating = false;
            Send(new Dictionary<string, object> { { "subject", "calibration.should_stop" } });
        }
        public static void CalibrationFinished()
        {
            IsIdle = true;

            Debug.Log("Calibration finished");

            UnSubscribeFrom("notify.calibration.successful");
            UnSubscribeFrom("notify.calibration.failed");
            UnSubscribeFrom("pupil.");

            if (OnCalibrationEnded != null)
                OnCalibrationEnded();
            else
            {
                Debug.Log("No 'calibration ended' delegate set");
            }
        }
        public static void CalibrationFailed()
        {
            IsIdle = true;

            if (OnCalibrationFailed != null)
                OnCalibrationFailed();
            else
            {
                Debug.Log("No 'calibration failed' delegate set");
            }
        }
        private static List<Dictionary<string, object>> _calibrationData = new List<Dictionary<string, object>>();
        public static void AddCalibrationReferenceData()
        {
            Send(new Dictionary<string, object> {
                { "subject","calibration.add_ref_data" },
                {
                    "ref_data",
                    _calibrationData.ToArray ()
                }
            });

            if (_settings.debug.printSampling)
            {
                Debug.Log("Sending ref_data");

                var str = "";

                foreach (var element in _calibrationData)
                {
                    foreach (var i in element)
                    {
                        if (i.Key == "norm_pos")
                        {
                            str += "|| " + i.Key + " | " + ((System.Single[])i.Value)[0] + " , " + ((System.Single[])i.Value)[1];
                        }
                        else
                        {
                            str += "|| " + i.Key + " | " + i.Value.ToString();
                        }
                    }
                    str += "\n";

                }
                Debug.Log(str);
            }

            //Clear the current calibration data, so we can proceed to the next point if there is any.
            _calibrationData.Clear();
        }

        public static void AddCalibrationPointReferencePosition(float[] position, float timestamp)
        {
            if (CalibrationMode == Calibration.Mode._3D)
            {
                for (var i = 0; i < position.Length; i++)
                    position[i] *= PupilSettings.PupilUnitScalingFactor;
            }
            _calibrationData.Add(new Dictionary<string, object>() {
                { CalibrationType.positionKey, position },
                { "timestamp", timestamp },
                { "id", int.Parse(PupilData.rightEyeID) }
            });
            _calibrationData.Add(new Dictionary<string, object>() {
                { CalibrationType.positionKey, position },
                { "timestamp", timestamp },
                { "id", int.Parse(PupilData.leftEyeID) }
            });
        }

        private static Dictionary<string, float> calibrationConfidenceForEye;
        public static void UpdateCalibrationConfidence(string eyeID, float confidence)
        {
            if (calibrationConfidenceForEye == null)
                calibrationConfidenceForEye = new Dictionary<string, float>();
            if (!calibrationConfidenceForEye.ContainsKey(eyeID))
                calibrationConfidenceForEye.Add(eyeID, confidence);
            else
                calibrationConfidenceForEye[eyeID] = confidence;

            UpdateCalibrationMarkerColor(eyeID, confidence);
        }

        private static void UpdateCalibrationMarkerColor(string eyeID, float value)
        {
            var currentColor = Calibration.Marker.color;
            switch (eyeID)
            {
                case PupilData.rightEyeID:
                    currentColor.g = value;
                    break;
                case PupilData.leftEyeID:
                    currentColor.b = value;
                    break;
            }
            Calibration.Marker.color = currentColor;
        }

        #endregion
        public static bool eyeProcess0;
        public static bool eyeProcess1;
        public static bool StartEyeProcesses()
        {
            var startLeftEye = new Dictionary<string, object> {
                { "subject","eye_process.should_start." + PupilData.leftEyeID },
                { "eye_id", int.Parse(PupilData.leftEyeID)  },
                { "delay", 0.1f }
            };
            var startRightEye = new Dictionary<string, object> {
                { "subject","eye_process.should_start."  + PupilData.rightEyeID },
                { "eye_id", int.Parse(PupilData.rightEyeID) },
                { "delay", 0.2f }
            };

            eyeProcess0 = false;
            eyeProcess1 = false;

            if (!SetDetectionMode()) return false;
            if (!Send(startLeftEye)) return false;
            eyeProcess1 = true;
            if (!Send(startRightEye)) return false;
            eyeProcess0 = true;
            return true;
        }

        public static bool ReceiveDataIsSet { get { return OnReceiveData != null; } }
        public static void ReceiveData(string topic, Dictionary<string, object> dictionary, byte[] thirdFrame = null)
        {
            if (OnReceiveData != null) OnReceiveData(topic, dictionary, thirdFrame);
            else Debug.Log("OnReceiveData is not set");
        }
        public static bool StopEyeProcesses()
        {
            var stopLeftEye = new Dictionary<string, object> {
                { "subject","eye_process.should_stop." + PupilData.leftEyeID },
                { "eye_id", int.Parse(PupilData.leftEyeID)  },
                { "delay", 0.1f }
            };
            var stopRightEye = new Dictionary<string, object> {
                { "subject","eye_process.should_stop." + PupilData.rightEyeID },
                { "eye_id", int.Parse(PupilData.rightEyeID) },
                { "delay", 0.2f }
            };

            if (!Send(stopLeftEye)) return false;
            eyeProcess1 = false;
            if (!Send(stopRightEye)) return false;
            eyeProcess0 = false;
            return true;
        }
        public static void StartBinocularVectorGazeMapper()
        {
            Send(new Dictionary<string, object> { { "subject", "" }, { "name", "Binocular_Vector_Gaze_Mapper" } });
        }
        public static bool SetDetectionMode()
        {
            return Send(new Dictionary<string, object> { { "subject", "set_detection_mapping_mode" }, { "mode", CalibrationType.name } });
        }
        public static bool ActivateFakeCapture()
        {
            return Send(
                new Dictionary<string, object> {
                    { "subject","start_plugin" }
                    ,{ "name","Fake_Source" }
                    ,{
                        "args", new Dictionary<string,object>
                        {
                            { "frame_size", new int[] { 1280, 720 } }
                            ,{ "frame_rate", 30 }
                        }
                    }
                });
        }
        public float? GetPupilTimestamp()
        {
            return _connection.GetPupilTimestamp();
        }
    }

}
