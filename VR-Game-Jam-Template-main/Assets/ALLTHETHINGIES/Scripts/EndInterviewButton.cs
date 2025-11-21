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

        // Auto-discover InterviewSessionManager if not assigned in Inspector.
        if (interviewManager == null)
        {
            interviewManager = FindFirstObjectByType<InterviewSessionManager>();
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
        if (interviewManager == null)
        {
            Debug.LogError("[EndInterviewButton] No InterviewSessionManager reference set.");
            return;
        }

        interviewManager.EndInterview();
    }
}
