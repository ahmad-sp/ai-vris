using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;

/// <summary>
/// ReportsController
/// - Fetches sessions list from sessionsEndpoint
/// - Displays clickable items in a ScrollView
/// - Fetches full report from reportBaseEndpoint + sessionId when an item is clicked
/// - Formats the raw report string into richer TMP-friendly text
/// - Shows either a detail panel (from history) or the finalReportPanel (single report)
/// </summary>
public class ReportsController : MonoBehaviour
{
    [Header("Panels (assign in Inspector)")]
    public GameObject reportsPanel;        // main list popup (inactive by default)
    public GameObject reportDetailPanel;   // popup for a selected historic report
    public GameObject finalReportPanel;    // popup shown after interview with one report
    public TextMeshProUGUI detailReportText;
    public TextMeshProUGUI finalReportText;

    [Header("List UI")]
    public RectTransform listContent;      // content area of ScrollView
    public GameObject reportItemPrefab;    // prefab: Button (root) with TMP label child
    public ScrollRect listScrollRect;

    [Header("Server endpoints (set these in Inspector)")]
    public string sessionsEndpoint = "http://127.0.0.1:8000/api/interview/sessions/"; // returns array
    public string reportBaseEndpoint = "http://127.0.0.1:8000/api/report/"; // append <session_id>/

    [Header("Networking")]
    public float fetchTimeoutSeconds = 8f;

    [Header("Optional")]
    public AudioSource uiClickSound;

    // Local cache
    List<SessionItem> sessionsCache = new List<SessionItem>();

    [Serializable]
    public class SessionItem
    {
        public int session_id;
        public string candidate_name;
        public string role;
        public bool completed;
        public string report; // may be null or string
    }

    // server-side report response example (we parse only fields we need)
    [Serializable]
    public class ReportResponse
    {
        public string candidate;
        public string status;
        public string role;
        public string report; // full report string (raw)
    }

    void Start()
    {
        if (reportsPanel != null) reportsPanel.SetActive(false);
        if (reportDetailPanel != null) reportDetailPanel.SetActive(false);
        if (finalReportPanel != null) finalReportPanel.SetActive(false);
    }

    // --------------------
    // Public API (call these)
    // --------------------
    public void OpenReportsPanel()
    {
        PlayClick();
        if (reportsPanel == null)
        {
            Debug.LogError("[ReportsController] reportsPanel not assigned.");
            return;
        }

        reportsPanel.SetActive(true);
        // clear existing items and reload
        StartCoroutine(FetchAndPopulateSessions());
    }

    public void CloseReportsPanel()
    {
        if (reportsPanel != null) reportsPanel.SetActive(false);
    }

    public void CloseReportDetail()
    {
        if (reportDetailPanel != null) reportDetailPanel.SetActive(false);
    }

    public void CloseFinalReport()
    {
        if (finalReportPanel != null) finalReportPanel.SetActive(false);
    }

    /// Call this at the end of an interview flow if you have the session id.
    /// This will fetch the server report for that session and show the finalReportPanel.
    public void ShowFinalReportFromServer(int sessionId)
    {
        PlayClick();
        StartCoroutine(FetchReportCoroutine(sessionId, (formatted, raw) =>
        {
            if (finalReportText != null)
            {
                finalReportText.text = formatted;
                if (finalReportPanel != null) finalReportPanel.SetActive(true);
            }
            // Optionally: append to local cache (not implemented here) or tell server to persist
        }));
    }

    // --------------------
    // Networking + population
    // --------------------
    IEnumerator FetchAndPopulateSessions()
    {
        // clear current UI
        if (listContent != null)
        {
            for (int i = listContent.childCount - 1; i >= 0; --i)
                Destroy(listContent.GetChild(i).gameObject);
        }
        sessionsCache.Clear();

        if (string.IsNullOrEmpty(sessionsEndpoint))
        {
            Debug.LogWarning("[ReportsController] sessionsEndpoint not set.");
            yield break;
        }

        using (UnityWebRequest uwr = UnityWebRequest.Get(sessionsEndpoint))
        {
            uwr.timeout = (int)fetchTimeoutSeconds;
            yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
#else
            if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                Debug.LogWarning("[ReportsController] sessions fetch error: " + uwr.error);
                // fallback: show nothing or show local cached entries if you implement caching
                yield break;
            }

            string json = uwr.downloadHandler.text;
            // the response is an array of objects
            try
            {
                // Unity's JsonUtility doesn't parse root arrays directly, so wrap it
                SessionItem[] arr = JsonHelper.FromJson<SessionItem>(json);
                if (arr != null)
                {
                    sessionsCache.AddRange(arr);
                    PopulateSessionList(sessionsCache);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ReportsController] Failed parsing sessions JSON: " + ex.Message);
            }
        }
    }

    void PopulateSessionList(List<SessionItem> sessions)
    {
        if (listContent == null || reportItemPrefab == null) return;

        // create items (most recent first by session_id)
        sessions.Sort((a, b) => b.session_id.CompareTo(a.session_id));

        foreach (var s in sessions)
        {
            GameObject go = Instantiate(reportItemPrefab, listContent);
            Button b = go.GetComponent<Button>();
            TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>();

            string title = $"#{s.session_id} • {s.candidate_name} • {s.role}";
            if (!s.completed) title += " • (incomplete)";
            if (label != null) label.text = title;

            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                // capture s in closure
                b.onClick.AddListener(() => OnSessionItemClicked(s));
            }
        }

        if (listScrollRect != null) listScrollRect.verticalNormalizedPosition = 1f;
    }

    void OnSessionItemClicked(SessionItem s)
    {
        PlayClick();
        if (s.completed)
        {
            // fetch the server report endpoint for this session id
            StartCoroutine(FetchReportCoroutine(s.session_id, (formattedText, rawReport) =>
            {
                // show in detail panel
                if (reportDetailPanel != null && detailReportText != null)
                {
                    detailReportText.text = formattedText;
                    reportDetailPanel.SetActive(true);
                }
            }));
        }
        else
        {
            // if the session is not completed, show quick message instead
            if (reportDetailPanel != null && detailReportText != null)
            {
                detailReportText.text = "<b>Interview not completed</b>\nThis session has no final report yet.";
                reportDetailPanel.SetActive(true);
            }
        }
    }

    // fetches the single report and returns formatted text through callback
    IEnumerator FetchReportCoroutine(int sessionId, System.Action<string, string> callback)
    {
        string url = reportBaseEndpoint;
        if (!url.EndsWith("/")) url += "/";
        url += sessionId.ToString() + "/";

        using (UnityWebRequest uwr = UnityWebRequest.Get(url))
        {
            uwr.timeout = (int)fetchTimeoutSeconds;
            yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
#else
            if (uwr.isNetworkError || uwr.isHttpError)
#endif
            {
                Debug.LogWarning("[ReportsController] fetch report error: " + uwr.error);
                callback?.Invoke("<b>Error</b>\nUnable to fetch report.", null);
                yield break;
            }

            string json = uwr.downloadHandler.text;
            try
            {
                ReportResponse rr = JsonUtility.FromJson<ReportResponse>(json);
                string raw = rr != null ? rr.report : null;
                if (string.IsNullOrEmpty(raw))
                {
                    // server may return the report wrapped differently (e.g. top-level "report" or nested). Try quick heuristics:
                    // If the response itself looks like the raw string (not JSON fields), treat whole response as report.
                    raw = json.Trim();
                }

                string formatted = FormatRawReportToTMP(raw, rr);
                callback?.Invoke(formatted, raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ReportsController] parse report error: " + ex.Message);
                // fallback: show the raw json nicely escaped
                callback?.Invoke("<b>Report</b>\n" + UnityEngine.Networking.UnityWebRequest.EscapeURL(json), json);
            }
        }
    }

    // --------------------
    // Formatting helper (converts the raw large string into TMP rich text)
    // --------------------
    string FormatRawReportToTMP(string rawReport, ReportResponse meta = null)
    {
        if (string.IsNullOrEmpty(rawReport))
        {
            return "<b>No Report</b>\nNo report text available for this session.";
        }

        // Normalize newlines
        rawReport = rawReport.Replace("\r\n", "\n").Replace("\r", "\n");

        // Trim header-like HTTP lines if present
        if (rawReport.StartsWith("HTTP/") || rawReport.StartsWith("HTTP"))
        {
            // try to find first '{' for JSON start or the first blank line before '{'
            int idx = rawReport.IndexOf('{');
            if (idx >= 0)
            {
                rawReport = rawReport.Substring(idx);
            }
        }

        // Many reports contain a structured report text with headings separated by blank lines.
        // We'll split on double-newline to get sections, then render the first line as bold heading.
        string[] sections = rawReport.Split(new string[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // If meta provided, add top summary header
        if (meta != null)
        {
            sb.AppendLine($"<b>Candidate:</b> {meta.candidate}");
            sb.AppendLine($"<b>Role:</b> {meta.role}");
            sb.AppendLine($"<b>Status:</b> {meta.status}");
            sb.AppendLine("\n");
        }

        foreach (var sec in sections)
        {
            // Take first line as heading if it looks like one (contains colon or starts with a word)
            string[] lines = sec.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) continue;

            string first = lines[0].Trim();
            // Heuristics: treat as heading if it contains "Summary", "Section", "Scores", "Strengths", "Areas", "Recommendations", or ends with ':'
            bool isHeading = first.ToLower().Contains("summary") ||
                             first.ToLower().Contains("section") ||
                             first.ToLower().Contains("score") ||
                             first.ToLower().Contains("strength") ||
                             first.ToLower().Contains("area") ||
                             first.ToLower().Contains("recommend") ||
                             first.EndsWith(":") || first.Contains("Candidate") || first.Contains("Overall Score");

            if (isHeading)
            {
                sb.AppendLine($"<b>{first}</b>");
                // append the rest of the lines in this section as normal text
                for (int i = 1; i < lines.Length; ++i)
                {
                    string l = lines[i].Trim();
                    // convert dash list items to bullets
                    if (l.StartsWith("- "))
                        sb.AppendLine("\u2022 " + l.Substring(2));
                    else
                        sb.AppendLine(l);
                }
            }
            else
            {
                // Not a heading — print entire section as text, but try to beautify lines starting with '-'
                foreach (var l in lines)
                {
                    string tl = l.Trim();
                    if (tl.StartsWith("- "))
                        sb.AppendLine("\u2022 " + tl.Substring(2));
                    else
                        sb.AppendLine(tl);
                }
            }

            sb.AppendLine("\n"); // extra spacing between sections
        }

        // TMP supports rich text; return final string
        return sb.ToString();
    }

    // --------------------
    // Utilities
    // --------------------
    void PlayClick()
    {
        if (uiClickSound != null) uiClickSound.Play();
    }

    // JsonHelper (wrap/unwrap arrays for UnityJson)
    public static class JsonHelper
    {
        [Serializable]
        private class Wrapper<T>
        {
            public T[] Items;
        }

        public static T[] FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return new T[0];
            string wrapped = "{\"Items\":" + json + "}";
            Wrapper<T> w = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return w.Items ?? new T[0];
        }

        public static string ToJson<T>(T[] array)
        {
            Wrapper<T> w = new Wrapper<T>();
            w.Items = array;
            string wrapped = JsonUtility.ToJson(w);
            int start = wrapped.IndexOf("[");
            int end = wrapped.LastIndexOf("]");
            if (start >= 0 && end >= 0)
                return wrapped.Substring(start, end - start + 1);
            return "[]";
        }
    }
}
