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

        #region Public API1

        public bool Connect()
        {
            return false;
        }
        
        #endregion

    }



}
