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
    public Transform reportListContainer;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI errorText;
    public Button backButton;
    public Button refreshButton;

    [Header("API Settings")]
    private string baseUrl = "http://localhost:8000/api/";
    
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
        loadingText.gameObject.SetActive(true);
        errorText.gameObject.SetActive(false);

        string url = baseUrl + "reports/" + sessionId + "/";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            loadingText.gameObject.SetActive(false);

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                ReportDetailPanel detailPanel = reportDetailPanel.GetComponent<ReportDetailPanel>();
                
                if (detailPanel != null)
                {
                    detailPanel.DisplayReport(jsonResponse);
                    reportsListPanel.SetActive(false);
                    reportDetailPanel.SetActive(true);
                }
            }
            else
            {
                errorText.gameObject.SetActive(true);
                errorText.text = "Failed to load report details: " + request.error;
                Debug.LogError("Error loading report detail: " + request.error);
            }
        }
    }

    public void BackToList()
    {
        reportsListPanel.SetActive(true);
        reportDetailPanel.SetActive(false);
    }
}
