using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class InterviewManager : MonoBehaviour
{
    public string apiUrl = "http://192.168.1.12:8000/api/interview/";
    public int sessionId = 29;

    // Start() is no longer auto-fetching
    void Start()
    {
        // Leave empty, or use if you want auto-start
        // StartCoroutine(GetNextQuestion());
    }

    // 🔹 Call this from the Button
    public void OnButtonClick()
    {
        Debug.Log("Button clicked → fetching question from Django API...");
        StartCoroutine(GetNextQuestion());
    }

    // Fetch the next question
    IEnumerator GetNextQuestion()
    {
        UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            Debug.Log("Received JSON: " + json);

            DjangoQuestion questionData = JsonUtility.FromJson<DjangoQuestion>(json);
            DisplayQuestion(questionData.question);
        }
        else
        {
            Debug.LogError("API request failed: " + request.error);
        }
    }

    void DisplayQuestion(string question)
    {
        Debug.Log("Question: " + question);
        TriggerMouthAnimation(question);
    }

    void TriggerMouthAnimation(string text)
    {
        Debug.Log("Animating mouth for: " + text);
        // Add your preset mouth animation code here
    }

    public void SubmitAnswer(string answer)
    {
        StartCoroutine(PostAnswer(answer));
    }

    IEnumerator PostAnswer(string answer)
    {
        AnswerData data = new AnswerData { session_id = sessionId, answer = answer };
        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Answer sent successfully: " + answer);
            StartCoroutine(GetNextQuestion());
        }
        else
        {
            Debug.LogError("Failed to send answer: " + request.error);
        }
    }
}

// Helper classes
[System.Serializable]
public class DjangoQuestion
{
    public int session_id;
    public string step;
    public string question;
    public string audio_url;
    public int remaining_sections;
    public int remaining_questions;
    public string report_url;
}

[System.Serializable]
public class AnswerData
{
    public int session_id;
    public string answer;
}
