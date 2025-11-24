using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene names")]
    public string startSceneName = "InterviewRoom";
    public string reportsSceneName = "ReportsScene";

    [Header("Panels (if not using separate reports scene)")]
    public bool useSceneForReports = false;
    public GameObject reportsPanel;
    public GameObject optionsPanel;
    public GameObject helpPanel;
    public GameObject aboutPanel; // <-- Added: About / Credits panel (assign in Inspector)

    [Header("Reports Controller (NEW!)")]
    public ReportsController reportsController;

    [Header("Buttons (optional - assign to ensure runtime hookup)")]
    public Button startUIButton;
    public Button reportsUIButton;
    public Button optionsUIButton;
    public Button helpUIButton;
    public Button quitUIButton;
    public Button aboutUIButton; // <-- Added: About button
    public CandidateFormController candidateFormController;

    [Header("Reports fetch (optional)")]
    public string reportEndpoint = "";
    public TextMeshProUGUI recentReportText;

    [Header("UI / Audio (optional)")]
    public AudioSource uiClickSound;
    public float panelAnimationDuration = 0.15f;

    void Start()
    {
        // Ensure panels are closed at start
        if (reportsPanel != null) reportsPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);
        if (aboutPanel != null) aboutPanel.SetActive(false);

        // Safe programmatic hookup for buttons (prevents inspector-wiring mistakes)
        if (startUIButton != null)
        {
            startUIButton.onClick.RemoveAllListeners();
            startUIButton.onClick.AddListener(OnStartButtonPressed);
        }
        if (reportsUIButton != null)
        {
            reportsUIButton.onClick.RemoveAllListeners();
            reportsUIButton.onClick.AddListener(OnReportsButtonPressed);
        }
        if (optionsUIButton != null)
        {
            optionsUIButton.onClick.RemoveAllListeners();
            optionsUIButton.onClick.AddListener(OnOptionsButtonPressed);
            // NOTE: If you're using OptionsController (recommended), wire the Options button in Inspector
            // to call OptionsController.ToggleOptionsPanel() instead. Both approaches work.
        }
        if (helpUIButton != null)
        {
            helpUIButton.onClick.RemoveAllListeners();
            helpUIButton.onClick.AddListener(OnHelpButtonPressed);
        }
        if (quitUIButton != null)
        {
            quitUIButton.onClick.RemoveAllListeners();
            quitUIButton.onClick.AddListener(OnQuitButtonPressed);
        }
        if (aboutUIButton != null)
        {
            aboutUIButton.onClick.RemoveAllListeners();
            aboutUIButton.onClick.AddListener(OnAboutButtonPressed);
        }
    }

    // ------------------------------
    // Button handlers (wire these in Inspector or via the button fields above)
    // ------------------------------
    public void OnStartButtonPressed()
    {
        Debug.Log("[MainMenu] OnStartButtonPressed called");
        PlayClick();
        if (candidateFormController != null)
        {
            candidateFormController.OpenForm();
            return;
        }

        // fallback: previous behavior
        if (!string.IsNullOrEmpty(startSceneName))
            SceneManager.LoadScene(startSceneName);
        else
            Debug.LogWarning("Start scene name not set in MainMenuController.");
    }

    public void OnReportsButtonPressed()
    {
        Debug.Log("[MainMenu] OnReportsButtonPressed");
        PlayClick();

        // NEW — if ReportsController exists, always use it
        if (reportsController != null)
        {
            reportsController.OpenReportsPanel();
            return;
        }

        // (Below is the original fallback logic)
        if (useSceneForReports)
        {
            if (string.IsNullOrEmpty(reportsSceneName))
            {
                Debug.LogWarning("[MainMenu] reportsSceneName is empty.");
                return;
            }
            // Extra guard: check if scene is in build
            if (!Application.CanStreamedLevelBeLoaded(reportsSceneName))
            {
                Debug.LogError($"[MainMenu] Reports scene '{reportsSceneName}' is not in Build Settings.");
                return;
            }
            SceneManager.LoadScene(reportsSceneName);
            return;
        }

        // using panel
        if (reportsPanel == null)
        {
            Debug.LogError("[MainMenu] reportsPanel is null! Assign it in Inspector.");
            return;
        }

        // toggle open
        TogglePanel(reportsPanel, true);
        Debug.Log("[MainMenu] reportsPanel.SetActive(true) called.");

        // optionally fetch latest report when opening
        if (!string.IsNullOrEmpty(reportEndpoint))
        {
            if (recentReportText == null)
            {
                Debug.LogWarning("[MainMenu] reportEndpoint provided but recentReportText is not assigned. Skipping fetch.");
            }
            else
            {
                Debug.Log("[MainMenu] Starting FetchLatestReportCoroutine...");
                StartCoroutine(FetchLatestReportCoroutine(reportEndpoint));
            }
        }
    }

    public void OnOptionsButtonPressed()
    {
        Debug.Log("[MainMenu] OnOptionsButtonPressed");
        PlayClick();

        // If you prefer the OptionsController to handle loading/saving and animations,
        // call its ToggleOptionsPanel() from the Options button OnClick in the Inspector.
        TogglePanel(optionsPanel);
    }

    public void OnHelpButtonPressed()
    {
        Debug.Log("[MainMenu] OnHelpButtonPressed");
        PlayClick();
        TogglePanel(helpPanel);
    }

    public void OnAboutButtonPressed()
    {
        Debug.Log("[MainMenu] OnAboutButtonPressed");
        PlayClick();
        TogglePanel(aboutPanel);
    }

    public void OnQuitButtonPressed()
    {
        Debug.Log("[MainMenu] OnQuitButtonPressed");
        PlayClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------
    // Helpers
    // ------------------------------
    void PlayClick()
    {
        if (uiClickSound != null)
            uiClickSound.Play();
    }

    public void TogglePanel(GameObject panel, bool? activate = null)
    {
        if (panel == null)
        {
            Debug.LogWarning("[MainMenu] TogglePanel called with null panel.");
            return;
        }

        bool target = activate.HasValue ? activate.Value : !panel.activeSelf;
        Debug.Log($"[MainMenu] TogglePanel {panel.name} -> active={target}");
        panel.SetActive(target);
    }

    // ... keep FetchLatestReportCoroutine and SimpleReport as you had them ...

    IEnumerator FetchLatestReportCoroutine(string url)
    {
        using (UnityWebRequest uwr = UnityWebRequest.Get(url))
        {
            uwr.timeout = 8; // seconds
            yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
#else
            if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                Debug.LogWarning("Report fetch error: " + uwr.error);
                if (recentReportText != null)
                    recentReportText.text = "Unable to load latest report.";
            }
            else
            {
                string json = uwr.downloadHandler.text;
                // Simple parsing with JsonUtility; ensure keys match the class
                try
                {
                    SimpleReport r = JsonUtility.FromJson<SimpleReport>(json);
                    if (r != null)
                    {
                        if (recentReportText != null)
                            recentReportText.text = $"Session: {r.sessionId}\nScore: {r.overallScore}\nSummary: {r.summary}";
                    }
                    else
                    {
                        if (recentReportText != null)
                            recentReportText.text = "No report data.";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Failed parsing report JSON: " + ex.Message);
                    if (recentReportText != null)
                        recentReportText.text = "Invalid report format.";
                }
            }
        }
    }

    [System.Serializable]
    public class SimpleReport
    {
        public string sessionId;
        public int overallScore;
        public string[] topIssues;
        public string summary;
    }

    // Optional panel animation coroutine (scale fade) - uncomment and call if desired
    /*
    IEnumerator AnimatePanelScale(GameObject panel, bool open)
    {
        float t = 0f;
        Vector3 start = open ? new Vector3(0.9f,0.9f,0.9f) : Vector3.one;
        Vector3 end = open ? Vector3.one : new Vector3(0.9f,0.9f,0.9f);
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = panel.AddComponent<CanvasGroup>();
        }
        float startAlpha = open ? 0f : 1f;
        float endAlpha = open ? 1f : 0f;
        panel.SetActive(true); // ensure active for animation

        while (t < panelAnimationDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / panelAnimationDuration);
            panel.transform.localScale = Vector3.Lerp(start, end, f);
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, f);
            yield return null;
        }
        panel.transform.localScale = end;
        cg.alpha = endAlpha;

        if (!open)
            panel.SetActive(false);
    }
    */
}
