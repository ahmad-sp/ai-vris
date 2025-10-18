using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class VoiceUploader : MonoBehaviour
{
    public string apiUrl = "http://192.168.133.1:8000/api/audio-to-text/";  // Django endpoint

    // Example: call this after recording is saved
    public void UploadRecordedFile(string filePath)
    {
        StartCoroutine(UploadAudio(filePath));
    }

    IEnumerator UploadAudio(string filePath)
    {
        byte[] audioData = System.IO.File.ReadAllBytes(filePath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", audioData, "candidate_audio.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                Debug.LogError("Upload failed: " + www.error);
            else
            {
                Debug.Log("✅ Transcribed Text: " + www.downloadHandler.text);
                // TODO: Parse the transcript and send it to interview step API
            }
        }
    }
}
