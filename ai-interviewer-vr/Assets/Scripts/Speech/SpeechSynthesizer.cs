using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;

public class SpeechSynthesizer : MonoBehaviour
{
    private Dictionary<string, string> voiceClips;

    void Start()
    {
        voiceClips = new Dictionary<string, string>
        {
            { "welcome", "Welcome to the interview simulation." },
            { "question", "What is your greatest strength?" },
            { "feedback", "Thank you for your response. Here is your feedback." }
        };
    }

    public void Speak(string key)
    {
        if (voiceClips.ContainsKey(key))
        {
            string textToSpeak = voiceClips[key];
            // Implement text-to-speech functionality here
            Debug.Log("Speaking: " + textToSpeak);
        }
        else
        {
            Debug.LogWarning("No voice clip found for key: " + key);
        }
    }
}