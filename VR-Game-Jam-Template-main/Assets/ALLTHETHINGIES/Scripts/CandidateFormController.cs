using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class CandidateFormController : MonoBehaviour
{
    [Header("UI")]
    public GameObject candidateFormPanel; // the whole panel root (set inactive by default)
    public TMP_InputField nameInput;
    public TMP_InputField roleInput;
    public TextMeshProUGUI validationText;
    public Button submitButton;
    public Button cancelButton;

    [Header("Scene to load")]
    public string startSceneName = "InterviewRoom"; // change to your scene name

    // PlayerPrefs keys
    const string KEY_CANDIDATE_NAME = "candidate_name";
    const string KEY_CANDIDATE_ROLE = "candidate_role";

    void Start()
    {
        if (candidateFormPanel != null) candidateFormPanel.SetActive(false);

        // safety hookup
        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmit);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CloseForm);
        }

        if (validationText != null) validationText.text = "";
    }

    public void OpenForm()
    {
        if (candidateFormPanel == null)
        {
            Debug.LogWarning("[CandidateFormController] candidateFormPanel not assigned.");
            return;
        }
        // optionally prefill from PlayerPrefs
        nameInput.SetTextWithoutNotify(PlayerPrefs.GetString(KEY_CANDIDATE_NAME, ""));
        roleInput.SetTextWithoutNotify(PlayerPrefs.GetString(KEY_CANDIDATE_ROLE, ""));
        validationText.text = "";
        candidateFormPanel.SetActive(true);
        // optionally focus input (not always available on all targets)
        nameInput.Select();
    }

    public void CloseForm()
    {
        if (candidateFormPanel != null) candidateFormPanel.SetActive(false);
    }

    public void OnSubmit()
    {
        string name = nameInput != null ? nameInput.text.Trim() : "";
        string role = roleInput != null ? roleInput.text.Trim() : "";

        // Basic validation
        if (string.IsNullOrEmpty(name))
        {
            ShowValidation("Please enter your name.");
            return;
        }
        if (string.IsNullOrEmpty(role))
        {
            ShowValidation("Please enter the role.");
            return;
        }

        // Save choices so other scenes can read them (PlayerPrefs is simplest)
        PlayerPrefs.SetString(KEY_CANDIDATE_NAME, name);
        PlayerPrefs.SetString(KEY_CANDIDATE_ROLE, role);
        PlayerPrefs.Save();

        // Optionally close the form immediately and then load scene
        candidateFormPanel.SetActive(false);

        // Load the interview/start scene
        if (!string.IsNullOrEmpty(startSceneName))
        {
            SceneManager.LoadScene(startSceneName);
        }
        else
        {
            Debug.LogWarning("[CandidateFormController] startSceneName not set.");
        }
    }

    void ShowValidation(string msg)
    {
        if (validationText != null)
        {
            validationText.text = msg;
        }
        else
        {
            Debug.Log(msg);
        }
    }

    // Optional helper to read values from other scenes:
    public static string GetSavedName() => PlayerPrefs.GetString(KEY_CANDIDATE_NAME, "");
    public static string GetSavedRole() => PlayerPrefs.GetString(KEY_CANDIDATE_ROLE, "");
}
