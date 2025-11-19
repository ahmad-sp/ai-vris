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
    public bool useSceneForReports = false;   // if true, Reports loads reportsSceneName; else opens reportsPanel
    public GameObject reportsPanel;           // world-space panel that shows reports
    public GameObject optionsPanel;           // options popup
    public GameObject helpPanel;              // help popup

    [Header("Reports fetch (optional)")]
    [Tooltip("If non-empty and useSceneForReports==false, this will fetch recent report JSON when opening reportsPanel.")]
    public string reportEndpoint = "";        // e.g. https://yourserver/api/reports/latest
    public TextMeshProUGUI recentReportText;  // place to show a short summary

    [Header("UI / Audio (optional)")]
    public AudioSource uiClickSound;
    public float panelAnimationDuration = 0.15f; // if you want to animate panels (simple fade or scale)

    void Start()
    {
        // Ensure panels are closed at start
        if (reportsPanel != null) reportsPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);
    }

    // ------------------------------
    // Button handlers (wire these in Inspector)
    // ------------------------------
    public void OnStartButtonPressed()
    {
        PlayClick();
        if (!string.IsNullOrEmpty(startSceneName))
            SceneManager.LoadScene(startSceneName);
        else
            Debug.LogWarning("Start scene name not set in MainMenuController.");
    }

    public void OnReportsButtonPressed()
    {
        PlayClick();
        if (useSceneForReports)
        {
            if (!string.IsNullOrEmpty(reportsSceneName))
                SceneManager.LoadScene(reportsSceneName);
            else
                Debug.LogWarning("Reports scene name not set in MainMenuController.");
        }
        else
        {
            TogglePanel(reportsPanel, true);
            // optionally fetch latest report when opening
            if (!string.IsNullOrEmpty(reportEndpoint) && recentReportText != null)
            {
                StartCoroutine(FetchLatestReportCoroutine(reportEndpoint));
            }
        }
    }

    public void OnOptionsButtonPressed()
    {
        PlayClick();
        TogglePanel(optionsPanel);
    }

    public void OnHelpButtonPressed()
    {
        PlayClick();
        TogglePanel(helpPanel);
    }

    public void OnQuitButtonPressed()
    {
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

    /// <summary>
    /// Toggle a panel (activate/deactivate). If activate == true, will open; if false, will close.
    /// If activate is omitted, it toggles the current active state.
    /// </summary>
    public void TogglePanel(GameObject panel, bool? activate = null)
    {
        if (panel == null) return;

        bool target;
        if (activate.HasValue) target = activate.Value;
        else target = !panel.activeSelf;

        panel.SetActive(target);
        // Optional: animate (scale/fade) here if needed
        // Example: StartCoroutine(AnimatePanelScale(panel, target));
    }

    // ------------------------------
    // Optional: fetch a simple report JSON
    // Expected JSON shape (example):
    // { "sessionId":"abc123", "overallScore":82, "topIssues":["fillers","pace"], "summary":"Good answers but use less filler words" }
    // ------------------------------
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
                        recentReportText.text = $"Session: {r.sessionId}\nScore: {r.overallScore}\nSummary: {r.summary}";
                    }
                    else
                    {
                        recentReportText.text = "No report data.";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Failed parsing report JSON: " + ex.Message);
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