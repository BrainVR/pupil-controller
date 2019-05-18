using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using BrainVR.Eyetracking.PupilLabs;
using UnityEngine;
using UnityEngine.Profiling;

#if !UNITY_WSA
namespace FFmpegOut
{
	[RequireComponent(typeof(Camera))]
	[AddComponentMenu("FFmpegOut/Camera Capture")]
	public class CameraCapture : MonoBehaviour
	{
		#region Editable properties

		[SerializeField] bool _setResolution = true;
		[SerializeField] int _frameRate = 30;
		[SerializeField] FFmpegPipe.Codec _codec;
		[SerializeField] public float _recordLength = 5;

		#endregion

		#region Private members

		[SerializeField] public Shader _shader;

		enum RecorderState {RECORDING,PROCESSING,STOPPING, IDLE}
		RecorderState _recorderState = RecorderState.RECORDING;

		Material _material;

		FFmpegPipe _pipe;
		float _elapsed;

		RenderTexture _tempTarget;
		GameObject _tempBlitter;

		int renderedFrameCount = 0;
		int writtenFrameCount = 0;

		List<byte> timeStampList = new List<byte>();
//		StringBuilder strBuilder;

		public Camera RecordedMainCamera;

//		bool renderImage = true;

		#endregion

		public Shader sh;

		#region MonoBehavior functions
		void OnValidate()
		{
			_recordLength = Mathf.Max(_recordLength, 0.01f);
		}
		void OnEnable()
		{
			if (!FFmpegConfig.CheckAvailable)
			{
				Debug.LogError(
					"ffmpeg.exe is missing. " +
					"Please refer to the installation instruction. " +
					"https://github.com/keijiro/FFmpegOut"
				);
				enabled = false;
			}
		}
		void OnDestroy()
		{
			if (_pipe != null) ClosePipe ();
		}
		void Start()
		{
			_material = new Material (Shader.Find ("Hidden/FFmpegOut/CameraCapture"));
			PupilController.StartRecording();
		}
        void Update()
		{
			_elapsed += Time.deltaTime;

			if (_elapsed < PupilManager.Instance.Settings.recorder.recordingLength)
			{
				if (_pipe == null) OpenPipe();
			}
			else
			{
				if (_pipe != null && PupilManager.Instance.Settings.recorder.isFixedRecordingLength && _recorderState == RecorderState.RECORDING) Stop ();
			}
			if (_recorderState == RecorderState.STOPPING) GameObject.Destroy (this.gameObject);
		}

		public void Stop()
		{
			PupilRecorder.isRecording = false;
			PupilController.StopRecording ();
			_recorderState = RecorderState.PROCESSING;
		}

		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if (_pipe != null && _recorderState == RecorderState.RECORDING)
			{
				var tempRT = RenderTexture.GetTemporary(source.width, source.height);

				if (_material != null) Graphics.Blit (source, tempRT, _material, 0);
				else Debug.LogWarning ("Material for the recorder is null, fix this!");
				
				var tempTex = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
				tempTex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
				tempTex.Apply();

				// With the winter 2017 release of this plugin, Pupil timestamp is set to Unity time when connecting
				timeStampList.AddRange ( System.BitConverter.GetBytes(Time.realtimeSinceStartup));
				_pipe.Write(tempTex.GetRawTextureData());

				Destroy(tempTex);
				RenderTexture.ReleaseTemporary(tempRT);
			}
			Graphics.Blit(source, destination);
		}
		#endregion
		#region Private methods
		void OpenPipe()
		{
			if (_pipe != null) return;

			timeStampList = new List<byte> ();

			var camera = GetComponent<Camera>();
			var width = PupilManager.Instance.Settings.recorder.resolutions [(int)PupilManager.Instance.Settings.recorder.resolution] [0];
			var height = PupilManager.Instance.Settings.recorder.resolutions [(int)PupilManager.Instance.Settings.recorder.resolution] [1];
			// Apply the screen resolution settings.
			if (_setResolution)
			{
				_tempTarget = RenderTexture.GetTemporary(width, height);
				camera.targetTexture = _tempTarget;
				_tempBlitter = Blitter.CreateGameObject(camera);
			}
			else
			{
				width = camera.pixelWidth;
				height = camera.pixelHeight;
			}

			// Open an output stream.

			var cameraName = "Unity_" + PupilManager.Instance.Settings.currentCamera.name;
			_pipe = new FFmpegPipe(cameraName, width, height, _frameRate, PupilManager.Instance.Settings.recorder.codec);

			// Change the application frame rate.
			if (Time.captureFramerate == 0) Time.captureFramerate = _frameRate;
			else if (Time.captureFramerate != _frameRate)
			{
				Debug.LogWarning(
					"Frame rate mismatch; the application frame rate has been " +
					"changed with a different value. Make sure using the same " +
					"frame rate when capturing multiple cameras."
				);
			}
			Debug.Log("Capture started (" + _pipe.Filename + ")");
		}
		
		void ClosePipe()
		{
			var camera = GetComponent<Camera>();
			// Destroy the blitter object.
			if (_tempBlitter != null)
			{
				Destroy(_tempBlitter);
				_tempBlitter = null;
			}

			// Release the temporary render target.
			if (_tempTarget != null && _tempTarget == camera.targetTexture)
			{
				camera.targetTexture = null;
				RenderTexture.ReleaseTemporary(_tempTarget);
				_tempTarget = null;
			}
			// Close the output stream.
		    if (_pipe == null) return;
		    Debug.Log ("Capture ended (" + _pipe.Filename + ").");
		    // Write pupil timestamps to a file
		    var timeStampFileName = "Unity_" + PupilManager.Instance.Settings.currentCamera.name;
		    var timeStampByteArray = timeStampList.ToArray();
		    File.WriteAllBytes(_pipe.FilePath + "/" + timeStampFileName + ".time", timeStampByteArray);

		    PupilController.SaveRecording (_pipe.FilePath);
		    _pipe.Close();
		    if (!string.IsNullOrEmpty(_pipe.Error))
		    {
		        Debug.LogWarning(
		            "ffmpeg returned with a warning or an error message. " +
		            "See the following lines for details:\n" + _pipe.Error
		        );
		    }
		    _pipe = null;
		}
		#endregion
	}
}
#endif
