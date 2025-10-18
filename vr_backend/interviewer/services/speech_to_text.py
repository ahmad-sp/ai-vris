import io
import os
from google.cloud import speech

# Initialize client once
client = speech.SpeechClient()

def transcribe_audio(file_path):
    """
    Transcribes a local audio file using Google Cloud Speech-to-Text.
    Supported formats: wav, flac, mp3 (converted).
    """

    if not os.path.exists(file_path):
        raise FileNotFoundError(f"Audio file not found: {file_path}")

    with io.open(file_path, "rb") as audio_file:
        content = audio_file.read()

    audio = speech.RecognitionAudio(content=content)
    config = speech.RecognitionConfig(
        encoding=speech.RecognitionConfig.AudioEncoding.LINEAR16,  # or MP3
        language_code="en-US",
        enable_automatic_punctuation=True,
        model="default",
    )

    response = client.recognize(config=config, audio=audio)

    # Combine all transcripts
    transcript = " ".join(result.alternatives[0].transcript for result in response.results)
    return transcript.strip() if transcript else ""
