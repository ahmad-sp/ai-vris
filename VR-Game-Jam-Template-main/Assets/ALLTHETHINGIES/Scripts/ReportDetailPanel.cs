using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReportDetailPanel : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI candidateNameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI responsesCountText;
    public TextMeshProUGUI scoredResponsesText;
    public TextMeshProUGUI hasResumeText;
    public TextMeshProUGUI reportContentText;
    public ScrollRect reportScrollRect;
    public Button backButton;
    public Button exportButton;

    [Header("Colors")]
    public Color completedColor = Color.green;
    public Color incompleteColor = Color.yellow;

    private ReportDetailData currentReport;

    [System.Serializable]
    public class ReportDetailData
    {
        public int session_id;
        public string candidate_name;
        public string role;
        public bool completed;
        public string created_at;
        public int responses_count;
        public int scored_responses;
        public string report;
        public bool has_resume;
    }

    void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(BackToReports);
        }

        if (exportButton != null)
        {
            exportButton.onClick.AddListener(ExportReport);
        }
    }

    public void DisplayReport(string jsonResponse)
    {
        currentReport = JsonUtility.FromJson<ReportDetailData>(jsonResponse);
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (currentReport == null) return;

        // Update header information
        if (candidateNameText != null)
            candidateNameText.text = currentReport.candidate_name;

        if (roleText != null)
            roleText.text = currentReport.role;

        if (dateText != null)
        {
            if (System.DateTime.TryParse(currentReport.created_at, out System.DateTime date))
            {
                dateText.text = date.ToString("MMMM dd, yyyy 'at' HH:mm");
            }
            else
            {
                dateText.text = currentReport.created_at;
            }
        }

        if (statusText != null)
        {
            statusText.text = currentReport.completed ? "Completed" : "In Progress";
            statusText.color = currentReport.completed ? completedColor : incompleteColor;
        }

        // Update statistics
        if (responsesCountText != null)
            responsesCountText.text = $"Total Responses: {currentReport.responses_count}";

        if (scoredResponsesText != null)
            scoredResponsesText.text = $"Scored Responses: {currentReport.scored_responses}";

        if (hasResumeText != null)
        {
            hasResumeText.text = currentReport.has_resume ? "Resume: Available" : "Resume: Not Available";
            hasResumeText.color = currentReport.has_resume ? Color.green : Color.gray;
        }

        // Update report content
        if (reportContentText != null)
        {
            reportContentText.text = FormatReportContent(currentReport.report);
            
            // Scroll to top
            if (reportScrollRect != null)
            {
                StartCoroutine(ScrollToTop());
            }
        }

        // Enable export button only if there's content
        if (exportButton != null)
        {
            exportButton.interactable = !string.IsNullOrEmpty(currentReport.report);
        }
    }

    private string FormatReportContent(string rawReport)
    {
        if (string.IsNullOrEmpty(rawReport))
            return "No report content available.";

        // Simple formatting - you can enhance this based on your report structure
        string formatted = rawReport;
        
        // Add line breaks for better readability
        formatted = formatted.Replace("Candidate Summary", "\n=== Candidate Summary ===");
        formatted = formatted.Replace("Summary of Responses", "\n=== Summary of Responses ===");
        formatted = formatted.Replace("Section Scores", "\n=== Section Scores ===");
        formatted = formatted.Replace("Strengths", "\n=== Strengths ===");
        formatted = formatted.Replace("Areas for Improvement", "\n=== Areas for Improvement ===");
        formatted = formatted.Replace("Preliminary Assessment", "\n=== Preliminary Assessment ===");
        formatted = formatted.Replace("Recommendations / Next Steps", "\n=== Recommendations / Next Steps ===");
        
        return formatted;
    }

    private IEnumerator ScrollToTop()
    {
        yield return null; // Wait one frame
        if (reportScrollRect != null)
        {
            reportScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void BackToReports()
    {
        // Find the ReportsManager and call BackToList
        ReportsManager reportsManager = FindObjectOfType<ReportsManager>();
        if (reportsManager != null)
        {
            reportsManager.BackToList();
        }
        else
        {
            // Fallback: deactivate this panel
            gameObject.SetActive(false);
        }
    }

    private void ExportReport()
    {
        if (currentReport == null || string.IsNullOrEmpty(currentReport.report))
            return;

        string fileName = $"Report_{currentReport.candidate_name}_{currentReport.role}_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        
        try
        {
            string content = $"Interview Report\n" +
                           $"================\n" +
                           $"Candidate: {currentReport.candidate_name}\n" +
                           $"Role: {currentReport.role}\n" +
                           $"Date: {currentReport.created_at}\n" +
                           $"Status: {(currentReport.completed ? "Completed" : "In Progress")}\n" +
                           $"Total Responses: {currentReport.responses_count}\n" +
                           $"Scored Responses: {currentReport.scored_responses}\n" +
                           $"Resume Available: {(currentReport.has_resume ? "Yes" : "No")}\n\n" +
                           $"{currentReport.report}";

            System.IO.File.WriteAllText(filePath, content);
            Debug.Log($"Report exported to: {filePath}");
            
            // Optionally show a confirmation message
            ShowExportConfirmation(filePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export report: {e.Message}");
        }
    }

    private void ShowExportConfirmation(string filePath)
    {
        // You could implement a UI popup here
        Debug.Log($"Report successfully exported to: {filePath}");
    }
}
