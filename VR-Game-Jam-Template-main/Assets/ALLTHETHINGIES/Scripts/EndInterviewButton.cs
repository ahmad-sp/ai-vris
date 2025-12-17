using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper component that wires a UI Button to the InterviewSessionManager.EndInterview routine.
/// Attach this script to the button GameObject.
/// </summary>
[RequireComponent(typeof(Button))]
public class EndInterviewButton : MonoBehaviour
{
    [SerializeField] private InterviewSessionManager interviewManager;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void Start()
    {
        // Auto-discover InterviewSessionManager if not assigned in Inspector.
        // Doing this in Start ensures the other object is initialized.
        if (interviewManager == null)
        {
            // Try both modern and legacy find methods
            interviewManager = FindFirstObjectByType<InterviewSessionManager>();
            if (interviewManager == null)
            {
                interviewManager = FindObjectOfType<InterviewSessionManager>();
            }
        }

        if (interviewManager == null)
        {
            Debug.LogError("❌ [EndInterviewButton] CRITICAL: Could not find InterviewSessionManager in the scene. Please assign it manually in the Inspector.");
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        Debug.Log("🖱️ [EndInterviewButton] Button Clicked.");

        if (interviewManager == null)
        {
            Debug.LogError("❌ [EndInterviewButton] No InterviewSessionManager reference found. Trying to find it again...");
            interviewManager = FindFirstObjectByType<InterviewSessionManager>();
        }

        if (interviewManager != null)
        {
            interviewManager.EndInterview();
        }
        else
        {
            Debug.LogError("❌ [EndInterviewButton] Still cannot find InterviewSessionManager. Check if it exists in the scene.");
        }
    }
}
