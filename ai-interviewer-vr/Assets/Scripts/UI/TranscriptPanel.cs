using UnityEngine;
using UnityEngine.UI;

public class TranscriptPanel : MonoBehaviour
{
    public Text transcriptText; // UI Text component to display the transcript
    private string conversationTranscript = ""; // Stores the conversation transcript

    // Method to add a new line to the transcript
    public void AddToTranscript(string newLine)
    {
        conversationTranscript += newLine + "\n"; // Append new line to the transcript
        UpdateTranscriptDisplay(); // Update the UI display
    }

    // Method to clear the transcript
    public void ClearTranscript()
    {
        conversationTranscript = ""; // Reset the transcript
        UpdateTranscriptDisplay(); // Update the UI display
    }

    // Method to update the transcript display in the UI
    private void UpdateTranscriptDisplay()
    {
        transcriptText.text = conversationTranscript; // Set the UI text to the current transcript
    }
}