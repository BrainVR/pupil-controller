using UnityEngine;

namespace BrainVR.Eyetracking.PupilLabs
{

    public class PupilCalibrationMarker
    {
        public string Name;
        private Color _color = Color.white;
        public Vector3 Position;
        private Material _material;
        private GameObject _gameObject;

        public Color color
        {
            get => _color;
            set
            {
                _color = value;

                if (_material != null)
                    _material.color = _color;
            }
        }
        private GameObject gameObject
        {
            get
            {
                if (_gameObject != null) return _gameObject;
                _gameObject = GameObject.Instantiate(Resources.Load<GameObject>("MarkerObject"));
                _gameObject.name = this.Name;
                _material = new Material(Resources.Load<Material>("Materials/MarkerMaterial"));
                _gameObject.GetComponent<MeshRenderer>().material = _material;
                _gameObject.transform.parent = this.camera.transform;
                _material.color = this.color;
                return _gameObject;
            }
        }

        private Camera _camera;

        public Camera camera
        {
            get => _camera ?? (_camera = Camera.main);
            set
            {
                _camera = value;
                gameObject.transform.parent = _camera.transform;
            }
        }

        public PupilCalibrationMarker(string name, Color color)
        {
            this.Name = name;
            this.color = color;
            camera = PupilManager.Instance.Settings.currentCamera;
        }

        public void UpdatePosition(Vector2 newPosition)
        {
            Position.x = newPosition.x;
            Position.y = newPosition.y;
            Position.z = PupilTools.CalibrationType.vectorDepthRadius[0].x;
            gameObject.transform.position = camera.ViewportToWorldPoint(Position);
            UpdateOrientation();
        }

        public void UpdatePosition(Vector3 newPosition)
        {
            Position = newPosition;
            gameObject.transform.localPosition = Position;
            UpdateOrientation();
        }

        public void UpdatePosition(float[] newPosition)
        {
            if (PupilTools.CalibrationMode == Calibration.Mode._2D)
            {
                if (newPosition.Length == 2)
                {
                    Position.x = newPosition[0];
                    Position.y = newPosition[1];
                    Position.z = PupilTools.CalibrationType.vectorDepthRadius[0].x;
                    gameObject.transform.position = camera.ViewportToWorldPoint(Position);
                }
                else
                {
                    Debug.Log("Length of new position array does not match 2D mode");
                }
            }
            else if (PupilTools.CalibrationMode == Calibration.Mode._3D)
            {
                if (newPosition.Length == 3)
                {
                    Position.x = newPosition[0];
                    Position.y = newPosition[1];
                    Position.z = newPosition[2];
                    gameObject.transform.localPosition = Position;
                }
                else
                {
                    Debug.Log("Length of new position array does not match 3D mode");
                }
            }
            UpdateOrientation();
        }

        private void UpdateOrientation()
        {
            gameObject.transform.LookAt(camera.transform.position);
        }
        public static bool TryToSetActive(PupilCalibrationMarker marker, bool toggle)
        {
            if (marker == null) return false;
            if (marker.gameObject != null) marker.gameObject.SetActive(toggle);
            return true;
        }

        public void SetScale(float value)
        {
            if (gameObject.transform.localScale.x != value)
                gameObject.transform.localScale = Vector3.one * value;
        }

        public static bool TryToReset(PupilCalibrationMarker marker)
        {
            if (marker == null) return false;
            marker.camera = PupilManager.Instance.Settings.currentCamera;
            marker.gameObject.SetActive(true);
            return true;
        }
    }
}