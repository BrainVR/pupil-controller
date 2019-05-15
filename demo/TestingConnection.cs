using UnityEngine;
using BrainVR.Eyetracking.PupilLabs;

public class TestingConnection : MonoBehaviour
{
    private PupilManager manager;
    // Start is called before the first frame update
    void Start()
    {
        manager = PupilManager.Instance;
        manager.Connect();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
