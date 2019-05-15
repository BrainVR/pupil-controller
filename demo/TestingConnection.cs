using UnityEngine;
using BrainVR.Eyetracking.PupilLabs;

public class TestingConnection : MonoBehaviour
{
    private PupilManager manager;

    private bool _monitoring;
    // Start is called before the first frame update
    void Start()
    {
        manager = PupilManager.Instance;
        manager.Connect();
    }

    // Update is called once per frame
    void Update()
    {
        //if (manager.IsConnected) Debug.Log(PupilData.gazePoint);
        if (Input.GetKeyDown(KeyCode.H) & manager.IsConnected)
        {
            manager.StartMonitoring();
            Debug.Log("Starting monitoring");
            _monitoring = !_monitoring;
        }
        if(_monitoring) Debug.Log(PupilData.gazePoint.Raw);
    }
}
