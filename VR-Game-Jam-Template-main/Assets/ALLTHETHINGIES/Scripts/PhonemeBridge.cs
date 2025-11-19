using System.Collections;
using UnityEngine;

public class PhonemeBridge : MonoBehaviour
{
    [Header("References")]
    public PhenomesOutput phenomes; // Assign the PhenomesOutput used by InterviewerController
    public InterviewerController interviewerController; // Optional: toggle Talking during playback

    private Coroutine playbackRoutine;

    // Hook this to CandidateInfoForm.onQuestionReceived
    public void ReceiveText(string text)
    {
        if (phenomes == null)
        {
            Debug.LogWarning("PhonemeBridge: PhenomesOutput not assigned.");
            return;
        }

        phenomes.sentence = text ?? string.Empty;

        if (playbackRoutine != null)
            StopCoroutine(playbackRoutine);

        playbackRoutine = StartCoroutine(PlayPhonemes());
    }

    private IEnumerator PlayPhonemes()
    {
        if (interviewerController != null)
            interviewerController.Talking = true;

        yield return StartCoroutine(phenomes.Playback());

        if (interviewerController != null)
            interviewerController.Talking = false;

        playbackRoutine = null;
    }
}
