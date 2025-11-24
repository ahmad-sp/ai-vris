using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using System.Text.RegularExpressions;

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
    // Use the endpoint you gave: /api/interview/
    public string backendBaseUrl = "http://127.0.0.1:8000";
    public string createSessionPath = "/api/interview/"; // UPDATED to the endpoint you reported

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
        // optionally prefill from PlayerPrefs, safe checks
        if (nameInput != null)
            nameInput.SetTextWithoutNotify(PlayerPrefs.GetString(KEY_CANDIDATE_NAME, ""));
        if (roleInput != null)
            roleInput.SetTextWithoutNotify(PlayerPrefs.GetString(KEY_CANDIDATE_ROLE, ""));

        if (validationText != null) validationText.text = "";
        candidateFormPanel.SetActive(true);
        // optionally focus input (not always available on all targets)
        if (nameInput != null) nameInput.Select();
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
        if (validationText != null) validationText.text = "Checking server...";

        string allowHeader = null;

        // 1) OPTIONS: check allowed methods (not all servers respond, but it's helpful)
        using (UnityWebRequest optionsReq = UnityWebRequest.Put(url, "")) // create request object
        {
            optionsReq.method = "OPTIONS";
            optionsReq.downloadHandler = new DownloadHandlerBuffer();
            optionsReq.timeout = 8;
            yield return optionsReq.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (optionsReq.result == UnityWebRequest.Result.ConnectionError || optionsReq.result == UnityWebRequest.Result.ProtocolError)
#else
            if (optionsReq.isNetworkError || optionsReq.isHttpError)
#endif
            {
                Debug.LogWarning("[CandidateFormController] OPTIONS request failed: " + optionsReq.error + " raw: " + optionsReq.downloadHandler?.text);
                // Continue — some servers don't respond to OPTIONS, we will still try POST below
            }
            else
            {
                allowHeader = optionsReq.GetResponseHeader("Allow");
                Debug.Log("[CandidateFormController] OPTIONS Allow: " + allowHeader);
            }
        }

        // If server explicitly disallows POST, show message and stop
        if (!string.IsNullOrEmpty(allowHeader) && !allowHeader.ToUpper().Contains("POST"))
        {
            string msg = $"Server does not allow POST at this URL. Allowed: {allowHeader}";
            Debug.LogError("[CandidateFormController] " + msg);
            if (validationText != null)
                validationText.text = "Server doesn't accept POST here.\nAllowed: " + allowHeader + "\nCheck API endpoint or server.";
            Debug.Log("[CandidateFormController] Quick test with curl (try in terminal):");
            Debug.Log($"curl -i -X OPTIONS {url}");
            Debug.Log($"curl -v -H \"Content-Type: application/json\" -X POST -d '{{\"candidate_name\":\"{name}\",\"role\":\"{role}\"}}' {url}");
            yield break;
        }

        // 2) Build JSON payload
        var payloadObj = new CreateSessionPayload { candidate_name = name, role = role };
        string json = JsonUtility.ToJson(payloadObj);
        Debug.Log("[CandidateFormController] POST payload: " + json);

        using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
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
                if (validationText != null)
                    validationText.text = "Failed to create session: " + uwr.error;
                Debug.Log($"Server response body: {uwr.downloadHandler?.text}");
                Debug.Log("Try testing with curl:");
                Debug.Log($"curl -v -H \"Content-Type: application/json\" -X POST -d '{json}' {url}");
                yield break;
            }

            // Parse response (try multiple formats)
            string resp = uwr.downloadHandler.text;
            int sessionId = 0;
            try
            {
                // Primary parse: expects {"session_id": 122}
                var wrapper = JsonUtility.FromJson<SessionCreateResponse>(resp);
                if (wrapper != null && wrapper.session_id != 0) sessionId = wrapper.session_id;
                else
                {
                    Debug.LogWarning("[CandidateFormController] session_id not found in response JSON. Raw: " + resp);
                    // fallback: try to extract 'session_id' or 'id' numeric value using regex
                    try
                    {
                        Match m = Regex.Match(resp, @"""session_id""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                        if (!m.Success) m = Regex.Match(resp, @"""id""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            int.TryParse(m.Groups[1].Value, out sessionId);
                            Debug.LogWarning("[CandidateFormController] extracted session id via regex: " + sessionId);
                        }
                    }
                    catch (System.Exception rex)
                    {
                        Debug.LogWarning("[CandidateFormController] regex parse failed: " + rex.Message);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CandidateFormController] Failed to parse create-session response: " + ex.Message + " raw: " + resp);
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
    private class CreateSessionPayload
    {
        public string candidate_name;
        public string role;
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
