using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BrainVR.Eyetracking.PupilLabs
{
    public class PupilRecorder
{
        public static GameObject RecorderGO;
        public static bool isRecording;
#if !UNITY_WSA
        public FFmpegOut.FFmpegPipe.Codec codec;
        public FFmpegOut.FFmpegPipe.Resolution resolution;
#endif
        public List<int[]> resolutions = new List<int[]>() {
            new[]{1920, 1080},
            new[]{1280, 720},
            new[]{640, 480}
        };
        public string filePath;
        public bool isFixedRecordingLength;
        public float recordingLength = 10f;
        public bool isCustomPath;

        public static void Start()
        {
            RecorderGO = new GameObject("RecorderCamera");
            RecorderGO.transform.parent = PupilManager.Instance.transform;
            RecorderGO.transform.localPosition = Vector3.zero;
            RecorderGO.transform.localEulerAngles = Vector3.zero;
            RecorderGO.AddComponent<FFmpegOut.CameraCapture>();
            var c = RecorderGO.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.Color;
            c.targetDisplay = 1;
            c.stereoTargetEye = StereoTargetEyeMask.None;
            c.allowHDR = false;
            c.allowMSAA = false;
            c.fieldOfView = Camera.main.fieldOfView;
            PupilTools.RepaintGUI();
        }

        public static void Stop()
        {
            RecorderGO.GetComponent<FFmpegOut.CameraCapture>().Stop();
            Object.Destroy(RecorderGO);
            PupilTools.RepaintGUI();
        }

        public string GetRecordingPath()
        {
            var date = DateTime.Now.ToString("yyyy_MM_dd");
            var path = Application.dataPath + "/" + date;
            if (isCustomPath) path = filePath + "/" + date;
            path = path.Replace("Assets/", "");
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            Debug.Log("Recording path: " + path);
            return path;
        }
    }
}
