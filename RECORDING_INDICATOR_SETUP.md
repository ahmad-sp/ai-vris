# Recording Indicator Feature

## What Was Added ✅

A visual **Recording Indicator** that blinks when the user's voice is detected and being recorded.

---

## How It Works

1. **System Monitors VAD**
   - The system checks `vad.IsCurrentlyRecordingSegment()` every frame.
   - This returns TRUE only when:
     - Voice volume > Threshold
     - AND recording has officially started (not just brief noise)

2. **Blinking Logic**
   - **When Recording (Speaking)**:
     - The indicator toggles on/off every 0.5 seconds.
     - Creates a blinking effect (Process: Visible → Invisible → Visible...).
   
   - **When Silent**:
     - The indicator is hidden (`SetActive(false)`).
     - Timer resets.

---

## Setup Instructions (Unity)

1. **Create the Indicator UI**
   - In your Unity Canvas (inside `InterviewSessionManager` or relevant panel).
   - Create a **red circle image** or "REC" text.
   - Name it `RecordingIndicator` (or similar).
   - Ideally, place it near the question text or a prominent corner.
   - Set it to **inactive** by default (uncheck the box in Inspector).

2. **Link to Script**
   - Select the `InterviewSessionManager` GameObject.
   - Find the **"UI References"** section.
   - Drag your new `RecordingIndicator` GameObject into the empty `Recording Indicator` slot.

---

## Code Changes

### `InterviewSessionManager.cs`

Added `Update()` loop:

```csharp
private void Update()
{
    if (recordingIndicator != null && vad != null)
    {
        bool isRecording = vad.IsCurrentlyRecordingSegment();

        if (isRecording)
        {
            // Blink every 0.5s
            indicatorBlinkTimer += Time.deltaTime;
            if (indicatorBlinkTimer >= 0.5f)
            {
                indicatorBlinkTimer = 0f;
                isIndicatorVisible = !isIndicatorVisible;
                recordingIndicator.SetActive(isIndicatorVisible);
            }
        }
        else
        {
            // Hide when not recording
            recordingIndicator.SetActive(false);
            isIndicatorVisible = false;
        }
    }
}
```

---

## Testing

1. **Play the Scene**.
2. **Start Interview**.
3. **Stay Silent**: Indicator should be hidden.
4. **Speak**: Indicator should appear and blink red.
5. **Stop Speaking**: Indicator should disappear.

---

**Status**: ✅ **CODE IMPLEMENTED**

**Next Step**: Open Unity and assign the UI GameObject in the Inspector!
