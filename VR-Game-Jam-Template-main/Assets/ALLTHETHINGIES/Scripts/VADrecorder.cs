using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System;
using UnityEngine.Events;

/// <summary>
/// Voice-activated recorder with silence detection (VAD), WAV saving (SavWav required),
/// and automatic upload to Django STT endpoint. Also can forward transcript to interview endpoint.
/// </summary>
public class VADVoiceRecorder : MonoBehaviour
{
    [Header("Recording settings")]
    public int sampleRate = 16000;
    public int maxRecordSeconds = 120;          // max cap for a single recorded answer
    public float vadThreshold = 0.02f;          // amplitude threshold to detect voice (tweak for mic)
    public float silenceSecondsToStop = 4.0f;   // seconds of continuous silence to auto-stop
    public float preSpeechBuffer = 0.3f;        // seconds to include before voice detection (small padding)
    public bool autoUpload = true;              // upload automatically after save
    public bool sendTranscriptToInterview = false;
    public bool autoStartOnAwake = false;

    [Header("Events")]
    public UnityEvent<string> onTranscript; // fired when transcript is parsed

    [Header("API endpoints (use your machine IP)")]
    public string sttUploadUrl = "http://192.168.133.1:8000/api/audio-to-text/"; // change to your host/IP
    public string interviewStepUrl = "http://192.168.133.1:8000/api/interview/"; // optional: forward transcript

    // internal
    private AudioClip recordingClip;
    private string savePath;
    private bool isListening = false;
    private int microphoneStartPosition = 0;
    private float lastVoiceTime = 0f;
    private int channels = 1;

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "candidate_audio.wav");
        Debug.Log("[VAD] Save path: " + savePath);
        if (autoStartOnAwake)
            StartListening();
    }

    void OnDestroy()
    {
        StopListening();
    }

    // Start continuous microphone capture to buffer audio
    public void StartListening()
    {
        if (isListening) return;
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[VAD] No microphone devices found.");
            return;
        }

        // Start a long looping clip to read from (we will extract segments)
        recordingClip = Microphone.Start(null, true, maxRecordSeconds, sampleRate);
        while (Microphone.GetPosition(null) <= 0) { } // wait until started
        microphoneStartPosition = Microphone.GetPosition(null);
        isListening = true;
        lastVoiceTime = Time.time;
        Debug.Log("[VAD] Listening started. sampleRate=" + sampleRate);
    }

    public void StopListening()
    {
        if (!isListening) return;
        Microphone.End(null);
        isListening = false;
        recordingClip = null;
        Debug.Log("[VAD] Listening stopped.");
    }

    void Update()
    {
        if (!isListening || recordingClip == null) return;

        int pos = Microphone.GetPosition(null);
        if (pos < 0) return;

        // read a short window from the mic buffer for VAD
        int sampleWindow = Mathf.Min(1024, recordingClip.samples);
        float[] samples = new float[sampleWindow * channels];

        int start = pos - sampleWindow;
        if (start < 0) start = 0;

        recordingClip.GetData(samples, start);

        // compute RMS / amplitude
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        float rms = Mathf.Sqrt(sum / samples.Length);

        bool isSpeaking = rms > vadThreshold;

        if (isSpeaking)
        {
            // mark last voice time
            lastVoiceTime = Time.time;

            // if we haven't started a real recorded segment, mark start position
            if (!IsCurrentlyRecordingSegment())
            {
                Debug.Log("[VAD] Voice detected (rms=" + rms.ToString("F4") + "). Starting segment capture.");
                // store the start index to include preSpeechBuffer
                segmentStartPosition = Mathf.Max(0, pos - Mathf.RoundToInt(preSpeechBuffer * sampleRate));
                segmentEndPosition = -1; // still active
            }
        }
        else
        {
            // if we've been silent for long enough and we have an active segment => finalize
            if (IsCurrentlyRecordingSegment() && (Time.time - lastVoiceTime) >= silenceSecondsToStop)
            {
                segmentEndPosition = pos;
                Debug.Log("[VAD] Silence detected for " + silenceSecondsToStop + "s - finalizing segment.");
                StartCoroutine(ExtractSaveUploadSegment(segmentStartPosition, segmentEndPosition));
                // reset segment markers for next phrase
                segmentStartPosition = -1;
                segmentEndPosition = -1;
            }
        }
    }

    // segment tracking indices in samples
    private int segmentStartPosition = -1;
    private int segmentEndPosition = -1;

    bool IsCurrentlyRecordingSegment()
    {
        return segmentStartPosition >= 0 && (segmentEndPosition <= 0 || segmentEndPosition > segmentStartPosition);
    }

    IEnumerator ExtractSaveUploadSegment(int startPos, int endPos)
    {
        // Wait a frame to ensure microphone buffer is stable
        yield return null;

        if (recordingClip == null)
        {
            Debug.LogWarning("[VAD] No clip to extract from.");
            yield break;
        }

        int micPos = Microphone.GetPosition(null);
        int totalSamples = recordingClip.samples;
        int usedEnd = endPos > 0 ? endPos : micPos;
        if (usedEnd < startPos) usedEnd = micPos;

        int samplesLength = usedEnd - startPos;
        if (samplesLength <= 0)
        {
            Debug.LogWarning("[VAD] Zero-length segment. skipping.");
            yield break;
        }

        // limit by maxRecordSeconds just in case
        int maxSamples = maxRecordSeconds * sampleRate;
        if (samplesLength > maxSamples) samplesLength = maxSamples;

        float[] data = new float[samplesLength * channels];
        recordingClip.GetData(data, startPos);

        // create new clip and save WAV
        AudioClip clipSegment = AudioClip.Create("segment", samplesLength, channels, sampleRate, false);
        clipSegment.SetData(data, 0);

        // Save WAV file (SavWav.Save expects full path)
        bool saved = SavWav.Save(savePath, clipSegment);
        if (!saved)
        {
            Debug.LogError("[VAD] Failed to save WAV.");
            yield break;
        }

        Debug.Log("[VAD] Saved segment WAV: " + savePath);

        // Upload to Django
        if (autoUpload)
        {
            yield return StartCoroutine(UploadFileToServer(savePath));
        }
    }

    IEnumerator UploadFileToServer(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("[VAD] File not found for upload: " + path);
            yield break;
        }

        byte[] fileData = File.ReadAllBytes(path);

        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", fileData, Path.GetFileName(path), "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(sttUploadUrl, form))
        {
            www.timeout = 60;
            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (www.result != UnityWebRequest.Result.Success)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Debug.LogError("[VAD] Upload failed: " + www.error + "  Response: " + www.downloadHandler.text);
                yield break;
            }

            string recv = www.downloadHandler.text;
            Debug.Log("[VAD] STT response: " + recv);

            // Expecting JSON like { "transcript": "..." }
            string transcript = ParseTranscriptFromJson(recv);
            if (!string.IsNullOrEmpty(transcript))
            {
                Debug.Log("[VAD] Transcript: " + transcript);
                if (onTranscript != null)
                {
                    onTranscript.Invoke(transcript);
                }

                if (sendTranscriptToInterview)
                {
                    // Forward to interview step API
                    yield return StartCoroutine(PostTranscriptToInterview(transcript));
                }
            }
        }
    }

    string ParseTranscriptFromJson(string json)
    {
        try
        {
            // simple parse for {"transcript":"..."} - avoid full JSON lib
            int idx = json.IndexOf("transcript");
            if (idx >= 0)
            {
                int colon = json.IndexOf(':', idx);
                int firstQuote = json.IndexOf('"', colon + 1);
                int secondQuote = json.IndexOf('"', firstQuote + 1);
                string value = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                return UnityWebRequest.UnEscapeURL(value);
            }

            // fallback: attempt to strip braces
            return json;
        }
        catch (Exception)
        {
            return json;
        }
    }

    IEnumerator PostTranscriptToInterview(string transcript)
    {
        // Use JSON to include session_id
        int sessionId = PlayerPrefs.GetInt("session_id", 0);
        var payload = new InterviewAnswer { session_id = sessionId, answer = transcript };
        var json = JsonUtility.ToJson(payload);

        var www = new UnityWebRequest(interviewStepUrl, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        {
            www.timeout = 60;
            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (www.result != UnityWebRequest.Result.Success)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Debug.LogError("[VAD] Interview post failed: " + www.error + "  Response: " + www.downloadHandler.text);
                yield break;
            }

            Debug.Log("[VAD] Interview response: " + www.downloadHandler.text);

            // parse and play audio_url returned (optional)
            // Example: response JSON contains "audio_url": "http://.../reply_x.mp3"
            string resp = www.downloadHandler.text;
            string audioUrl = ExtractJsonField(resp, "audio_url");
            string nextQuestion = ExtractJsonField(resp, "question");
            if (!string.IsNullOrEmpty(nextQuestion))
            {
                // also broadcast next question via transcript event for manager reuse if desired
                // (manager can have a separate event for question; kept minimal here)
            }
            if (!string.IsNullOrEmpty(audioUrl))
            {
                Debug.Log("[VAD] Received audio URL: " + audioUrl);
                // TODO: play the audio URL using UnityWebRequestMultimedia.GetAudioClip
                StartCoroutine(PlayAudioFromUrl(audioUrl));
            }
        }
    }

    [Serializable]
    public class InterviewAnswer
    {
        public int session_id;
        public string answer;
    }

    string ExtractJsonField(string json, string fieldName)
    {
        try
        {
            int idx = json.IndexOf(fieldName);
            if (idx >= 0)
            {
                int colon = json.IndexOf(':', idx);
                int firstQuote = json.IndexOf('"', colon + 1);
                int secondQuote = json.IndexOf('"', firstQuote + 1);
                return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            }
        }
        catch { }
        return null;
    }

    IEnumerator PlayAudioFromUrl(string url)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (www.result != UnityWebRequest.Result.Success)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Debug.LogError("[VAD] Audio download error: " + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            AudioSource src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.Play();
            yield return new WaitForSeconds(clip.length);
        }
    }
}
