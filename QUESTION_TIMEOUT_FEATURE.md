# Question Timeout and Repeat Feature

## Overview
This feature implements automatic question repetition and skipping when no voice input is received from the candidate during an interview.

## How It Works

### Flow Diagram
```
Question Asked → Audio Played → VAD Listening Started → Timeout Monitor Started
                                                              ↓
                                                    [Wait for Answer]
                                                              ↓
                                        ┌─────────────────────┴─────────────────────┐
                                        ↓                                           ↓
                                  Answer Received                          Timeout Reached
                                        ↓                                           ↓
                              Cancel Timeout                            Repeat Question
                              Process Answer                            Play Audio Again
                              Next Question                             Restart VAD
                                                                                ↓
                                                                    [Wait for Answer Again]
                                                                                ↓
                                                        ┌───────────────────────┴───────────────────────┐
                                                        ↓                                               ↓
                                                  Answer Received                              Timeout Reached Again
                                                        ↓                                               ↓
                                              Cancel Timeout                                  Skip Question
                                              Process Answer                                  Submit Empty Answer (0 marks)
                                              Next Question                                   Next Question
```

### Detailed Behavior

1. **Initial Question Phase**
   - Question text is displayed
   - Audio is played
   - VAD (Voice Activity Detection) starts listening
   - Timeout monitor starts counting down

2. **First Timeout (No Answer Received)**
   - After `questionTimeoutSeconds` (default: 30s)
   - Question is repeated with "[REPEATED]" prefix
   - Audio is played again
   - VAD restarts listening
   - Second timeout monitor starts

3. **Second Timeout (Still No Answer)**
   - After `repeatTimeoutSeconds` (default: 30s)
   - Question is skipped
   - Empty answer is submitted to backend (awarded 0 marks)
   - Interview proceeds to next question

4. **Answer Received (Any Time)**
   - Timeout is immediately canceled
   - Answer is processed normally
   - Interview proceeds to next question

## Configuration

### Inspector Settings

The following settings are available in the Unity Inspector on the `InterviewSessionManager` component:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `questionTimeoutSeconds` | float | 30.0 | Time in seconds to wait before repeating the question |
| `repeatTimeoutSeconds` | float | 30.0 | Time in seconds to wait after repeat before skipping |
| `enableQuestionTimeout` | bool | true | Enable/disable the entire timeout feature |

### How to Configure

1. Select the GameObject with `InterviewSessionManager` component
2. In the Inspector, find the "Question Timeout Settings" section
3. Adjust the values as needed:
   - **Shorter timeouts** (e.g., 15s): For faster-paced interviews
   - **Longer timeouts** (e.g., 60s): For more thoughtful responses
   - **Disable feature**: Uncheck `enableQuestionTimeout`

## Implementation Details

### Key Components

#### 1. Timeout Tracking Variables
```csharp
private Coroutine questionTimeoutCoroutine;  // Active timeout coroutine
private bool hasReceivedAnswer;              // Answer received flag
private string currentQuestion;              // Current question text
private string currentAudioUrl;              // Current audio URL
private bool isQuestionRepeated;             // Repeat flag
```

#### 2. Core Methods

**`QuestionTimeoutMonitor()`**
- Main coroutine that handles the timeout logic
- Waits for initial timeout
- Repeats question if needed
- Waits for repeat timeout
- Skips question if still no answer

**`StartQuestionTimeout()`**
- Cancels any existing timeout
- Starts new timeout monitor if enabled

**`CancelQuestionTimeout()`**
- Stops the active timeout coroutine
- Called when answer is received

#### 3. Integration Points

The timeout system is integrated at these key points:

1. **HandleInitialPrompt()** - First question of interview
2. **FetchCurrentQuestion()** - When resuming/loading interview
3. **PostAnswerAndHandleNext()** - After each answer, for next question
4. **OnTranscriptReady()** - Cancels timeout when answer received
5. **OnDestroy()** - Cleanup when component is destroyed

### Debug Logging

The feature includes comprehensive debug logging with `[Timeout]` prefix:

- Timeout start: `"[Timeout] Starting timeout monitor. Initial timeout: 30s"`
- Countdown updates: `"[Timeout] Waiting for answer... 20s remaining"` (every 10s)
- Answer received: `"[Timeout] Answer received, canceling timeout"`
- Repeat triggered: `"[Timeout] No answer received after 30s. Repeating question..."`
- Skip triggered: `"[Timeout] No answer received after repeat. Skipping question with 0 marks..."`

## Backend Integration

### Empty Answer Handling

When a question is skipped, an empty string `""` is sent as the answer to the backend:

```csharp
yield return StartCoroutine(PostAnswerAndHandleNext(""));
```

**Backend Requirements:**
- The Django backend should handle empty answers appropriately
- Empty answers should be awarded 0 marks
- The interview should continue to the next question normally

### API Endpoint
The feature uses the existing interview endpoint:
- **URL**: `{backendBaseUrl}/api/interview/`
- **Method**: POST
- **Payload**: `{ "session_id": <id>, "answer": "" }`

## Testing Recommendations

### Test Scenarios

1. **Normal Flow**
   - Ask question
   - Provide answer within timeout
   - Verify timeout is canceled
   - Verify next question appears

2. **Repeat Flow**
   - Ask question
   - Wait for first timeout
   - Verify question repeats with "[REPEATED]" prefix
   - Verify audio plays again
   - Provide answer
   - Verify next question appears

3. **Skip Flow**
   - Ask question
   - Wait for first timeout (question repeats)
   - Wait for second timeout
   - Verify question is skipped
   - Verify empty answer is sent
   - Verify next question appears

4. **Disabled Feature**
   - Disable `enableQuestionTimeout`
   - Verify no timeout occurs
   - Verify interview waits indefinitely

### Test Configuration

For testing, you may want to use shorter timeouts:
- `questionTimeoutSeconds`: 10
- `repeatTimeoutSeconds`: 10

This allows faster testing cycles.

## Troubleshooting

### Issue: Timeout not starting
**Check:**
- `enableQuestionTimeout` is checked in Inspector
- VAD is properly configured and listening
- No errors in console

### Issue: Timeout not canceling when answer given
**Check:**
- `OnTranscriptReady()` is being called
- `hasReceivedAnswer` flag is being set
- No errors in VAD/STT pipeline

### Issue: Question not repeating
**Check:**
- `currentQuestion` and `currentAudioUrl` are being stored
- Audio source is properly configured
- Network connection for audio download

### Issue: Empty answer not skipping question
**Check:**
- Backend accepts empty answers
- Backend returns next question properly
- Network connection is stable

## Future Enhancements

Potential improvements for future versions:

1. **Visual Countdown**: Display remaining time to candidate
2. **Audio Cue**: Play a sound before repeating
3. **Configurable Repeat Count**: Allow multiple repeats before skip
4. **Per-Question Timeouts**: Different timeouts for different question types
5. **Pause/Resume**: Allow candidate to pause the timeout
6. **Analytics**: Track timeout/skip rates for interview optimization

## Code Modifications Summary

### Files Modified
- `InterviewSessionManager.cs`

### Lines Added
- Configuration fields: ~10 lines
- Timeout coroutine: ~130 lines
- Integration points: ~20 lines
- **Total**: ~160 lines

### Backward Compatibility
- Feature is enabled by default but can be disabled
- No breaking changes to existing functionality
- Works with existing backend API

## Version History

### v1.0 (Current)
- Initial implementation
- Basic timeout and repeat functionality
- Configurable timeout durations
- Debug logging
- Integration with existing interview flow
