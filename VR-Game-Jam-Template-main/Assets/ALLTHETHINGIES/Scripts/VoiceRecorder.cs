using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class VoiceRecorder : MonoBehaviour
{
    private AudioClip recording;
    private bool isRecording = false;

    public Button recordButton; // assign in inspector
    public Text buttonText;     // optional UI text

    private string filePath;

    void Start()
    {
        // ✅ Define save path properly
        filePath = System.IO.Path.Combine(Application.persistentDataPath, "candidate_audio.wav");
        Debug.Log("Audio will be saved to: " + filePath);

        if (recordButton != null)
            recordButton.onClick.AddListener(ToggleRecording);
    }

    public void ToggleRecording()
    {
        if (!isRecording)
            StartRecording();
        else
            StopRecording();
    }

    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected!");
            return;
        }

        Debug.Log("🎙️ Recording started...");
        recording = Microphone.Start(null, false, 10, 44100);
        isRecording = true;

        if (buttonText != null)
            buttonText.text = "Stop Recording";
    }

    public void StopRecording()
    {
        if (recording == null) return;

        Debug.Log("🛑 Recording stopped...");
        Microphone.End(null);
        isRecording = false;

        // ✅ Save the file to full path
        SavWav.Save(filePath, recording);

            Debug.Log("✅ Saved recording at: " + filePath);

        if (buttonText != null)
            buttonText.text = "Start Recording";
    }

    public string GetFilePath()
    {
        return filePath;
    }
}
