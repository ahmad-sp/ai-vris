using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class ReportsManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject reportsListPanel;
    public GameObject reportDetailPanel;
    public GameObject reportItemPrefab;
    public GameObject mainMenu;
    public Transform reportListContainer;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI errorText;
    public Button backButton;
    public Button refreshButton;

    [Header("API Settings")]
    public string baseUrl = "http://127.0.0.1:8000/api/";
    
    private List<ReportData> reports = new List<ReportData>();

    [System.Serializable]
    
    public class ReportData
    {
        public int session_id;
        public string candidate_name;
        public string role;
        public bool completed;
        public string created_at;
        public bool report_available;
    }

    [System.Serializable]
    public class ReportsListResponse
    {
        public List<ReportData> reports;
        public int total_count;
    }

    void Start()
    {
        // Set up button listeners
        backButton.onClick.AddListener(BackToList);
        refreshButton.onClick.AddListener(RefreshReports);
        
        // Load reports on start
        StartCoroutine(LoadReports());
    }

    public void RefreshReports()
    {
        StartCoroutine(LoadReports());
    }

    IEnumerator LoadReports()
    {
        // Show loading state
        loadingText.gameObject.SetActive(true);
        errorText.gameObject.SetActive(false);
        ClearReportList();

        string url = baseUrl + "reports/";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            loadingText.gameObject.SetActive(false);

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                ReportsListResponse response = JsonUtility.FromJson<ReportsListResponse>(jsonResponse);
                
                reports = response.reports;
                PopulateReportList();
            }
            else
            {
                errorText.gameObject.SetActive(true);
                errorText.text = "Failed to load reports: " + request.error;
                Debug.LogError("Error loading reports: " + request.error);
            }
        }
    }

    void ClearReportList()
    {
        foreach (Transform child in reportListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    void PopulateReportList()
    {
        ClearReportList();

        foreach (ReportData report in reports)
        {
            GameObject item = Instantiate(reportItemPrefab, reportListContainer);
            ReportItem reportItem = item.GetComponent<ReportItem>();
            
            if (reportItem != null)
            {
                reportItem.Setup(report, this);
            }
        }
    }

    public void OnReportSelected(ReportData report)
    {
        StartCoroutine(LoadReportDetail(report.session_id));
    }

    IEnumerator LoadReportDetail(int sessionId)
    {
        Debug.Log($"[ReportsManager] Loading report detail for session {sessionId}");
        loadingText.gameObject.SetActive(true);
        errorText.gameObject.SetActive(false);

        string url = baseUrl + "reports/" + sessionId + "/";
        Debug.Log($"[ReportsManager] Request URL: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            loadingText.gameObject.SetActive(false);

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"[ReportsManager] Successfully received report data: {jsonResponse.Substring(0, Mathf.Min(200, jsonResponse.Length))}...");
                
                ReportDetailPanel detailPanel = reportDetailPanel.GetComponent<ReportDetailPanel>();
                
                if (detailPanel != null)
                {
                    Debug.Log("[ReportsManager] Displaying report in detail panel");
                    
                    // IMPORTANT: Activate the panel BEFORE calling DisplayReport
                    // This prevents coroutine errors
                    reportsListPanel.SetActive(false);
                    reportDetailPanel.SetActive(true);
                    
                    // Now display the report (panel is already active)
                    detailPanel.DisplayReport(jsonResponse);
                }
                else
                {
                    Debug.LogError("[ReportsManager] ReportDetailPanel component not found!");
                }
            }
            else
            {
                Debug.LogError($"[ReportsManager] Error loading report detail: {request.error}");
                Debug.LogError($"[ReportsManager] Response Code: {request.responseCode}");
                errorText.gameObject.SetActive(true);
                errorText.text = "Failed to load report details: " + request.error;
            }
        }
    }

    public void BackToList()
    {
        reportsListPanel.SetActive(true);
        reportDetailPanel.SetActive(false);
    }
    public void BackToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main menu");
    }
}
