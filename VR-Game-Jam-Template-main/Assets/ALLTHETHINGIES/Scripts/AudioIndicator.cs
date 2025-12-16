using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple script to toggle a UI element or change its color based on voice activity.
/// Attach this to a GameObject with an Image component, or reference a target Graphic.
/// </summary>
public class AudioIndicator : MonoBehaviour
{
    [Header("References")]
    public Graphic targetGraphic; // Assign the Image or Text to color (e.g., Recording Dot)
    
    [Header("Settings")]
    public Color activeColor = Color.red;
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Semi-transparent gray
    public bool enableBlinking = true;
    public float blinkSpeed = 4f; // Blinks per second

    private bool isSpeaking = false;
    private Coroutine blinkCoroutine;

    void Start()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();
        
        // Initialize state
        UpdateVisuals(false);
    }

    /// <summary>
    /// Call this method from VADVoiceRecorder.onSpeechStatusChanged
    /// </summary>
    /// <param name="speaking">True if speaking, false otherwise</param>
    public void OnSpeechStatusChanged(bool speaking)
    {
        isSpeaking = speaking;
        
        if (isSpeaking)
        {
            if (enableBlinking)
            {
                if (blinkCoroutine == null)
                    blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
            else
            {
                UpdateVisuals(true);
            }
        }
        else
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            UpdateVisuals(false);
        }
    }

    private void UpdateVisuals(bool active)
    {
        if (targetGraphic != null)
        {
            targetGraphic.color = active ? activeColor : inactiveColor;
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (isSpeaking)
        {
            // Fade out
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * blinkSpeed;
                if (targetGraphic != null)
                    targetGraphic.color = Color.Lerp(activeColor, inactiveColor, t);
                yield return null;
            }
            
            // Fade in
            t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * blinkSpeed;
                if (targetGraphic != null)
                    targetGraphic.color = Color.Lerp(inactiveColor, activeColor, t);
                yield return null;
            }
        }
    }
}
