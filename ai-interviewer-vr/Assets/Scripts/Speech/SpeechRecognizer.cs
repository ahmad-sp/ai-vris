using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;
using System.Text;

public class SpeechRecognizer : MonoBehaviour
{
    private DictationRecognizer dictationRecognizer;
    private StringBuilder recognizedText;

    void Start()
    {
        recognizedText = new StringBuilder();
        dictationRecognizer = new DictationRecognizer();

        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;

        dictationRecognizer.Start();
    }

    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        recognizedText.Append(text + " ");
        // Process the recognized text (e.g., send to AI for analysis)
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        if (cause != DictationCompletionCause.Complete)
        {
            // Handle the error or incomplete dictation
        }
        // Optionally restart the dictation
        dictationRecognizer.Start();
    }

    private void OnDictationError(string error, int hresult)
    {
        // Handle the error
    }

    public string GetRecognizedText()
    {
        return recognizedText.ToString().Trim();
    }

    void OnDestroy()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Stop();
            dictationRecognizer.Dispose();
        }
    }
}