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

    private Coroutine reportFetchRoutine;
    private bool initialPromptHandled = false; // Flag to prevent Start() from overriding initial prompt

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
        
        // If initial prompt was already handled (from CandidateInfoForm), don't override it
        if (initialPromptHandled)
        {
            Debug.Log("[InterviewSessionManager] Initial prompt already handled, skipping Start() flow.");
            return;
        }
        
        // Check if we have an initial prompt stored in PlayerPrefs (from scene transition)
        int hasInitialPrompt = PlayerPrefs.GetInt("has_initial_prompt", 0);
        if (hasInitialPrompt == 1)
        {
            string storedQuestion = PlayerPrefs.GetString("initial_question", "");
            string storedAudioUrl = PlayerPrefs.GetString("initial_audio_url", "");
            
            if (!string.IsNullOrEmpty(storedQuestion))
            {
                Debug.Log($"[InterviewSessionManager] Found stored initial prompt in PlayerPrefs. Question: {storedQuestion.Substring(0, Mathf.Min(50, storedQuestion.Length))}...");
                // Clear the flag so we don't use it again
                PlayerPrefs.DeleteKey("has_initial_prompt");
                PlayerPrefs.DeleteKey("initial_question");
                PlayerPrefs.DeleteKey("initial_audio_url");
                PlayerPrefs.Save();
                
                // Use the stored prompt
                initialPromptHandled = true;
                StartCoroutine(HandleInitialPrompt(storedQuestion, storedAudioUrl));
                return;
            }
        }
        
        int sessionId = PlayerPrefs.GetInt("session_id", 0);
        Debug.Log($"[InterviewSessionManager] Found sessionId = {sessionId}");

        if (sessionId != 0)
        {
            // Use the regular interview endpoint to get the current question
            StartCoroutine(FetchCurrentQuestion(sessionId));
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
        Debug.Log($"[InterviewSessionManager] StartWithPrompt called with question: {question?.Substring(0, Mathf.Min(50, question?.Length ?? 0))}..., audioUrl: {audioUrl}");
        initialPromptHandled = true; // Mark that we've handled the initial prompt
        StartCoroutine(HandleInitialPrompt(question, audioUrl));
    }

    private IEnumerator HandleInitialPrompt(string question, string audioUrl)
    {
        Debug.Log($"[InterviewSessionManager] HandleInitialPrompt - Setting question text and playing audio");
        
        if (questionText != null && !string.IsNullOrEmpty(question))
        {
            questionText.text = question;
            Debug.Log($"[InterviewSessionManager] Question text set: {question.Substring(0, Mathf.Min(100, question.Length))}...");
        }
        else
        {
            Debug.LogWarning($"[InterviewSessionManager] Question text is null or empty. questionText={questionText != null}, question={!string.IsNullOrEmpty(question)}");
        }
        
        if (onQuestionReceived != null && !string.IsNullOrEmpty(question))
            onQuestionReceived.Invoke(question);

        if (!string.IsNullOrEmpty(audioUrl) && questionAudioSource != null)
        {
            Debug.Log($"[InterviewSessionManager] Downloading and playing audio from: {audioUrl}");
            yield return StartCoroutine(DownloadAndPlay(audioUrl));
        }
        else
        {
            Debug.LogWarning($"[InterviewSessionManager] Audio URL or AudioSource is null. audioUrl={!string.IsNullOrEmpty(audioUrl)}, audioSource={questionAudioSource != null}");
        }

        if (vad != null)
        {
            Debug.Log("[InterviewSessionManager] Starting VAD listening");
            vad.StartListening();
        }
        else
        {
            Debug.LogWarning("[InterviewSessionManager] VAD is null, cannot start listening");
        }
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

    private IEnumerator FetchCurrentQuestion(int sessionId)
    {
        // Use the regular interview endpoint to get the current question
        // This is called when scene loads and we need to fetch the current state
        string url = backendBaseUrl.TrimEnd('/') + interviewPath;
        Debug.Log($"[InterviewSessionManager] Fetching current question from: {url}");

        // Create a POST request with session_id to get current question
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        var payload = new System.Text.StringBuilder();
        payload.Append("{");
        payload.Append($"\"session_id\": {sessionId}");
        payload.Append("}");
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(payload.ToString());
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[InterviewSessionManager] Failed to fetch current question: {request.error}");
            StartInterviewLocally();
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log($"[InterviewSessionManager] Current question response: {json}");

        // Parse the response (cannot use yield in try-catch, so parse first)
        InterviewResponse response = null;
        try
        {
            response = JsonUtility.FromJson<InterviewResponse>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InterviewSessionManager] Error parsing response: {ex.Message}");
            StartInterviewLocally();
            yield break;
        }

        if (response != null && !string.IsNullOrEmpty(response.question))
        {
            Debug.Log($"[InterviewSessionManager] Setting question from server: {response.question.Substring(0, Mathf.Min(50, response.question.Length))}...");
            if (questionText != null) questionText.text = response.question;
            if (onQuestionReceived != null) onQuestionReceived.Invoke(response.question);

            if (!string.IsNullOrEmpty(response.audio_url) && questionAudioSource != null)
            {
                yield return StartCoroutine(DownloadAndPlay(response.audio_url));
            }
            
            StartInterviewListening();
        }
        else
        {
            Debug.LogWarning("[InterviewSessionManager] No question in response, starting locally");
            StartInterviewLocally();
        }
    }

    [System.Serializable]
    private class InterviewResponse
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

        if (questionText != null)
            questionText.text = "Interview ended. Generating report...";

        // Show countdown while waiting for report generation
        if (reportText != null)
            reportText.text = "Generating your report...\nThis may take a few moments...";

        // Wait for report to be generated with countdown
        yield return StartCoroutine(WaitForReportWithCountdown(sessionId));
        
        // Now fetch and display the report
        HandleInterviewCompleted(reportUrl);
    }

    private IEnumerator WaitForReportWithCountdown(int sessionId)
    {
        string reportUrl = $"{backendBaseUrl.TrimEnd('/')}/api/reports/{sessionId}/";
        int maxWaitTime = 30; // Maximum 30 seconds
        int elapsed = 0;
        int checkInterval = 2; // Check every 2 seconds
        
        Debug.Log($"[Interview] Waiting for report generation at: {reportUrl}");
        
        while (elapsed < maxWaitTime)
        {
            int remaining = maxWaitTime - elapsed;
            
            // Update countdown message
            if (reportText != null)
            {
                reportText.text = $"Generating your report...\nPlease wait {remaining} seconds";
            }
            
            // Try to fetch the report
            using (UnityWebRequest req = UnityWebRequest.Get(reportUrl))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                
                if (req.result == UnityWebRequest.Result.Success)
                {
                    // Check if we got a valid report (not an error)
                    string responseText = req.downloadHandler.text;
                    if (!string.IsNullOrEmpty(responseText) && 
                        !responseText.Contains("\"error\"") && 
                        responseText.Length > 100) // Report should be substantial
                    {
                        Debug.Log("[Interview] Report is ready!");
                        if (reportText != null)
                            reportText.text = "Report generated successfully!";
                        yield return new WaitForSeconds(0.5f);
                        yield break; // Report is ready, exit
                    }
                }
            }
            
            // Wait before next check
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;
        }
        
        // Timeout reached, proceed anyway
        Debug.LogWarning("[Interview] Report generation timeout, proceeding anyway");
        if (reportText != null)
            reportText.text = "Report generation taking longer than expected...\nFetching report...";
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
                {
                    reportText.text = cleanReport;
                    
                    Debug.Log($"[Report] Report text set. Checking scroll rect...");
                    Debug.Log($"[Report] reportScrollRect exists: {reportScrollRect != null}");
                    
                    // Force the scroll content to expand properly
                    if (reportScrollRect != null)
                    {
                        Debug.Log("[Report] Starting ForceReportContentHeight coroutine");
                        StartCoroutine(ForceReportContentHeight());
                    }
                    else
                    {
                        Debug.LogError("[Report] ❌ reportScrollRect is NULL! Assign it in Inspector!");
                        Debug.LogError("[Report] Without ScrollRect, the report won't be scrollable.");
                    }
                }
                else
                {
                    Debug.LogError("[Report] reportText is NULL!");
                }
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
    
    // Force the report scroll content to expand to fit the text
    private IEnumerator ForceReportContentHeight()
    {
        // Wait for layout to be calculated
        yield return new WaitForEndOfFrame();
        
        if (reportScrollRect != null && reportScrollRect.content != null && reportText != null)
        {
            Debug.Log("[Report] === FORCING SCROLL CONTENT HEIGHT ===");
            
            var rt = reportScrollRect.content.GetComponent<RectTransform>();
            
            // Get the text's preferred height
            float textHeight = reportText.preferredHeight;
            Debug.Log($"[Report] Text preferred height: {textHeight}");
            
            // Add minimal padding (reduced from 200 to 5)
            float totalHeight = textHeight - 250;
            
            // Disable ContentSizeFitter if it exists (it will override our height)
            var csf = reportScrollRect.content.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                Debug.Log("[Report] Disabling ContentSizeFitter");
                csf.enabled = false;
            }
            
            // Disable VerticalLayoutGroup if it exists
            var vlg = reportScrollRect.content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                Debug.Log("[Report] Disabling VerticalLayoutGroup");
                vlg.enabled = false;
            }
            
            // Force the content height
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, totalHeight);
            Debug.Log($"[Report] Forced content height to {totalHeight}");
            
            // Wait one more frame
            yield return null;
            
            // Verify it worked
            float actualHeight = rt.rect.height;
            Debug.Log($"[Report] Actual content height: {actualHeight}");
            
            if (actualHeight > 500)
            {
                Debug.Log($"[Report] ✅ SUCCESS! Content is tall enough to scroll");
            }
            else
            {
                Debug.LogWarning($"[Report] ⚠️ Content might be too small: {actualHeight}");
            }
            
            // Reset scroll to top
            reportScrollRect.verticalNormalizedPosition = 1f;
            Debug.Log("[Report] Scroll position reset to top");
        }
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
