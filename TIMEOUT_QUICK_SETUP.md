# Quick Setup Guide: Question Timeout Feature

## What This Feature Does

✅ **Automatically repeats questions** if the candidate doesn't respond within a set time  
✅ **Skips questions with 0 marks** if still no response after the repeat  
✅ **Fully configurable** timeout durations  
✅ **Can be enabled/disabled** as needed

## Quick Setup (3 Steps)

### Step 1: Open Unity Inspector
1. In Unity, find the GameObject with the `InterviewSessionManager` component
2. Select it to view in the Inspector

### Step 2: Configure Timeout Settings
Look for the **"Question Timeout Settings"** section:

```
┌─────────────────────────────────────────────┐
│ Question Timeout Settings                   │
├─────────────────────────────────────────────┤
│ ☑ Enable Question Timeout                   │
│ Question Timeout Seconds:      30           │
│ Repeat Timeout Seconds:        30           │
└─────────────────────────────────────────────┘
```

**Recommended Settings:**
- **Production**: 30 seconds each (default)
- **Testing**: 10 seconds each (faster testing)
- **Patient Interview**: 60 seconds each (more thinking time)

### Step 3: Test It!
1. Start an interview
2. Wait without answering
3. After 30s (or your configured time), the question should repeat
4. Wait again without answering
5. After another 30s, the question should be skipped

## Default Behavior

| Time | Event |
|------|-------|
| 0s | Question asked, audio played |
| 30s | ⏰ **First timeout** - Question repeated |
| 60s | ⏰ **Second timeout** - Question skipped (0 marks) |

## How to Disable

Simply **uncheck** the "Enable Question Timeout" checkbox in the Inspector.

## Customization Examples

### Fast-Paced Interview
```
Question Timeout Seconds: 15
Repeat Timeout Seconds: 15
```
Total wait time: 30 seconds

### Thoughtful Interview
```
Question Timeout Seconds: 60
Repeat Timeout Seconds: 60
```
Total wait time: 120 seconds (2 minutes)

### Single Warning (No Repeat)
```
Question Timeout Seconds: 0.1
Repeat Timeout Seconds: 45
```
Question repeats almost immediately, then 45s to answer

## Visual Indicators

When a question is repeated, it will show with a prefix:
```
[REPEATED] Tell me about your experience with...
```

When a question is skipped:
```
Question skipped due to no response. Moving to next question...
```

## Debug Console Messages

Watch the Unity Console for these messages:

✅ **Timeout Started**: `[Timeout] Starting timeout monitor. Initial timeout: 30s`  
⏰ **Countdown**: `[Timeout] Waiting for answer... 20s remaining`  
🎤 **Answer Received**: `[Timeout] Answer received, canceling timeout`  
🔁 **Repeating**: `[Timeout] No answer received after 30s. Repeating question...`  
⏭️ **Skipping**: `[Timeout] No answer received after repeat. Skipping question with 0 marks...`

## Troubleshooting

### "Timeout not working"
- ✅ Check that "Enable Question Timeout" is checked
- ✅ Verify timeout values are greater than 0
- ✅ Check Unity Console for errors

### "Question repeats too fast/slow"
- ✅ Adjust "Question Timeout Seconds" value
- ✅ Remember: values are in seconds, not milliseconds

### "Question doesn't skip after repeat"
- ✅ Check "Repeat Timeout Seconds" is set
- ✅ Verify backend accepts empty answers
- ✅ Check network connection

## Backend Requirements

Your Django backend should:
1. Accept empty string answers: `{ "answer": "" }`
2. Award 0 marks for empty answers
3. Return the next question normally

## Need More Details?

See the full documentation: `QUESTION_TIMEOUT_FEATURE.md`

---

**Feature Status**: ✅ Implemented and Ready to Use  
**Last Updated**: December 16, 2025
