using UnityEngine;

[CreateAssetMenu(menuName= "BrainVR/Eyetracking/Pupil/ConnectionSettings", fileName = "PupilConnectionSettings")]
public class PupilConnectionSettings : ScriptableObject
{
    public string IP = "127.0.0.1";
    public string IPHeader = ">tcp://127.0.0.1:";
    public int PORT = 50020;
    public string Subport = "59485";
    public bool IsLocal = true;
    public float ConfidenceThreshold = 0.6f;
}
