import os
import time
import requests
from dotenv import load_dotenv
from rest_framework.views import APIView
from rest_framework.response import Response
from rest_framework import status
from django.conf import settings
from django.http import HttpResponse

from .models import InterviewSession, InterviewResponse, ResumeUpload
from .services.llm_service import generate_interviewer_text
from .services.scoring_service import score_answer
from .services.flow_service import get_next_step, get_remaining, is_technical_role
from .services.report_service import generate_report
from .services.speech_to_text import transcribe_audio
from .services.resume_parser_service import process_resume_upload

load_dotenv()

OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY")
ELEVENLABS_API_KEY = os.getenv("ELEVENLABS_API_KEY")
VOICE_ID = os.getenv("ELEVENLABS_VOICE_ID")

DEFAULT_SECTION_FLOW = [
    "Greeting", "Introduction", "Resume Questions",
    "Technical", "Behavioral/Situational", "Wrap-Up"
]

MAX_QUESTIONS = {
    "Greeting": 2,
    "Introduction": 3,
    "Resume Questions": 3,
    "Technical": 5,
    "Behavioral/Situational": 3,
    "Wrap-Up": 1  # Only one closing message
}


def text_to_speech(text, request_obj=None):
    """Convert interviewer text to speech using ElevenLabs."""
    if not ELEVENLABS_API_KEY or not VOICE_ID:
        print("⚠️ Missing ElevenLabs API or Voice ID.")
        return None

    try:
        url = f"https://api.elevenlabs.io/v1/text-to-speech/{VOICE_ID}"
        headers = {"xi-api-key": ELEVENLABS_API_KEY, "Content-Type": "application/json"}
        payload = {"text": text, "voice_settings": {"stability": 0.6, "similarity_boost": 0.8}}

        response = requests.post(url, headers=headers, json=payload, timeout=40)
        response.raise_for_status()

        filename = f"reply_{int(time.time())}.mp3"
        filepath = settings.MEDIA_ROOT / filename
        with open(filepath, "wb") as f:
            f.write(response.content)

        if request_obj:
            return request_obj.build_absolute_uri(settings.MEDIA_URL + filename)
        return filename
    except Exception as e:
        print("🛑 ElevenLabs Error:", e)
        return None


class InterviewStep(APIView):
    """Main endpoint controlling each voice-driven interview step."""

    def post(self, request):
        candidate_name = request.data.get("candidate_name", "Anonimus")
        role = request.data.get("role")
        session_id = request.data.get("session_id")
        answer = request.data.get("answer")

        try:
            # 🟢 Load or create session
            if session_id:
                session = InterviewSession.objects.get(id=session_id)
            else:
                if not role:
                    return Response({"error": "Role is required."}, status=status.HTTP_400_BAD_REQUEST)
                if not is_technical_role(role):
                    return Response(
                        {"error": "Role not recognized as technical. Please use a valid technical/IT role."},
                        status=status.HTTP_400_BAD_REQUEST,
                    )

                session = InterviewSession.objects.create(
                    candidate_name=candidate_name, role=role, current_step="Greeting", completed=False
                )

            current_step = session.current_step
            asked_questions = session.responses.filter(step=current_step).count()
            max_questions = MAX_QUESTIONS.get(current_step, 3)

            # 💬 Save last answer (if any)
            if answer:
                last_response = session.responses.last()
                if last_response:
                    score, relevance = score_answer(last_response.question, answer)
                    last_response.answer = answer
                    last_response.score = score
                    last_response.relevance = relevance
                    last_response.save()

            # 🧱 Handle completion early
            if session.completed:
                return Response({
                    "session_id": session.id,
                    "step": "Completed",
                    "question": "Interview already completed.",
                    "audio_url": None,
                    "remaining_sections": 0,
                    "remaining_questions": 0,
                    "report_url": request.build_absolute_uri(f"/api/report/{session.id}/"),
                })

            # 🧠 Generate interviewer text dynamically
            resume_summary = None
            if current_step == "Resume Questions":
                try:
                    resume = session.resume
                    resume_summary = resume.summary
                except ResumeUpload.DoesNotExist:
                    resume_summary = None
            
            interviewer_text = generate_interviewer_text(session.role, current_step, answer, resume_summary)

            # 🚪 If Exit or Wrap-Up step, close interview gracefully
            if current_step in ["Wrap-Up", "Exit"]:
                interviewer_text = generate_interviewer_text(session.role, "Exit")
                session.completed = True
                session.save()
                audio_url = text_to_speech(interviewer_text, request_obj=request)

                return Response({
                    "session_id": session.id,
                    "step": "Exit",
                    "question": interviewer_text,
                    "audio_url": audio_url,
                    "remaining_sections": 0,
                    "remaining_questions": 0,
                    "report_url": request.build_absolute_uri(f"/api/report/{session.id}/"),
                })

            # 🗣️ Store interviewer question and convert to speech
            InterviewResponse.objects.create(session=session, step=current_step, question=interviewer_text)
            audio_url = text_to_speech(interviewer_text, request_obj=request)

            # 🔁 Move to next step if section done
            if asked_questions + 1 >= max_questions:  # If next question would exceed max
                next_step = get_next_step(current_step, session.role, session)
                session.current_step = next_step
                if next_step == "Exit":
                    session.completed = True
                session.save()  # Save immediately after updating step
            else:
                # Still in same section, just increment counter
                asked_questions += 1
                session.save()

            # 🧾 Calculate remaining
            remaining_sections, remaining_questions = get_remaining(session)
            report_url = None
            if session.completed:
                report_url = request.build_absolute_uri(f"/api/report/{session.id}/")

            return Response({
                "session_id": session.id,
                "step": session.current_step,
                "question": interviewer_text,
                "audio_url": audio_url,
                "remaining_sections": remaining_sections,
                "remaining_questions": remaining_questions,
                "report_url": report_url,
            })

        except InterviewSession.DoesNotExist:
            return Response({"error": "Session not found."}, status=status.HTTP_404_NOT_FOUND)
        except Exception as e:
            print("🔥 Internal Error:", str(e))
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)


class RestartInterviewView(APIView):
    """Restart the interview with a blank session."""
    def post(self, request):
        restart_flag = request.data.get("restart", False)
        if not restart_flag:
            return Response({"error": "restart flag required"}, status=status.HTTP_400_BAD_REQUEST)

        session = InterviewSession.objects.create(
            candidate_name="", role="", current_step="Role Selection", completed=False
        )
        return Response(
            {"message": "Fresh interview session started.", "session_id": session.id},
            status=status.HTTP_201_CREATED,
        )


class InterviewReport(APIView):
    """GET /api/report/{session_id}/"""
    def get(self, request, session_id):
        try:
            session = InterviewSession.objects.get(id=session_id)
            # Generate report even if interview is not marked as completed
            report_text = generate_report(session)
            if request.query_params.get("format") == "text":
                return HttpResponse(report_text, content_type="text/plain")
            return Response({
                "candidate": session.candidate_name,
                "status": "completed" if session.completed else "incomplete",
                "role": session.role,
                "report": report_text
            })
        except InterviewSession.DoesNotExist:
            return Response({"error": "Session not found."}, status=status.HTTP_404_NOT_FOUND)


class SessionList(APIView):
    """List all sessions."""
    def get(self, request):
        sessions = InterviewSession.objects.all().order_by("-id")
        data = [
            {
                "session_id": s.id,
                "candidate_name": s.candidate_name,
                "role": s.role,
                "completed": s.completed,
                "report": generate_report(s) if s.completed else None,
            }
            for s in sessions
        ]
        return Response(data)


class SessionDetail(APIView):
    """GET /api/interview/sessions/<session_id>/"""
    def get(self, request, session_id):
        try:
            session = InterviewSession.objects.get(id=session_id)
            report_text = generate_report(session) if session.completed else None
            return Response({
                "session_id": session.id,
                "candidate_name": session.candidate_name,
                "role": session.role,
                "completed": session.completed,
                "report": report_text
            })
        except InterviewSession.DoesNotExist:
            return Response({"error": "Session not found"}, status=status.HTTP_404_NOT_FOUND)

class InterruptInterviewView(APIView):
    """Endpoint to handle interrupted interviews and generate a report."""
    def post(self, request, session_id):
        try:
            session = InterviewSession.objects.get(id=session_id)
            
            # Mark session as completed to prevent further interactions
            session.completed = True
            session.save()
            
            # Generate the report
            report = generate_report(session)
            
            return Response({
                "message": "Interview interrupted successfully",
                "report_url": request.build_absolute_uri(f"/api/report/{session.id}/")
            }, status=status.HTTP_200_OK)
            
        except InterviewSession.DoesNotExist:
            return Response({"error": "Session not found"}, status=status.HTTP_404_NOT_FOUND)
        except Exception as e:
            print(f"Error interrupting interview: {str(e)}")
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)


class AudioToTextView(APIView):
    """
    Endpoint to receive audio from Unity VR, convert to text, and return.
    """

    def post(self, request):
        audio_data = request.FILES.get('audio')
        if not audio_data:
            return Response({"error": "No audio file provided"}, status=status.HTTP_400_BAD_REQUEST)

        try:
            # Save the audio file temporarily
            temp_audio_path = os.path.join(settings.MEDIA_ROOT, 'temp_audio.wav')
            with open(temp_audio_path, 'wb+') as destination:
                for chunk in audio_data.chunks():
                    destination.write(chunk)

            # Transcribe the audio
            text = transcribe_audio(temp_audio_path)
            
            # Clean up the temporary file
            if os.path.exists(temp_audio_path):
                os.remove(temp_audio_path)
                
            return Response({"text": text}, status=status.HTTP_200_OK)
            
        except Exception as e:
            print(f"Error in audio processing: {str(e)}")
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)


class ResumeUploadView(APIView):
    """Handle resume upload and parsing."""
    
    def post(self, request):
        session_id = request.data.get("session_id")
        role = request.data.get("role")
        pdf_file = request.FILES.get("resume")
        
        if not session_id or not role or not pdf_file:
            return Response({
                "error": "session_id, role, and resume file are required"
            }, status=status.HTTP_400_BAD_REQUEST)
        
        try:
            session = InterviewSession.objects.get(id=session_id)
            
            # Process the resume
            result = process_resume_upload(pdf_file, role)
            
            if "error" in result:
                return Response({"error": result["error"]}, status=status.HTTP_400_BAD_REQUEST)
            
            # Save resume data
            resume = ResumeUpload.objects.create(
                session=session,
                pdf_file=pdf_file,
                raw_text=result["raw_text"],
                summary=result["summary"],
                role=role
            )
            
            return Response({
                "success": True,
                "message": "Resume uploaded and processed successfully",
                "resume_id": resume.id,
                "summary": result["summary"]
            }, status=status.HTTP_201_CREATED)
            
        except InterviewSession.DoesNotExist:
            return Response({"error": "Session not found"}, status=status.HTTP_404_NOT_FOUND)
        except Exception as e:
            print(f"Resume upload error: {str(e)}")
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)