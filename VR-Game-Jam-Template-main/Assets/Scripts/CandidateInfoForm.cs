using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.Events;

public class CandidateInfoForm : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField candidateNameInput;

    [Tooltip("Enable to use a dropdown for role selection. If false, provide a TMP_InputField for free text role input.")]
    public bool useDropdownForRole = true;
    public TMP_Dropdown roleDropdown; // used when useDropdownForRole = true
    public TMP_InputField roleInput;  // used when useDropdownForRole = false

    [Header("Resume Upload")]
    public Button resumeSelectButton; // Button to select resume file
    public TextMeshProUGUI resumeFileNameText; // Display selected file name
    public TMP_InputField resumePathInput; // Optional: manual file path input
    private string selectedResumePath = ""; // Store selected file path

    [Header("Buttons")]
    public Button submitButton;

    [Header("Feedback UI (optional)")]
    public TextMeshProUGUI validationMessage; // Optional: show validation errors

    [Header("Interview UI & Audio (optional)")]
    public TextMeshProUGUI questionText; // where to display interviewer question
    public AudioSource questionAudioSource; // to play interviewer audio

    [Header("Question Visual (optional)")]
    public Image questionImage; // UI image shown only after submit

    [Header("Interview Meta (optional)")]
    public TextMeshProUGUI stepText;
    public TextMeshProUGUI remainingQuestionsText;
    public TextMeshProUGUI remainingSectionsText;
    public TextMeshProUGUI metaCombinedText; // formatted single-line view

    [Header("Recording (optional)")]
    public VoiceRecorder voiceRecorder; // existing recorder to start listening

    [Header("UI Container (optional)")]
    public GameObject formContainer; // assign the panel to hide after submit

    [Header("Events (optional)")]
    public UnityEvent<string> onQuestionReceived; // hook to phoneme system

    [Header("Loop Manager (optional)")]
    public InterviewSessionManager interviewManager; // if set, manager handles prompt + VAD loop

    [Header("Flow")]
    [Tooltip("When enabled, loads the next scene automatically after successful submit using SceneTransitionManager.")]
    public bool autoStartNextScene = false;
    public int nextSceneBuildIndex = 1;

    [Header("Prefs Keys")]
    public string candidateNameKey = "candidate_name";
    public string candidateRoleKey = "candidate_role";

    [Header("Backend")]
    public string backendBaseUrl = "http://127.0.0.1:8000";
    public string interviewPath = "/api/interview/";
    public string resumeUploadPath = "/api/resume-upload/";

    private void Awake()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);

        // Setup resume select button
        if (resumeSelectButton != null)
            resumeSelectButton.onClick.AddListener(OnResumeSelectClicked);

        // Setup resume path input to update selection when user types
        if (resumePathInput != null)
            resumePathInput.onEndEdit.AddListener(OnResumePathInputChanged);

        // Pre-fill from PlayerPrefs if available
        if (candidateNameInput != null && PlayerPrefs.HasKey(candidateNameKey))
            candidateNameInput.text = PlayerPrefs.GetString(candidateNameKey);

        if (useDropdownForRole && roleDropdown != null && PlayerPrefs.HasKey(candidateRoleKey))
        {
            // Try to match existing option by text
            var savedRole = PlayerPrefs.GetString(candidateRoleKey);
            for (int i = 0; i < roleDropdown.options.Count; i++)
            {
                if (roleDropdown.options[i].text == savedRole)
                {
                    roleDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }
        else if (!useDropdownForRole && roleInput != null && PlayerPrefs.HasKey(candidateRoleKey))
        {
            roleInput.text = PlayerPrefs.GetString(candidateRoleKey);
        }

        // Initialize resume UI
        UpdateResumeUI();

        // Hide question visual until submit succeeds
        if (questionImage != null)
            questionImage.gameObject.SetActive(false);
    }

    public void OnSubmitClicked()
    {
        var nameVal = candidateNameInput != null ? candidateNameInput.text.Trim() : string.Empty;
        string roleVal = string.Empty;

        if (useDropdownForRole && roleDropdown != null)
        {
            roleVal = roleDropdown.options != null && roleDropdown.options.Count > 0
                ? roleDropdown.options[roleDropdown.value].text.Trim()
                : string.Empty;
        }
        else if (!useDropdownForRole && roleInput != null)
        {
            roleVal = roleInput.text.Trim();
        }

        if (string.IsNullOrEmpty(nameVal) || string.IsNullOrEmpty(roleVal))
        {
            SetValidationMessage("Please enter both name and role.");
            return;
        }

        PlayerPrefs.SetString(candidateNameKey, nameVal);
        PlayerPrefs.SetString(candidateRoleKey, roleVal);
        PlayerPrefs.Save();

        SetValidationMessage("");
        Debug.Log($"Saved Candidate Info: name='{nameVal}', role='{roleVal}'");

        StartCoroutine(SubmitAndStartSession(nameVal, roleVal));
    }

    public void OnResumeSelectClicked()
    {
        // Try to use manual path input if available
        if (resumePathInput != null && !string.IsNullOrEmpty(resumePathInput.text))
        {
            ValidateAndSetResumePath(resumePathInput.text.Trim());
            return;
        }

        // For VR/Editor: Use native file picker if available
        // Note: This requires platform-specific implementation
        // For now, we'll use the manual path input approach
        OpenFilePicker();
    }

    private void OnResumePathInputChanged(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            ValidateAndSetResumePath(path.Trim());
        }
    }

    private void ValidateAndSetResumePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            selectedResumePath = "";
            UpdateResumeUI();
            return;
        }

        if (System.IO.File.Exists(path))
        {
            // Check if it's a PDF file
            string extension = System.IO.Path.GetExtension(path).ToLower();
            if (extension == ".pdf")
            {
                selectedResumePath = path;
                UpdateResumeUI();
                SetValidationMessage("");
                Debug.Log($"Resume selected: {path}");
            }
            else
            {
                SetValidationMessage("Please select a PDF file (.pdf)");
            }
        }
        else
        {
            SetValidationMessage("File not found at specified path.");
        }
    }

    private void OpenFilePicker()
    {
        // Use native file picker to select PDF file
        string selectedPath = FilePicker.PickPDFFile("Select Your Resume PDF", "");
        
        if (!string.IsNullOrEmpty(selectedPath))
        {
            ValidateAndSetResumePath(selectedPath);
        }
        else
        {
            // User cancelled or file picker not available
            // Fallback to manual input if available
            if (resumePathInput != null)
            {
                resumePathInput.Select();
                resumePathInput.ActivateInputField();
                SetValidationMessage("File picker cancelled. You can enter the file path manually above.");
            }
            else
            {
                Debug.Log("File picker cancelled or not available on this platform.");
            }
        }
    }

    private void UpdateResumeUI()
    {
        if (resumeFileNameText != null)
        {
            if (!string.IsNullOrEmpty(selectedResumePath))
            {
                resumeFileNameText.text = System.IO.Path.GetFileName(selectedResumePath);
            }
            else
            {
                resumeFileNameText.text = "No file selected";
            }
        }

        if (resumePathInput != null && !string.IsNullOrEmpty(selectedResumePath))
        {
            resumePathInput.text = selectedResumePath;
        }
    }

    private void SetValidationMessage(string msg)
    {
        if (validationMessage != null)
        {
            validationMessage.text = msg;
        }
    }

    [System.Serializable]
    private class InterviewCreateResponse
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
    private class InterviewCreateRequest
    {
        public string candidate_name;
        public string role;
    }

    private System.Collections.IEnumerator SubmitAndStartSession(string nameVal, string roleVal)
    {
        var url = backendBaseUrl.TrimEnd('/') + interviewPath;
        var payload = new InterviewCreateRequest { candidate_name = nameVal, role = roleVal };
        var json = JsonUtility.ToJson(payload);

        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            var serverMsg = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (!string.IsNullOrEmpty(serverMsg))
                SetValidationMessage(serverMsg);
            else
                SetValidationMessage("Submission failed. Please check your role and network.");
            Debug.LogError($"Interview create failed: {request.error} - {request.downloadHandler.text}");
            yield break;
        }

        var txt = request.downloadHandler.text;
        InterviewCreateResponse resp = null;
        try
        {
            resp = JsonUtility.FromJson<InterviewCreateResponse>(txt);
        }
        catch
        {
            Debug.LogError("Failed to parse interview create response: " + txt);
        }

        if (resp == null || resp.session_id == 0)
        {
            SetValidationMessage("Server error. Please check role and try again.");
            yield break;
        }

        PlayerPrefs.SetInt("session_id", resp.session_id);
        PlayerPrefs.Save();

        // Upload resume if one was selected
        if (!string.IsNullOrEmpty(selectedResumePath))
        {
            yield return StartCoroutine(UploadResume(resp.session_id, roleVal));
        }

        UpdateMetaUI(resp);

        // Show the question image only after a successful submit/session start
        if (questionImage != null)
            questionImage.gameObject.SetActive(true);

        // If a loop manager exists, let it handle the initial prompt + VAD
        if (interviewManager != null)
        {
            interviewManager.StartWithPrompt(resp.question, resp.audio_url);
        }
        else
        {
            // Handle interviewer prompt locally: show text, play audio, then begin listening
            StartCoroutine(HandleInterviewerPrompt(resp));
        }

        // Hide only the visual container so this MonoBehaviour stays active for coroutines
        if (formContainer != null)
            formContainer.SetActive(false);

        if (autoStartNextScene)
        {
            if (SceneTransitionManager.singleton != null)
            {
                SceneTransitionManager.singleton.GoToSceneAsync(nextSceneBuildIndex);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneBuildIndex);
            }
        }
    }

    private System.Collections.IEnumerator HandleInterviewerPrompt(InterviewCreateResponse resp)
    {
        UpdateMetaUI(resp);
        // Display question text
        if (questionText != null && !string.IsNullOrEmpty(resp.question))
        {
            questionText.text = resp.question;
        }

        // Emit to any phoneme system
        if (onQuestionReceived != null && !string.IsNullOrEmpty(resp.question))
        {
            onQuestionReceived.Invoke(resp.question);
        }

        // Play audio if available, then start listening
        if (!string.IsNullOrEmpty(resp.audio_url) && questionAudioSource != null)
        {
            yield return DownloadAndPlay(resp.audio_url);
        }

        // Start microphone listening after TTS playback (or immediately if no audio)
        StartListening();
    }

    private System.Collections.IEnumerator DownloadAndPlay(string url)
    {
        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to download question audio: " + req.error);
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            questionAudioSource.clip = clip;
            questionAudioSource.Play();
            // Wait until playback ends
            while (questionAudioSource.isPlaying)
                yield return null;
        }
    }

    private void StartListening()
    {
        if (voiceRecorder != null)
        {
            voiceRecorder.StartRecording();
        }
        else
        {
            Debug.LogWarning("VoiceRecorder not assigned; cannot start listening.");
        }
    }

    private System.Collections.IEnumerator UploadResume(int sessionId, string role)
    {
        if (string.IsNullOrEmpty(selectedResumePath) || !System.IO.File.Exists(selectedResumePath))
        {
            Debug.LogWarning("Resume file not found or not selected. Skipping upload.");
            yield break;
        }

        string url = backendBaseUrl.TrimEnd('/') + resumeUploadPath;
        Debug.Log($"Uploading resume to: {url}");

        // Read file bytes
        byte[] fileData = System.IO.File.ReadAllBytes(selectedResumePath);
        string fileName = System.IO.Path.GetFileName(selectedResumePath);

        // Create multipart form data
        WWWForm form = new WWWForm();
        form.AddField("session_id", sessionId.ToString());
        form.AddField("role", role);
        form.AddBinaryData("resume", fileData, fileName, "application/pdf");

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            www.timeout = 60; // 60 second timeout for file upload
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Resume upload failed: {www.error} - {www.downloadHandler?.text}");
                SetValidationMessage("Resume upload failed. Interview will continue without resume.");
            }
            else
            {
                Debug.Log($"✅ Resume uploaded successfully: {www.downloadHandler.text}");
                // Optionally show success message
                if (validationMessage != null)
                {
                    validationMessage.text = "Resume uploaded successfully!";
                }
            }
        }
    }

    private void UpdateMetaUI(InterviewCreateResponse resp)
    {
        if (resp == null) return;
        if (stepText != null) stepText.text = string.IsNullOrEmpty(resp.step) ? "" : resp.step;
        if (remainingQuestionsText != null) remainingQuestionsText.text = resp.remaining_questions.ToString();
        if (remainingSectionsText != null) remainingSectionsText.text = resp.remaining_sections.ToString();
        if (metaCombinedText != null)
        {
            var stepVal = string.IsNullOrEmpty(resp.step) ? "" : resp.step;
            metaCombinedText.text = $" section:{stepVal}\n remaining question:{resp.remaining_questions} \n remaining section:{resp.remaining_sections}";
        }
    }
}
