using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    public Text questionText;
    public Text feedbackText;
    public GameObject transcriptPanel;

    private void Start()
    {
        InitializeHUD();
    }

    private void InitializeHUD()
    {
        questionText.text = "Welcome to the interview!";
        feedbackText.text = "";
        transcriptPanel.SetActive(false);
    }

    public void UpdateQuestion(string question)
    {
        questionText.text = question;
    }

    public void UpdateFeedback(string feedback)
    {
        feedbackText.text = feedback;
    }

    public void ToggleTranscriptPanel()
    {
        transcriptPanel.SetActive(!transcriptPanel.activeSelf);
    }
}