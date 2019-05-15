using System.Collections;
using System.Collections.Generic;
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
        private PupilController _controller;
        public bool IsConnected { get { return PupilController.IsConnected; }}

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
            PupilController.SubscribeTo("gaze");
        }

        #endregion

    }



}
