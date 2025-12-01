using UnityEngine;

public class InterviewRoomVRSetup : MonoBehaviour
{
    public MobileVRManager vrManager;

    void Start()
    {
        bool startInVR = PlayerPrefs.GetInt("StartInVR", 0) == 1;

        if (vrManager != null)
            vrManager.SetVR(startInVR);

        Debug.Log("InterviewRoom started in VR = " + startInVR);
    }
}
