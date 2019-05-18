using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BrainVR.Eyetracking.PupilLabs
{
    public class PupilStatus
    {
        public bool IsConnected = false;
    }

    public class PupilManager : Singleton<PupilManager>
    {
        public PupilStatus Status;
        public PupilSettings Settings;
        public PupilConnectionSettings ConnectionSettings;
        private PupilController _controller;

        public bool IsConnected {get { return PupilController.IsConnected; }}
        public bool IsMonitoring = false;
        #region MonoBehaviour
        void Awake()
        {
            _controller = new PupilController();
        }
        void OnApplicationQuit()
        {
            if (IsConnected) PupilController.Disconnect();
        }
        #endregion
        #region Public API1
        public void Connect()
        {
            StartCoroutine(PupilController.Connect(retry: true, retryDelay: 5f));
        }
        public void StartMonitoring()
        {
            if (!IsConnected) return;
            if (IsMonitoring) return;
            Debug.Log("Starting monitoring");
            PupilController.SubscribeTo("pupil.");
            PupilController.OnReceiveData += DataReceived;
            IsMonitoring = true;
        }
        public void StopMonitoring()
        {
            if (!IsConnected) return;
            if (!IsMonitoring) return;
            Debug.Log("Stopping monitoring");
            PupilController.UnSubscribeFrom("pupil.");
            PupilController.OnReceiveData -= DataReceived;
            IsMonitoring = false;
        }
        public float? GetTimestamp()
        {
            return _controller.GetPupilTimestamp();
        }
        #endregion
        #region private functions
        private void DataReceived(string topic, Dictionary<string, object> dictionary, byte[] thirdframe)
        {
            Debug.Log("Data received");
        }
        #endregion
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PupilManager))]
    public class CustomPupilManagerInspector : Editor
    {
        PupilManager manager;

        void OnEnable()
        {
            manager = (PupilManager)target;
        }
        public override void OnInspectorGUI()
        {
            //////////////////////////////////////STATUS FIELD//////////////////////////////////////
            if (manager.IsConnected)
            {
                GUI.color = Color.green;
                //TODO needs to make the connection settings separate
                //if (manager.isLocal) GUILayout.Label("localHost ( Connected )", manager.Settings.GUIStyles[1]);
                //else GUILayout.Label("remote " + PupilTools.Connection.IP + " ( Connected )", manager.Settings.GUIStyles[1]);
            }
            else
            {
                //TODO needs to make the connection settings separate

                //if (PupilTools.Connection.isLocal) GUILayout.Label("localHost ( Not Connected )", manager.Settings.GUIStyles[1]);
                //else GUILayout.Label("remote " + PupilTools.Connection.IP + " ( Not Connected )", manager.Settings.GUIStyles[1]);
            }
            manager.Settings = EditorGUILayout.ObjectField(manager.Settings, typeof(PupilSettings), false) as PupilSettings;
            manager.ConnectionSettings = EditorGUILayout.ObjectField(manager.ConnectionSettings, typeof(PupilConnectionSettings), false) as PupilConnectionSettings;
            var text = manager.IsMonitoring ? "Stop debug monitoring" : "Start debug monitoring";
            if (GUILayout.Button(text))
            {
                if (manager.IsMonitoring) manager.StopMonitoring();
                else manager.StartMonitoring();
                manager.Settings.debug.printMessage = manager.IsMonitoring;
            }
            if (GUILayout.Button("Connect")) manager.Connect();
        }
    }

#endif
}
