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
        private PupilConnector _connector;

        #region MonoBehaviour

        void Awake()
        {
            _connector = new PupilConnector();
        }
        

        #endregion

        #region Public API1
        public void Connect()
        {
            StartCoroutine(PupilConnector.Connect(retry: true, retryDelay: 5f));
        }
        
        #endregion

    }



}
