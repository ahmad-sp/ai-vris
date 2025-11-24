using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;

public class CandidateFormController : MonoBehaviour
{
    [Header("UI")]
    public GameObject candidateFormPanel; // the whole panel root (set inactive by default)
    public TMP_InputField nameInput;
    public TMP_InputField roleInput;
    public TextMeshProUGUI validationText;
    public Button submitButton;
    public Button cancelButton;

    [Header("Scene to load")]
    public string startSceneName = "InterviewRoom"; // change to your scene name

    [Header("Backend")]
    // Set this to your backend base URL, e.g. http://127.0.0.1:8000
    public string backendBaseUrl = "http://127.0.0.1:8000";
    // Endpoint to create a session. I use /api/interview/sessions/ (adjust if your API differs)
    public string createSessionPath = "/api/interview/sessions/";

    // PlayerPrefs keys
    const string KEY_CANDIDATE_NAME = "candidate_name";
    const string KEY_CANDIDATE_ROLE = "candidate_role";
    const string KEY_SESSION_ID = "session_id";

    void Start()
    {
        if (candidateFormPanel != null) candidateFormPanel.SetActive(false);

        // safety hookup
        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CloseForm);
        }

        if (validationText != null) validationText.text = "";
    }

    public void OpenForm()
    {
        if (candidateFormPanel == null)
        {
            Debug.LogWarning("[CandidateFormController] candidateFormPanel not assigned.");
            return;
        }
        // optionally prefill from PlayerPrefs
        nameInput.SetTextWithoutNotify(PlayerPrefs.GetString(KEY_CANDIDATE_NAME, ""));
        roleInput.SetTextWithoutNotify(PlayerPrefs.GetString(KEY_CANDIDATE_ROLE, ""));
        validationText.text = "";
        candidateFormPanel.SetActive(true);
        // optionally focus input (not always available on all targets)
        nameInput.Select();
    }

    public void CloseForm()
    {
        if (candidateFormPanel != null) candidateFormPanel.SetActive(false);
    }

    // Called by Submit button
    public void OnSubmit()
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";
        string role = roleInput != null ? roleInput.text.Trim() : "";

        // Basic validation
        if (string.IsNullOrEmpty(name))
        {
            ShowValidation("Please enter your name.");
            return;
        }
        if (string.IsNullOrEmpty(role))
        {
            ShowValidation("Please enter the role.");
            return;
        }

        // Save locally (so it's available even if session creation fails)
        PlayerPrefs.SetString(KEY_CANDIDATE_NAME, name);
        PlayerPrefs.SetString(KEY_CANDIDATE_ROLE, role);
        PlayerPrefs.Save();

        // Start coroutine to create session then load scene
        StartCoroutine(CreateSessionThenLoad(name, role));
    }

    IEnumerator CreateSessionThenLoad(string name, string role)
    {
        if (string.IsNullOrEmpty(backendBaseUrl) || string.IsNullOrEmpty(createSessionPath))
        {
            Debug.LogWarning("[CandidateFormController] backendBaseUrl or createSessionPath not set. Falling back to direct scene load.");
            candidateFormPanel.SetActive(false);
            if (!string.IsNullOrEmpty(startSceneName))
                SceneManager.LoadScene(startSceneName);
            yield break;
        }

        string url = backendBaseUrl.TrimEnd('/') + "/" + createSessionPath.TrimStart('/');
        // Build JSON payload - adjust keys to match your backend expected fields
        var payloadObj = new { candidate_name = name, role = role };
        string json = JsonUtility.ToJson(payloadObj);

        using (UnityWebRequest uwr = UnityWebRequest.PostWwwForm(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            uwr.uploadHandler = new UploadHandlerRaw(body);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.timeout = 10;

            if (validationText != null) validationText.text = "Creating session...";

            yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
#else
            if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                Debug.LogError("[CandidateFormController] Create session failed: " + uwr.error + " - " + uwr.downloadHandler?.text);
                if (validationText != null) validationText.text = "Failed to create session. Check server.";
                // still allow fallback: load scene without session, or return and let user retry
                yield break;
            }

            string resp = uwr.downloadHandler.text;
            // Try to parse session_id (assumes response JSON contains session_id field)
            int sessionId = 0;
            try
            {
                // Minimal wrapper to extract session_id
                var wrapper = JsonUtility.FromJson<SessionCreateResponse>(resp);
                if (wrapper != null && wrapper.session_id != 0)
                {
                    sessionId = wrapper.session_id;
                }
                else
                {
                    Debug.LogWarning("[CandidateFormController] session_id not found in response, raw: " + resp);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CandidateFormController] Failed to parse create-session response: " + ex.Message);
            }

            if (sessionId != 0)
            {
                PlayerPrefs.SetInt(KEY_SESSION_ID, sessionId);
                PlayerPrefs.Save();
                Debug.Log("[CandidateFormController] Created session id: " + sessionId);
            }
            else
            {
                Debug.LogWarning("[CandidateFormController] No session id returned - proceeding without session id.");
            }

            // Close form and load scene
            candidateFormPanel.SetActive(false);
            if (!string.IsNullOrEmpty(startSceneName))
                SceneManager.LoadScene(startSceneName);
            else
                Debug.LogWarning("[CandidateFormController] startSceneName not set.");
        }
    }

    [System.Serializable]
    private class SessionCreateResponse
    {
        public int session_id;
    }

    void ShowValidation(string msg)
    {
        if (validationText != null)
        {
            validationText.text = msg;
        }
        else
        {
            Debug.Log(msg);
        }
    }

    // Optional helper to read values from other scenes:
    public static string GetSavedName() => PlayerPrefs.GetString(KEY_CANDIDATE_NAME, "");
    public static string GetSavedRole() => PlayerPrefs.GetString(KEY_CANDIDATE_ROLE, "");
}
