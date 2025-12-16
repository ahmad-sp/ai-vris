# Voice-Aware Timeout System

## Issue Fixed ✅

**Problem**: The timeout was counting down even when the user was actively speaking, which meant:
- ❌ Long answers would trigger timeout mid-speech
- ❌ Users felt rushed and couldn't give detailed answers
- ❌ Timeout was unfair - it should only apply to SILENCE, not active speech

**Solution**: The timeout now **PAUSES** when voice activity is detected and only counts **SILENCE TIME**.

---

## How It Works Now

### Old Behavior (Problematic)
```
Question asked at 0s
User starts speaking at 10s
User still speaking at 30s
❌ TIMEOUT! (Even though user is actively answering)
Question repeats while user is mid-sentence
```

### New Behavior (Fixed)
```
Question asked at 0s
Silence: 0s → 10s (10 seconds of silence counted)
User starts speaking at 10s
[TIMEOUT PAUSED - voice detected!]
User speaks for 40 seconds (timeout NOT counting)
User stops speaking at 50s
Silence: 50s → 80s (30 seconds of silence counted)
✅ TIMEOUT at 80s (after 30 seconds of SILENCE, not total time)
```

---

## Key Changes

### 1. Silence-Based Timeout

**Before**:
```csharp
float elapsed = 0f;
while (elapsed < questionTimeoutSeconds)
{
    yield return new WaitForSeconds(1f);
    elapsed += 1f;  // Always counting, even during speech
}
```

**After**:
```csharp
float silenceTime = 0f;  // Only count SILENCE
while (silenceTime < questionTimeoutSeconds)
{
    yield return new WaitForSeconds(1f);
    
    bool isVoiceActive = vad.IsCurrentlyRecordingSegment();
    
    if (!isVoiceActive)
    {
        silenceTime += 1f;  // Only count when NO voice
    }
    else
    {
        silenceTime = 0f;  // Reset when voice detected
    }
}
```

### 2. Voice Detection Integration

The system now checks VAD (Voice Activity Detection) every second:
- **Voice Detected** → Timeout paused, silence counter reset to 0
- **No Voice** → Timeout counting, silence counter increments

### 3. Smart Reset

When voice is detected, the silence counter **resets to 0**:
- This means the user gets the FULL timeout period after they stop speaking
- Prevents timeout from triggering immediately after a long answer

---

## Detailed Behavior

### Scenario 1: Quick Answer
```
0s: Question asked
0-5s: Silence (5s counted)
5-10s: User speaks (timeout paused, counter reset to 0)
10-15s: Silence (5s counted)
15s: Answer submitted
✅ No timeout (only 5s of silence after speaking)
```

### Scenario 2: Long Answer
```
0s: Question asked
0-3s: Silence (3s counted)
3-60s: User gives detailed answer (timeout paused entire time)
60-65s: Silence (5s counted)
65s: Answer submitted
✅ No timeout (only 5s of silence after speaking)
```

### Scenario 3: No Answer
```
0s: Question asked
0-30s: Complete silence (30s counted)
30s: ⏰ TIMEOUT - Question repeated
30-60s: Still silence (30s counted)
60s: ⏰ TIMEOUT - Question skipped
✅ Timeout triggered correctly (no voice detected)
```

### Scenario 4: Interrupted Answer
```
0s: Question asked
0-5s: Silence (5s counted)
5-15s: User starts answering (timeout paused, counter reset)
15-20s: User pauses to think (5s silence counted)
20-40s: User continues (timeout paused, counter reset)
40-45s: Silence (5s counted)
45s: Answer submitted
✅ No timeout (only brief pauses counted)
```

### Scenario 5: Multiple Pauses
```
0s: Question asked
0-10s: Silence (10s counted)
10-20s: User speaks (timeout paused, counter reset)
20-25s: Pause (5s counted)
25-35s: User speaks more (timeout paused, counter reset)
35-40s: Pause (5s counted)
40-50s: User finishes (timeout paused, counter reset)
50-55s: Silence (5s counted)
55s: Answer submitted
✅ No timeout (never reached 30s of continuous silence)
```

---

## Configuration

### Timeout Settings (Unity Inspector)

The timeout values now represent **SILENCE TIME**, not total time:

| Setting | Default | Meaning |
|---------|---------|---------|
| `questionTimeoutSeconds` | 30s | 30 seconds of **SILENCE** before repeat |
| `repeatTimeoutSeconds` | 30s | 30 seconds of **SILENCE** after repeat before skip |

**Important**: These are SILENCE seconds, not total seconds!

### Recommended Settings

**For Detailed Answers**:
```
questionTimeoutSeconds: 20-30s
repeatTimeoutSeconds: 20-30s
```
- Allows brief pauses for thinking
- Won't timeout during long answers
- Triggers if genuinely no response

**For Quick Answers**:
```
questionTimeoutSeconds: 15s
repeatTimeoutSeconds: 15s
```
- Faster-paced interview
- Still won't interrupt active speech

**For Very Detailed/Technical Answers**:
```
questionTimeoutSeconds: 45s
repeatTimeoutSeconds: 45s
```
- More generous silence allowance
- Good for complex technical questions

---

## Debug Logging

### New Log Messages

**When Voice is Detected**:
```
[Timeout] Voice detected! Pausing timeout. (Had 10s of silence)
```
- Indicates timeout has paused
- Shows how much silence was accumulated before voice

**During Silence**:
```
[Timeout] No voice detected... 20s of silence remaining
```
- Shows countdown of SILENCE time
- Only appears when no voice is active

**After Repeat**:
```
[Timeout] Voice detected after repeat! Pausing timeout. (Had 5s of silence)
```
- Same logic applies after question is repeated

### Monitoring in Unity Console

Filter by `[Timeout]` to see:
1. When timeout starts
2. When voice is detected (timeout pauses)
3. Silence countdown (only during actual silence)
4. When timeout triggers (after X seconds of silence)

---

## Technical Implementation

### Files Modified
- ✅ `InterviewSessionManager.cs`

### Changes Made

**Location 1: Initial Timeout (Lines 426-474)**
```csharp
// OLD: Count all time
float elapsed = 0f;
while (elapsed < questionTimeoutSeconds)
{
    elapsed += 1f;  // Always counting
}

// NEW: Count only silence
float silenceTime = 0f;
while (silenceTime < questionTimeoutSeconds)
{
    bool isVoiceActive = vad.IsCurrentlyRecordingSegment();
    
    if (!isVoiceActive)
        silenceTime += 1f;  // Count silence
    else
        silenceTime = 0f;   // Reset when voice detected
}
```

**Location 2: Repeat Timeout (Lines 519-556)**
```csharp
// Same logic applied to repeat timeout
silenceTime = 0f;
while (silenceTime < repeatTimeoutSeconds)
{
    bool isVoiceActive = vad.IsCurrentlyRecordingSegment();
    
    if (!isVoiceActive)
        silenceTime += 1f;
    else
        silenceTime = 0f;
}
```

### Integration with VAD

The system uses `vad.IsCurrentlyRecordingSegment()` to check voice activity:
- Returns `true` when VAD detects voice and is recording
- Returns `false` when no voice is detected
- Checked every 1 second during timeout monitoring

---

## Benefits

### 1. Fair Timeout
- ✅ Only triggers when there's genuinely no response
- ✅ Doesn't interrupt users mid-answer
- ✅ Allows for detailed, thoughtful responses

### 2. Better User Experience
- ✅ Users don't feel rushed
- ✅ Can give complete answers without fear of timeout
- ✅ Natural pauses for thinking are allowed

### 3. Accurate Detection
- ✅ Distinguishes between "no answer" and "long answer"
- ✅ Timeout only for actual silence/non-response
- ✅ Prevents false positives

### 4. Flexible Configuration
- ✅ Timeout values are now more predictable
- ✅ 30s means "30s of silence", not "30s total"
- ✅ Easy to adjust based on interview type

---

## Examples

### Example 1: Technical Question with Long Answer

**Question**: "Explain how you would design a scalable microservices architecture"

**Timeline**:
```
0s: Question asked
0-5s: Candidate thinks (5s silence)
5-120s: Candidate gives detailed 2-minute answer (timeout paused entire time)
120-125s: Brief pause (5s silence)
125s: Answer submitted
```

**Result**: ✅ No timeout (only 10s total silence)

### Example 2: Simple Question, No Answer

**Question**: "What's your name?"

**Timeline**:
```
0s: Question asked
0-30s: Complete silence (30s counted)
30s: ⏰ Question repeated
30-60s: Still silence (30s counted)
60s: ⏰ Question skipped
```

**Result**: ✅ Timeout triggered correctly

### Example 3: Answer with Multiple Pauses

**Question**: "Tell me about your experience with Python"

**Timeline**:
```
0s: Question asked
0-3s: Silence (3s)
3-20s: "I've been using Python for 5 years..." (paused)
20-25s: Thinking pause (5s)
25-45s: "I've worked on projects like..." (paused)
45-50s: Another pause (5s)
50-70s: "My favorite libraries are..." (paused)
70-73s: Final silence (3s)
73s: Answer submitted
```

**Result**: ✅ No timeout (only 16s total silence, never 30s continuous)

---

## Comparison: Before vs After

### Before (Time-Based)
```
Setting: 30s timeout
User speaks for 40s
Result: ❌ Timeout at 30s (mid-answer!)
Problem: Unfair, interrupts long answers
```

### After (Silence-Based)
```
Setting: 30s silence timeout
User speaks for 40s
Result: ✅ No timeout (no 30s of silence)
Benefit: Fair, only triggers on actual non-response
```

---

## Testing Guide

### Test 1: Long Answer
1. Start interview
2. Wait for question
3. Give a very long, detailed answer (60+ seconds)
4. **Expected**: No timeout, answer accepted normally
5. **Check logs**: Should see "Voice detected! Pausing timeout"

### Test 2: No Answer
1. Start interview
2. Wait for question
3. Don't say anything for 30+ seconds
4. **Expected**: Question repeats at 30s
5. **Check logs**: Should see silence countdown

### Test 3: Answer with Pauses
1. Start interview
2. Wait for question
3. Answer with multiple 5-10s pauses for thinking
4. **Expected**: No timeout (pauses reset counter)
5. **Check logs**: Should see voice detected messages

### Test 4: Partial Answer Then Silence
1. Start interview
2. Wait for question
3. Start answering, then stop mid-sentence
4. Wait 30s in silence
5. **Expected**: Question repeats after 30s of silence
6. **Check logs**: Should see silence countdown after you stop

---

## Troubleshooting

### Timeout Still Triggering During Speech

**Check**:
1. Is VAD properly detecting voice?
2. Check `vadThreshold` setting (might be too high)
3. Look for "Voice detected!" messages in logs
4. Verify microphone is working

**Debug**:
```
Look for in Unity Console:
[VAD] Voice detected (rms=X.XXXX). Starting segment capture.
[Timeout] Voice detected! Pausing timeout.
```

### Timeout Never Triggering

**Check**:
1. Is VAD too sensitive? (detecting background noise as voice)
2. Check `vadThreshold` setting (might be too low)
3. Verify timeout is enabled in Inspector
4. Check timeout values are > 0

**Debug**:
```
Look for in Unity Console:
[Timeout] No voice detected... Xs of silence remaining
```

### Timeout Values Seem Wrong

**Remember**:
- Values are now **SILENCE TIME**, not total time
- 30s timeout = 30s of continuous silence needed
- Voice activity resets the counter
- This is intentional and correct!

---

## Summary

### What Changed
- ❌ **Before**: Timeout counted all time (unfair to long answers)
- ✅ **After**: Timeout only counts silence time (fair and accurate)

### How It Works
1. Timeout monitors for voice activity every second
2. When voice detected → Pause timeout, reset silence counter
3. When no voice → Count silence time
4. Trigger timeout only after X seconds of **continuous silence**

### Benefits
- Fair to candidates giving detailed answers
- Only triggers on genuine non-response
- Allows natural pauses for thinking
- More accurate detection of "no answer"

---

**Status**: ✅ **IMPLEMENTED AND READY TO USE**

**Last Updated**: December 16, 2025  
**File Modified**: `InterviewSessionManager.cs`  
**Lines Changed**: ~60 lines  
**Testing**: Ready for testing

---

## Quick Reference

| Scenario | Old Behavior | New Behavior |
|----------|-------------|--------------|
| 60s answer | ❌ Timeout at 30s | ✅ No timeout |
| No answer | ✅ Timeout at 30s | ✅ Timeout at 30s |
| Answer with pauses | ❌ Might timeout | ✅ No timeout |
| Thinking pause | ❌ Counts toward timeout | ✅ Resets after voice |

**Key Takeaway**: Timeout now measures **SILENCE**, not **TIME**. This is much fairer!
