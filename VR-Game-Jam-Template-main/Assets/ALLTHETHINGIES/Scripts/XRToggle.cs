using UnityEngine;
using UnityEngine.SceneManagement;

public class XRToggle : MonoBehaviour
{
    [Header("Scene Settings")]
    public string interviewSceneName = "InterviewRoom";

    // Called when VR toggle changes in MainMenu
    public void OnVRToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt("StartInVR", isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Called by Start button
    public void OnStartButtonPressed()
    {
        SceneManager.LoadScene(interviewSceneName);
    }
}
