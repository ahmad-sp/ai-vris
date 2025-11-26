using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReportItem : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI candidateNameText;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI statusText;
    public Button selectButton;
    public Image backgroundImage;
    
    [Header("Colors")]
    public Color completedColor = Color.green;
    public Color incompleteColor = Color.yellow;

    private ReportsManager.ReportData reportData;
    private ReportsManager reportsManager;

    void Start()
    {
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    public void Setup(ReportsManager.ReportData data, ReportsManager manager)
    {
        reportData = data;
        reportsManager = manager;

        // Update UI with report data
        if (candidateNameText != null)
            candidateNameText.text = data.candidate_name;

        if (roleText != null)
            roleText.text = data.role;

        if (dateText != null)
        {
            // Parse the ISO date and format it
            if (System.DateTime.TryParse(data.created_at, out System.DateTime date))
            {
                dateText.text = date.ToString("MMM dd, yyyy HH:mm");
            }
            else
            {
                dateText.text = data.created_at;
            }
        }

        if (statusText != null)
        {
            statusText.text = data.completed ? "Completed" : "In Progress";
            statusText.color = data.completed ? completedColor : incompleteColor;
        }

        // Update background color based on status
        if (backgroundImage != null)
        {
            Color bgColor = data.completed ? completedColor : incompleteColor;
            bgColor.a = 0.2f; // Make it transparent
            backgroundImage.color = bgColor;
        }

        // Set button interactable based on report availability
        if (selectButton != null)
        {
            selectButton.interactable = data.report_available;
        }
    }

    private void OnSelectClicked()
    {
        if (reportsManager != null && reportData.report_available)
        {
            reportsManager.OnReportSelected(reportData);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        if (backgroundImage != null)
        {
            Color baseColor = reportData.completed ? completedColor : incompleteColor;
            baseColor.a = highlighted ? 0.4f : 0.2f;
            backgroundImage.color = baseColor;
        }
    }
}
