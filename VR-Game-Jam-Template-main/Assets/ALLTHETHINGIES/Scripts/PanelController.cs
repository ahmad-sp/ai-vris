using UnityEngine;

public class PanelController : MonoBehaviour
{
    public GameObject panel;

    public void ClosePanel()
    {
        panel.SetActive(false);
    }

    public void RetryInterview()
    {
        // Reload your scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("InterviewRoom");
    }

    public void BackToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main menu");
    }
}
