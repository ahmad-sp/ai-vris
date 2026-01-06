import io
import os
from google.cloud import speech

def transcribe_audio(file_path):
    """
    Transcribes a local audio file using Google Cloud Speech-to-Text.
    Supported formats: wav, flac, mp3 (converted).
    """

    if not os.path.exists(file_path):
        raise FileNotFoundError(f"Audio file not found: {file_path}")

    # Resolve GOOGLE_APPLICATION_CREDENTIALS if it's a relative path.
    cred_path = os.getenv("GOOGLE_APPLICATION_CREDENTIALS")
    if cred_path and not os.path.isabs(cred_path) and not os.path.exists(cred_path):
        try:
            from django.conf import settings

            candidate = os.path.join(getattr(settings, "BASE_DIR", ""), cred_path)
            if os.path.exists(candidate):
                os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = candidate
        except Exception:
            pass

    try:
        client = speech.SpeechClient()
    except Exception as e:
        raise RuntimeError(f"Failed to initialize Google Speech client: {str(e)}")

    with io.open(file_path, "rb") as audio_file:
        content = audio_file.read()

    audio = speech.RecognitionAudio(content=content)

    ext = os.path.splitext(file_path)[1].lower()
    if ext == ".mp3":
        encoding = speech.RecognitionConfig.AudioEncoding.MP3
    elif ext == ".wav":
        encoding = speech.RecognitionConfig.AudioEncoding.LINEAR16
    else:
        encoding = speech.RecognitionConfig.AudioEncoding.ENCODING_UNSPECIFIED

    config = speech.RecognitionConfig(
        encoding=encoding,
        language_code="en-US",
        enable_automatic_punctuation=True,
        model="default",
    )

    response = client.recognize(config=config, audio=audio)

    # Combine all transcripts
    transcript = " ".join(result.alternatives[0].transcript for result in response.results)
    return transcript.strip() if transcript else ""
