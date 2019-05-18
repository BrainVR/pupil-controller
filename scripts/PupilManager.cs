﻿using System.Collections.Generic;
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
        void OnApplicationQuit()
        {
            Disconnect();
        }
        #endregion
        #region Public API1
        public void Setup()
        {
            if (_controller == null) _controller = new PupilController(Settings, ConnectionSettings);
        }
        public void Connect()
        {
            Setup();
            StartCoroutine(PupilController.Connect(retry: true, retryDelay: 5f));
        }
        public void Disconnect()
        {
            if (IsMonitoring) StopMonitoring();
            if (IsConnected) PupilController.Disconnect();
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
        PupilManager _manager;

        void OnEnable()
        {
            _manager = (PupilManager)target;
        }
        public override void OnInspectorGUI()
        {
            //////////////////////////////////////STATUS FIELD//////////////////////////////////////
            if (_manager.IsConnected)
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
            _manager.Settings = EditorGUILayout.ObjectField(_manager.Settings, typeof(PupilSettings), false) as PupilSettings;
            _manager.ConnectionSettings = EditorGUILayout.ObjectField(_manager.ConnectionSettings, typeof(PupilConnectionSettings), false) as PupilConnectionSettings;
            var text = _manager.IsMonitoring ? "Stop debug monitoring" : "Start debug monitoring";
            if (GUILayout.Button(text))
            {
                if (_manager.IsMonitoring) _manager.StopMonitoring();
                else _manager.StartMonitoring();
                _manager.Settings.debug.printMessage = _manager.IsMonitoring;
            }
            if (GUILayout.Button("Connect")) _manager.Connect();
            if (GUILayout.Button("Connect")) _manager.Disconnect();
        }
    }

#endif
}
