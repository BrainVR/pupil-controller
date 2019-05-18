
using System;
using UnityEngine;

namespace BrainVR.Eyetracking.PupilLabs
{
    [Serializable]
    public class PupilCalibration
    {
        public enum Mode
        {
            _2D,
            _3D
        }

        [Serializable]
        public struct Type
        {
            public string name;
            public string pluginName;
            public string positionKey;
            public double[] ref_data;
            public float points;
            public float markerScale;
            public Vector2 centerPoint;
            public Vector2[] vectorDepthRadius;
            public int samplesPerDepth;
        }

        public Type CalibrationType2D = new Type()
        {
            name = "2d",
            pluginName = "HMD_Calibration",
            positionKey = "norm_pos",
            ref_data = new double[] {0.0, 0.0},
            points = 8,
            markerScale = 0.05f,
            centerPoint = new Vector2(0.5f, 0.5f),
            vectorDepthRadius = new Vector2[] {new Vector2(2f, 0.07f)},
            samplesPerDepth = 120
        };

        public Type CalibrationType3D = new Type()
        {
            name = "3d",
            pluginName = "HMD_Calibration_3D",
            positionKey = "mm_pos",
            ref_data = new double[] {0.0, 0.0, 0.0},
            points = 10,
            markerScale = 0.04f,
            centerPoint = new Vector2(0, -0.05f),
            vectorDepthRadius = new Vector2[] {new Vector2(1f, 0.24f)},
            samplesPerDepth = 40
        };

        public int samplesToIgnoreForEyeMovement = 10;

        public Type currentCalibrationType
        {
            get { return PupilController.CalibrationMode == Mode._2D ? CalibrationType2D : CalibrationType3D; }
        }

        public float[] rightEyeTranslation;
        public float[] leftEyeTranslation;

        private float radius;
        private double offset;

        public void UpdateCalibrationPoint()
        {
            var type = currentCalibrationType;
            currentCalibrationPointPosition = new float[] {0};
            switch (PupilController.CalibrationMode)
            {
                case Mode._3D:
                    currentCalibrationPointPosition = new[]
                        {type.centerPoint.x, type.centerPoint.y, type.vectorDepthRadius[_currentCalibrationDepth].x};
                    offset = 0.25f * Math.PI;
                    break;
                default:
                    currentCalibrationPointPosition = new[] {type.centerPoint.x, type.centerPoint.y};
                    offset = 0f;
                    break;
            }
            radius = type.vectorDepthRadius[_currentCalibrationDepth].y;
            if (_currentCalibrationPoint > 0 && _currentCalibrationPoint < type.points)
            {
                currentCalibrationPointPosition[0] +=
                    radius * (float) Math.Cos(2f * Math.PI * (float) (_currentCalibrationPoint - 1) / (type.points - 1f) + offset);
                currentCalibrationPointPosition[1] +=
                    radius * (float) Math.Sin(2f * Math.PI * (float) (_currentCalibrationPoint - 1) / (type.points - 1f) + offset);
            }
            if (PupilController.CalibrationMode == Mode._3D) currentCalibrationPointPosition[1] /= PupilManager.Instance.Settings.currentCamera.aspect;
            Marker.UpdatePosition(currentCalibrationPointPosition);
            Marker.SetScale(type.markerScale);
        }

        public PupilCalibrationMarker Marker;
        private int _currentCalibrationPoint;
        private int _previousCalibrationPoint;
        private int _currentCalibrationSamples;
        private int _currentCalibrationDepth;
        private int _previousCalibrationDepth;
        private float[] currentCalibrationPointPosition;

        public void InitializeCalibration()
        {
            Debug.Log("Initializing Calibration");
            _currentCalibrationPoint = 0;
            _currentCalibrationSamples = 0;
            _currentCalibrationDepth = 0;
            _previousCalibrationDepth = -1;
            _previousCalibrationPoint = -1;
            if (!PupilCalibrationMarker.TryToReset(Marker)) Marker = new PupilCalibrationMarker("Calibraton Marker", Color.white);
            UpdateCalibrationPoint();
            Debug.Log("Starting Calibration");
        }
        static float _lastTimeStamp = 0;
        static readonly float TimeBetweenCalibrationPoints = 0.02f; // was 0.1, 1000/60 ms wait in old version
        public void UpdateCalibration()
        {
            var t = Time.realtimeSinceStartup;
            if (!(t - _lastTimeStamp > TimeBetweenCalibrationPoints)) return;
            _lastTimeStamp = t;
            UpdateCalibrationPoint(); 

            //Adding the calibration reference data to the list that wil;l be passed on, once the required sample amount is met.
            if (_currentCalibrationSamples > samplesToIgnoreForEyeMovement)
                PupilController.AddCalibrationPointReferencePosition(currentCalibrationPointPosition, t);

            if (PupilManager.Instance.Settings.debug.printSampling)
                Debug.Log("Point: " + _currentCalibrationPoint + ", " + "Sampling at : " +
                          _currentCalibrationSamples + ". On the position : " + currentCalibrationPointPosition[0] +
                          " | " + currentCalibrationPointPosition[1]);

            _currentCalibrationSamples++; //Increment the current calibration sample. (Default sample amount per calibration point is 120)

            if (_currentCalibrationSamples < currentCalibrationType.samplesPerDepth) return;
            _currentCalibrationSamples = 0;
            _currentCalibrationDepth++;

            if (_currentCalibrationDepth < currentCalibrationType.vectorDepthRadius.Length) return;
            _currentCalibrationDepth = 0;
            _currentCalibrationPoint++;

            //Send the current relevant calibration data for the current calibration point. _CalibrationPoints returns _calibrationData as an array of a Dictionary<string,object>.
            PupilController.AddCalibrationReferenceData();

            if (_currentCalibrationPoint >= currentCalibrationType.points) PupilController.StopCalibration();
        }
    }
}