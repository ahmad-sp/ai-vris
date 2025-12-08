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
            
            // Scroll to top - use Invoke to avoid coroutine issues
            if (reportScrollRect != null)
            {
                // Reset scroll position immediately
                reportScrollRect.verticalNormalizedPosition = 1f;
                
                // Also schedule it for next frame to ensure it works
                Invoke(nameof(ResetScrollPosition), 0.1f);
            }
        }
    }

    private void ResetScrollPosition()
    {
        if (reportScrollRect != null)
        {
            reportScrollRect.verticalNormalizedPosition = 1f;
        }

        // Enable export button only if there's content
        if (exportButton != null)
        {
            exportButton.interactable = !string.IsNullOrEmpty(currentReport.report);
        }
        
        // Debug scrolling setup
        DebugScrollSetup();
    }
    
    private void DebugScrollSetup()
    {
        Debug.Log("=== SCROLL DEBUG INFO ===");
        
        if (reportScrollRect != null)
        {
            Debug.Log($"✅ ScrollRect exists");
            Debug.Log($"Content assigned: {reportScrollRect.content != null}");
            Debug.Log($"Viewport assigned: {reportScrollRect.viewport != null}");
            Debug.Log($"Vertical enabled: {reportScrollRect.vertical}");
            Debug.Log($"Horizontal enabled: {reportScrollRect.horizontal}");
            
            if (reportScrollRect.content != null)
            {
                float contentHeight = reportScrollRect.content.rect.height;
                Debug.Log($"📏 Content Height: {contentHeight}");
                
                // Check for ContentSizeFitter
                var csf = reportScrollRect.content.GetComponent<ContentSizeFitter>();
                Debug.Log($"ContentSizeFitter exists: {csf != null}");
                if (csf != null)
                {
                    Debug.Log($"  - Vertical Fit: {csf.verticalFit}");
                }
                
                // Check for VerticalLayoutGroup
                var vlg = reportScrollRect.content.GetComponent<VerticalLayoutGroup>();
                Debug.Log($"VerticalLayoutGroup exists: {vlg != null}");
                
                // Check children count
                Debug.Log($"Content children count: {reportScrollRect.content.childCount}");
            }
            
            if (reportScrollRect.viewport != null)
            {
                float viewportHeight = reportScrollRect.viewport.rect.height;
                Debug.Log($"📏 Viewport Height: {viewportHeight}");
                
                if (reportScrollRect.content != null)
                {
                    float contentHeight = reportScrollRect.content.rect.height;
                    if (contentHeight > viewportHeight)
                    {
                        Debug.Log($"✅ Content ({contentHeight}) > Viewport ({viewportHeight}) - SHOULD SCROLL");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Content ({contentHeight}) <= Viewport ({viewportHeight}) - WON'T SCROLL!");
                        Debug.LogWarning("Content needs to be taller than viewport to enable scrolling.");
                    }
                }
            }
        }
        else
        {
            Debug.LogError("❌ reportScrollRect is NULL! Assign it in Inspector.");
        }
        
        if (reportContentText != null)
        {
            Debug.Log($"Report text length: {reportContentText.text.Length} characters");
            Debug.Log($"Report text preferredHeight: {reportContentText.preferredHeight}");
        }
        
        Debug.Log("=== END SCROLL DEBUG ===");
        
        // FORCE CONTENT HEIGHT - Nuclear option to make scrolling work
        if (reportScrollRect != null && reportScrollRect.content != null)
        {
            StartCoroutine(ForceContentHeight());
        }
    }
    
    private System.Collections.IEnumerator ForceContentHeight()
    {
        // Wait for layout to be calculated
        yield return new WaitForEndOfFrame();
        
        if (reportScrollRect != null && reportScrollRect.content != null)
        {
            var rt = reportScrollRect.content.GetComponent<RectTransform>();
            
            // Calculate total height needed
            float totalHeight = 0;
            
            // Add height for all children
            foreach (RectTransform child in reportScrollRect.content)
            {
                if (child.gameObject.activeSelf)
                {
                    // Check if this child has the report text
                    var tmpText = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (tmpText != null && tmpText == reportContentText)
                    {
                        // Use the text's preferred height
                        totalHeight += reportContentText.preferredHeight;
                        Debug.Log($"📝 Added text preferred height: {reportContentText.preferredHeight}");
                    }
                    else
                    {
                        // Use the child's current height
                        totalHeight += child.rect.height;
                        Debug.Log($"📦 Added child height: {child.rect.height}");
                    }
                }
            }
            
            // Add padding from VerticalLayoutGroup if it exists
            var vlg = reportScrollRect.content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                totalHeight += vlg.padding.top + vlg.padding.bottom;
                totalHeight += vlg.spacing * (reportScrollRect.content.childCount - 1);
                Debug.Log($"📐 Added layout padding and spacing: {vlg.padding.top + vlg.padding.bottom + (vlg.spacing * (reportScrollRect.content.childCount - 1))}");
            }
            
            // Add minimal padding for safety (reduced from 100 to 50)
            totalHeight += 50;
            
            Debug.Log($"⚡ FORCING Content height to {totalHeight} pixels");
            
            // CRITICAL: Disable ContentSizeFitter - it will override our height!
            var csf = reportScrollRect.content.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                Debug.Log("🔧 Disabling ContentSizeFitter (it was overriding our height)");
                csf.enabled = false;
            }
            
            // CRITICAL: Disable VerticalLayoutGroup - it might also override!
            if (vlg != null)
            {
                Debug.Log("🔧 Disabling VerticalLayoutGroup (it was overriding our height)");
                vlg.enabled = false;
            }
            
            // Force the content height
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, totalHeight);
            
            Debug.Log($"✅ Content height SET to {totalHeight} pixels");
            
            // Wait one more frame to let it apply
            yield return null;
            
            // Verify it worked
            float actualHeight = rt.rect.height;
            Debug.Log($"🔍 Verification: Content height is now {actualHeight} pixels");
            
            if (actualHeight > 1080)
            {
                Debug.Log($"✅✅✅ SUCCESS! Content ({actualHeight}) > Viewport (1080) - SCROLLING SHOULD WORK!");
            }
            else
            {
                Debug.LogError($"❌ FAILED! Content ({actualHeight}) still too small. Something is overriding the height.");
            }
            
            // Reset scroll to top
            if (reportScrollRect != null)
            {
                reportScrollRect.verticalNormalizedPosition = 1f;
                Debug.Log("📜 Scroll position reset to top");
            }
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
