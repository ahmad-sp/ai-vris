using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

public class InterviewSessionManager : MonoBehaviour
{
    [Header("References")]
    public VADVoiceRecorder vad; // VAD recorder that raises onTranscript

    [Header("UI & Audio")]
    public TextMeshProUGUI questionText; // display interviewer question
    public TextMeshProUGUI metaCombinedText;
    public AudioSource questionAudioSource; // play interviewer TTS audio

    [Header("Report UI")]
    [Tooltip("Panel or tab to show after the interview ends.")]
    [Header("Scroll View")]
    public ScrollRect reportScrollRect;
    public GameObject reportTab;
    [Tooltip("Text element that displays the generated report.")]
    public TextMeshProUGUI reportText;
    [Tooltip("Query string appended to report_url, e.g. '?format=text'. Leave blank to use raw URL.")]
    public string reportFormatQuery = "?format=text";

    [Header("Events")]
    public UnityEvent<string> onQuestionReceived; // forward question to phoneme bridge

    [Header("Backend")] 
    public string backendBaseUrl = "http://127.0.0.1:8000";
    public string interviewPath = "/api/interview/";

    private bool isPlayingTts = false;
    private Coroutine reportFetchRoutine;

    

    private void Awake()
    {
        if (vad != null)
        {
            if (vad.onTranscript == null)
                vad.onTranscript = new UnityEvent<string>();
            vad.onTranscript.AddListener(OnTranscriptReady);
        }
    }

    void Start()
    {
        Debug.Log("[InterviewSessionManager] Start()");
        int sessionId = PlayerPrefs.GetInt("session_id", 0);
        Debug.Log($"[InterviewSessionManager] Found sessionId = {sessionId}");

        if (sessionId != 0)
        {
            // start server-based flow
            StartCoroutine(TryStartFromServer(sessionId));
        }
        else
        {
            Debug.LogWarning("[InterviewSessionManager] No session id found - starting local interview flow.");
            StartInterviewLocally();
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

    private IEnumerator TryStartFromServer(int sessionId)
    {
        // Build URL: backendBaseUrl + interviewPath + sessionId + "/start/"
        string url = backendBaseUrl.TrimEnd('/') + "/" + interviewPath.Trim('/') + "/" + sessionId + "/start/";
        Debug.Log("[InterviewSessionManager] Requesting start from: " + url);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result == UnityWebRequest.Result.ConnectionError)
#else
            if (req.isNetworkError)
#endif
            {
                Debug.LogWarning("[InterviewSessionManager] Network error while fetching start: " + req.error);
                StartInterviewLocally();
                yield break;
            }

#if UNITY_2020_1_OR_NEWER
            if (req.result == UnityWebRequest.Result.ProtocolError)
#else
            if (req.isHttpError)
#endif
            {
                Debug.LogWarning($"[InterviewSessionManager] Server returned HTTP {req.responseCode}. Body: {req.downloadHandler.text}");
                // 404 or error -> fallback to local
                StartInterviewLocally();
                yield break;
            }

            string json = req.downloadHandler.text;
            Debug.Log("[InterviewSessionManager] Start response: " + json);

            // Expecting JSON like: { "question": "...", "audio_url": "http://..." }
            string question = ExtractJsonString(json, "question");
            string audioUrl = ExtractJsonString(json, "audio_url");

            if (!string.IsNullOrEmpty(question))
            {
                if (questionText != null) questionText.text = question;
                else Debug.LogWarning("[InterviewSessionManager] questionText is null, can't show question.");
            }
            else
            {
                Debug.LogWarning("[InterviewSessionManager] No question field in response.");
            }

            if (!string.IsNullOrEmpty(audioUrl) && questionAudioSource != null)
            {
                Debug.Log("[InterviewSessionManager] Downloading audio: " + audioUrl);
                yield return StartCoroutine(DownloadAndPlayAudio(audioUrl));
            }
            else
            {
                Debug.Log("[InterviewSessionManager] No audio URL or audio source; starting VAD/listen now.");
                StartInterviewListening();
            }
        }
    }

    // Simple JSON extractor (very basic; adapt if your server JSON is nested)
    private string ExtractJsonString(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, $"\"{key}\"\\s*:\\s*\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) return UnityWebRequest.UnEscapeURL(m.Groups[1].Value);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[InterviewSessionManager] ExtractJsonString error: " + ex.Message);
        }
        return null;
    }

    private IEnumerator DownloadAndPlayAudio(string audioUrl)
    {
        // Reuse existing audio download helper, then start listening
        yield return StartCoroutine(DownloadAndPlay(audioUrl));
        StartInterviewListening();
    }

    private void StartInterviewListening()
    {
        Debug.Log("[InterviewSessionManager] Starting VAD / listening for candidate response.");
        if (vad != null)
        {
            vad.StartListening();
        }
        else
        {
            Debug.LogWarning("[InterviewSessionManager] vad reference is null.");
        }
    }

    private void StartInterviewLocally()
    {
        // fallback local question: set a default prompt and start listening
        Debug.Log("[InterviewSessionManager] Fallback: starting local interview.");
        if (questionText != null) questionText.text = "Hello — tell me about yourself.";
        StartInterviewListening(); 
    }

    private IEnumerator FetchInitialQuestionAndStart(int sessionId)
    {
        string url = backendBaseUrl.TrimEnd('/') + $"/api/interview/{sessionId}/start/";
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[Interview] Failed to fetch initial question: " + req.error);
                // fallback: start listening or show message
                if (vad != null) vad.StartListening();
                yield break;
            }

            // parse response (assume JSON { question: "...", audio_url: "..." })
            try
            {
                var resp = JsonUtility.FromJson<InitialQuestionResponse>(req.downloadHandler.text);
                StartCoroutine(HandleInitialPrompt(resp.question, resp.audio_url));
            }
            catch
            {
                Debug.LogWarning("[Interview] Could not parse initial question response.");
                if (vad != null) vad.StartListening();
            }
        }
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

        if (metaCombinedText != null)
        {
            var stepVal = string.IsNullOrEmpty(resp.step) ? "" : resp.step;
            metaCombinedText.text = $" section:{stepVal}\n remaining question:{resp.remaining_questions} \n remaining section:{resp.remaining_sections}";
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
            HandleInterviewCompleted(resp.report_url);
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
            Debug.LogError($"[Interview] Failed to end interview: {req.error} - {req.downloadHandler?.text}");
            yield break;
        }

        Debug.Log("[Interview] Interview ended via interrupt endpoint.");

        if (vad != null)
            vad.StopListening();

        if (questionText != null)
            questionText.text = "Interview ended. Generating report (this may take ~25 seconds)...";

        string reportUrl = null;
        var body = req.downloadHandler != null ? req.downloadHandler.text : null;
        if (!string.IsNullOrEmpty(body))
        {
            try
            {
                var interruptResp = JsonUtility.FromJson<InterruptResponse>(body);
                reportUrl = interruptResp != null ? interruptResp.report_url : null;
            }
            catch
            {
                Debug.LogWarning("[Interview] Could not parse interrupt response for report URL: " + body);
            }
        }

        // Show the report tab immediately with a loading message
        if (reportTab != null)
            reportTab.SetActive(true);

        // Show 10-second countdown
        for (int i = 10; i > 0; i--)
        {
            if (reportText != null)
                reportText.text = $"Generating your report...\nStarting in {i} seconds";
            yield return new WaitForSeconds(1f);
        }

        if (reportText != null)
            reportText.text = "Finalizing your report...";

        // Wait a bit more for the final message to be visible
        yield return new WaitForSeconds(1f);

        // Now handle the interview completion with the report URL
        HandleInterviewCompleted(reportUrl);
    }

    private void HandleInterviewCompleted(string reportUrl)
    {
        if (vad != null)
            vad.StopListening();

        if (questionText != null)
            questionText.text = "Interview completed. Preparing your report...";

        if (reportTab != null)
            reportTab.SetActive(true);

        if (reportText != null)
            reportText.text = "Preparing your report...";

        if (reportFetchRoutine != null)
        {
            StopCoroutine(reportFetchRoutine);
            reportFetchRoutine = null;
        }
        if (reportScrollRect != null)
        {
            reportScrollRect.normalizedPosition = new Vector2(0, 1); // Top
        }

        // Get the session ID from PlayerPrefs
        int sessionId = PlayerPrefs.GetInt("session_id", 0);
        if (sessionId == 0)
        {
            Debug.LogError("[Report] No session ID found in PlayerPrefs");
            if (reportText != null)
                reportText.text = "Error: Could not find interview session. Please try again.";
            return;
        }

        // Construct the report URL using the base URL and session ID
        string finalUrl = $"{backendBaseUrl.TrimEnd('/')}/api/reports/{sessionId}/";
        Debug.Log($"[Report] Fetching report from: {finalUrl}");

        reportFetchRoutine = StartCoroutine(FetchAndDisplayReport(finalUrl));
    }

    [System.Serializable]
    private class ReportResponse
    {
        public string report;
    }

    [System.Serializable]
    private class InitialQuestionResponse
    {
        public string question;
        public string audio_url;
    }

    private IEnumerator FetchAndDisplayReport(string url)
    {
        Debug.Log($"[Report] Starting to fetch report from: {url}");
        
        using (var req = UnityWebRequest.Get(url))
        {
            // Set a reasonable timeout (30 seconds)
            req.timeout = 30;
            
            if (reportText != null)
                reportText.text = "Fetching your report...";

            // Send the request
            yield return req.SendWebRequest();

            // Check for network errors
            if (req.result == UnityWebRequest.Result.ConnectionError || 
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMsg = $"Error loading report: {req.error}\n";
                errorMsg += $"Status: {req.responseCode}\n";
                errorMsg += $"URL: {url}\n";
                errorMsg += "Please check your connection and try again.";
                
                Debug.LogError($"[Report] {errorMsg}");
                
                if (reportText != null)
                    reportText.text = errorMsg;
                
                yield break;
            }

            // Check for HTTP errors
            if (req.responseCode >= 400)
            {
                string errorMsg = $"Server error: {req.responseCode}\n";
                errorMsg += $"Response: {req.downloadHandler?.text}\n";
                errorMsg += "Please try again later or contact support if the problem persists.";
                
                Debug.LogError($"[Report] {errorMsg}");
                
                if (reportText != null)
                    reportText.text = errorMsg;
                
                yield break;
            }

            // Parse the JSON response
            string jsonResponse = req.downloadHandler?.text;
            if (string.IsNullOrEmpty(jsonResponse))
            {
                string errorMsg = "Error: Received empty response from server.";
                Debug.LogError($"[Report] {errorMsg}");
                
                if (reportText != null)
                    reportText.text = errorMsg;
                
                yield break;
            }

            try
            {
                // Parse the JSON to extract just the report
                var response = JsonUtility.FromJson<ReportResponse>(jsonResponse);
                if (response == null || string.IsNullOrEmpty(response.report))
                {
                    throw new System.Exception("Could not parse report from response");
                }

                // Clean up the report text - replace \n with actual newlines
                string cleanReport = response.report;
                cleanReport = cleanReport.Replace("\n", "\n\n"); // Double newlines for paragraph spacing
                cleanReport = cleanReport.Replace("\n\n\n", "\n\n"); // Remove triple newlines
                cleanReport = cleanReport.Trim();

                Debug.Log($"[Report] Successfully parsed report ({cleanReport.Length} characters)");
                
                if (reportText != null)
                    reportText.text = cleanReport;
            }
            catch (System.Exception ex)
            {
                string errorMsg = $"Error parsing report: {ex.Message}\n\nRaw response:\n{jsonResponse}";
                Debug.LogError($"[Report] {errorMsg}");
                
                if (reportText != null)
                    reportText.text = errorMsg;
            }
        }

        reportFetchRoutine = null;
    }

    private string AppendFormatQuery(string baseUrl, string query)
    {
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(query))
            return baseUrl;

        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return baseUrl;

        if (trimmed.StartsWith("?"))
        {
            if (baseUrl.Contains("?"))
                return baseUrl + "&" + trimmed.Substring(1);
            return baseUrl + trimmed;
        }

        if (baseUrl.Contains("?"))
            return baseUrl + "&" + trimmed;
        return baseUrl + "?" + trimmed;
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

    [System.Serializable]
    private class InterruptResponse
    {
        public string message;
        public string report_url;
    }
}
