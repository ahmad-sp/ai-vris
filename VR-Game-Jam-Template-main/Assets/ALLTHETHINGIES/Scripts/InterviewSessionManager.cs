using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using TMPro;

public class InterviewSessionManager : MonoBehaviour
{
    [Header("References")]
    public VADVoiceRecorder vad; // VAD recorder that raises onTranscript

    [Header("UI & Audio")]
    public TextMeshProUGUI questionText; // display interviewer question
    public AudioSource questionAudioSource; // play interviewer TTS audio

    [Header("Events")]
    public UnityEvent<string> onQuestionReceived; // forward question to phoneme bridge

    [Header("Backend")] 
    public string backendBaseUrl = "http://127.0.0.1:8000";
    public string interviewPath = "/api/interview/";

    private bool isPlayingTts = false;

    private void Awake()
    {
        if (vad != null)
        {
            if (vad.onTranscript == null)
                vad.onTranscript = new UnityEvent<string>();
            vad.onTranscript.AddListener(OnTranscriptReady);
        }
    }

    // Public entry to handle the very first prompt from CandidateInfoForm
    public void StartWithPrompt(string question, string audioUrl)
    {
        StartCoroutine(HandleInitialPrompt(question, audioUrl));
    }

    private IEnumerator HandleInitialPrompt(string question, string audioUrl)
    {
        if (questionText != null && !string.IsNullOrEmpty(question))
            questionText.text = question;
        if (onQuestionReceived != null && !string.IsNullOrEmpty(question))
            onQuestionReceived.Invoke(question);

        if (!string.IsNullOrEmpty(audioUrl) && questionAudioSource != null)
        {
            yield return StartCoroutine(DownloadAndPlay(audioUrl));
        }

        if (vad != null)
            vad.StartListening();
    }

    private void OnDestroy()
    {
        if (vad != null && vad.onTranscript != null)
            vad.onTranscript.RemoveListener(OnTranscriptReady);
    }

    // Called by VAD when user stops speaking and STT returns a transcript
    private void OnTranscriptReady(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return;
        // Avoid capturing TTS: ensure VAD is not listening while we fetch and play next prompt
        if (vad != null)
            vad.StopListening();
        StartCoroutine(PostAnswerAndHandleNext(transcript));
    }

    private IEnumerator PostAnswerAndHandleNext(string answer)
    {
        int sessionId = PlayerPrefs.GetInt("session_id", 0);
        if (sessionId == 0)
        {
            Debug.LogError("[Interview] No session_id in PlayerPrefs. Start the session first.");
            yield break;
        }

        var url = backendBaseUrl.TrimEnd('/') + interviewPath;
        var payload = new InterviewAnswer { session_id = sessionId, answer = answer };
        var json = JsonUtility.ToJson(payload);

        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Interview] Step post failed: {req.error} - {req.downloadHandler.text}");
            // Resume VAD to allow retry
            if (vad != null) vad.StartListening();
            yield break;
        }

        var txt = req.downloadHandler.text;
        InterviewStepResponse resp = null;
        try { resp = JsonUtility.FromJson<InterviewStepResponse>(txt); }
        catch { Debug.LogError("[Interview] Failed to parse response: " + txt); }

        if (resp == null)
        {
            if (vad != null) vad.StartListening();
            yield break;
        }

        // Display and phonemes
        if (questionText != null && !string.IsNullOrEmpty(resp.question))
            questionText.text = resp.question;
        if (onQuestionReceived != null && !string.IsNullOrEmpty(resp.question))
            onQuestionReceived.Invoke(resp.question);

        // Play audio then restart VAD
        if (!string.IsNullOrEmpty(resp.audio_url) && questionAudioSource != null)
        {
            yield return StartCoroutine(DownloadAndPlay(resp.audio_url));
        }

        // If interview completed, do not restart VAD
        if (!string.IsNullOrEmpty(resp.step) && resp.step == "Exit")
        {
            Debug.Log("[Interview] Completed.");
            yield break;
        }

        if (vad != null)
            vad.StartListening();
    }

    private IEnumerator DownloadAndPlay(string url)
    {
        isPlayingTts = true;
        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[Interview] Failed to download TTS: " + req.error);
            }
            else
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                questionAudioSource.clip = clip;
                questionAudioSource.Play();
                while (questionAudioSource.isPlaying)
                    yield return null;
            }
        }
        isPlayingTts = false;
    }

    // Call from Unity UI button to end interview early and generate report
    public void EndInterview()
    {
        StartCoroutine(EndInterviewRoutine());
    }

    private IEnumerator EndInterviewRoutine()
    {
        int sessionId = PlayerPrefs.GetInt("session_id", 0);
        if (sessionId == 0)
        {
            Debug.LogError("[Interview] No active session to end.");
            yield break;
        }

        var url = backendBaseUrl.TrimEnd('/') + $"/api/interview/{sessionId}/interrupt/";
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Interview] Failed to end interview: {req.error} - {req.downloadHandler.text}");
            yield break;
        }

        Debug.Log("[Interview] Interview ended via interrupt endpoint.");

        if (vad != null)
            vad.StopListening();

        if (questionText != null)
            questionText.text = "Interview ended. Thank you!";
    }

    [System.Serializable]
    private class InterviewAnswer
    {
        public int session_id;
        public string answer;
    }

    [System.Serializable]
    private class InterviewStepResponse
    {
        public int session_id;
        public string step;
        public string question;
        public string audio_url;
        public int remaining_sections;
        public int remaining_questions;
        public string report_url;
        public string error;
    }
}
