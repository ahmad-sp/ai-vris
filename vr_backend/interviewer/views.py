import os
import time
import requests
from pathlib import Path
from groq import Groq
from dotenv import load_dotenv
from rest_framework.views import APIView
from rest_framework.response import Response
from rest_framework import status
from django.conf import settings
from django.http import HttpResponse

from .models import InterviewSession, InterviewResponse, ResumeUpload, InterviewReport
from .services.llm_service import generate_interviewer_text
from .services.scoring_service import score_answer
from .services.flow_service import get_next_step, get_remaining, is_technical_role
from .services.report_service import generate_report
from .services.speech_to_text import transcribe_audio
from .services.resume_parser_service import process_resume_upload

load_dotenv()

GROQ_API_KEY = os.getenv("GROQ_API_KEY")
client = Groq(api_key=GROQ_API_KEY)

DEFAULT_SECTION_FLOW = [
    "Greeting", "Introduction", "Resume Questions",
    "Technical", "Behavioral/Situational", "Wrap-Up"
]

MAX_QUESTIONS = {
    "Greeting": 2,
    "Introduction": 3,
    "Resume Questions": 5,
    "Technical": 5,
    "Behavioral/Situational": 3,
    "Wrap-Up": 2  # Only two closing message
}


def text_to_speech(text, request_obj=None):
    """Convert interviewer text to speech using Groq PlayAI."""
    if not GROQ_API_KEY:
        print("⚠️ Missing Groq API Key.")
        return None

    try:
        response = client.audio.speech.create(
            model="playai-tts",
            voice="Chip-PlayAI",
            response_format="mp3",
            input=text,
        )

        filename = f"reply_{int(time.time())}.mp3"
        media_root = Path(settings.MEDIA_ROOT)
        media_root.mkdir(parents=True, exist_ok=True)
        filepath = media_root / filename
        
        # Write binary content to file
        with open(filepath, "wb") as f:
            if hasattr(response, "content"):
                f.write(response.content)
            elif hasattr(response, "read"):
                f.write(response.read())
            elif hasattr(response, "iter_bytes"):
                for chunk in response.iter_bytes():
                    f.write(chunk)
            else:
                print(f"🛑 Groq TTS Error: Unknown response type {type(response)}")
                return None

        if request_obj:
            return request_obj.build_absolute_uri(settings.MEDIA_URL + filename)
        return filename
    except Exception as e:
        print("🛑 Groq TTS Error:", e)
        return None


class InterviewStep(APIView):
    """Main endpoint controlling each voice-driven interview step."""

    def post(self, request):
        candidate_name = request.data.get("candidate_name", "Anonimus")
        role = request.data.get("role")
        session_id = request.data.get("session_id")
        
        print(f"[InterviewStep] New request - candidate: {candidate_name}, role: {role}, session_id: {session_id}")
        print(f"[InterviewStep] Full request data: {request.data}")
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
                print(f"[InterviewStep] CREATED NEW SESSION {session.id} because no session_id was provided")

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
                    "report_url": request.build_absolute_uri(f"/api/reports/{session.id}/"),
                })

            # 🧠 Generate interviewer text dynamically
            # Refresh session to ensure we have latest data (especially resume)
            session.refresh_from_db()
            resume_summary = None
            
            # Try to fetch resume for ALL steps so the LLM has context
            try:
                resume = ResumeUpload.objects.get(session=session)
                resume_summary = resume.summary
                if current_step == "Resume Questions":
                    print(f"[Interview] Resume Questions step - Found resume with summary length: {len(resume_summary) if resume_summary else 0}")
            except ResumeUpload.DoesNotExist:
                resume_summary = None
                if current_step == "Resume Questions":
                    print(f"[Interview] ⚠️ Resume Questions step but no resume found for session {session.id}")
            
            print(f"[Interview] Generating question for step '{current_step}', has_resume_summary={resume_summary is not None}")
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
                    "report_url": request.build_absolute_uri(f"/api/reports/{session.id}/"),
                })

            # 🗣️ Store interviewer question and convert to speech
            InterviewResponse.objects.create(session=session, step=current_step, question=interviewer_text)
            audio_url = text_to_speech(interviewer_text, request_obj=request)

            # 🔁 Move to next step if section done
            if asked_questions + 1 >= max_questions:  # If next question would exceed max
                # Refresh session from database to ensure we have latest resume data
                session.refresh_from_db()
                next_step = get_next_step(current_step, session.role, session)
                print(f"[Interview] Moving from '{current_step}' to '{next_step}' (asked_questions={asked_questions + 1}, max={max_questions})")
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
                report_url = request.build_absolute_uri(f"/api/reports/{session.id}/")

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
            temp_audio_path = os.path.join(settings.MEDIA_ROOT, 'temp_audio.mp3')
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
        print(f"[ResumeUpload] Request method: {request.method}")
        print(f"[ResumeUpload] Request data: {request.data}")
        print(f"[ResumeUpload] Request FILES: {request.FILES}")
        
        session_id = request.data.get("session_id")
        role = request.data.get("role")
        pdf_file = request.FILES.get("resume")
        
        print(f"[ResumeUpload] session_id: {session_id}")
        print(f"[ResumeUpload] role: {role}")
        print(f"[ResumeUpload] pdf_file exists: {pdf_file is not None}")
        if pdf_file:
            print(f"[ResumeUpload] pdf_file name: {pdf_file.name}")
            print(f"[ResumeUpload] pdf_file size: {pdf_file.size}")
        
        if not session_id or not role or not pdf_file:
            return Response({
                "error": "session_id, role, and resume file are required"
            }, status=status.HTTP_400_BAD_REQUEST)
        
        try:
            session = InterviewSession.objects.get(id=session_id)
            print(f"[ResumeUpload] Found session: {session.id}")
            
            # Process the resume
            print(f"[ResumeUpload] Starting resume processing...")
            result = process_resume_upload(pdf_file, role)
            print(f"[ResumeUpload] Processing result: {result}")
            
            if "error" in result:
                print(f"[ResumeUpload] Processing error: {result['error']}")
                return Response({"error": result["error"]}, status=status.HTTP_400_BAD_REQUEST)
            
            # Save resume data
            resume = ResumeUpload.objects.create(
                session=session,
                pdf_file=pdf_file,
                raw_text=result["raw_text"],
                summary=result["summary"],
                role=role
            )
            
            print(f"[ResumeUpload] Resume saved successfully for session {session.id}, resume_id={resume.id}")
            print(f"[ResumeUpload] Resume summary length: {len(result['summary'])} characters")
            
            return Response({
                "success": True,
                "message": "Resume uploaded and processed successfully",
                "resume_id": resume.id,
                "summary": result["summary"]
            }, status=status.HTTP_201_CREATED)
            
        except InterviewSession.DoesNotExist:
            print(f"[ResumeUpload] Session not found: {session_id}")
            return Response({"error": "Session not found"}, status=status.HTTP_404_NOT_FOUND)
        except Exception as e:
            print(f"[ResumeUpload] Unexpected error: {str(e)}")
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)


class ReportsList(APIView):
    """GET /api/reports/ - List all available reports"""
    def get(self, request):
        try:
            # Get all sessions that have reports (either completed or with responses)
            sessions_with_reports = []
            
            # Get completed sessions first
            completed_sessions = InterviewSession.objects.filter(completed=True).order_by('-created_at')
            for session in completed_sessions:
                # Check if report exists or can be generated
                if InterviewReport.objects.filter(session=session).exists() or session.responses.exists():
                    # Check if truly completed (reached end steps)
                    # Use exact same logic as report service for consistency
                    is_truly_completed = session.completed and session.current_step in ['Exit', 'Wrap-Up', 'Conclusion']
                    
                    sessions_with_reports.append({
                        "session_id": session.id,
                        "candidate_name": session.candidate_name or "Unknown",
                        "role": session.role or "Unknown",
                        "completed": is_truly_completed,
                        "created_at": session.created_at.isoformat(),
                        "report_available": True
                    })
            
            # Also get incomplete sessions that have responses (for partial reports)
            incomplete_sessions = InterviewSession.objects.filter(completed=False).order_by('-created_at')
            for session in incomplete_sessions:
                if session.responses.exists():
                    sessions_with_reports.append({
                        "session_id": session.id,
                        "candidate_name": session.candidate_name or "Unknown",
                        "role": session.role or "Unknown",
                        "completed": False,  # Always false for incomplete sessions
                        "created_at": session.created_at.isoformat(),
                        "report_available": True
                    })
            
            return Response({
                "reports": sessions_with_reports,
                "total_count": len(sessions_with_reports)
            })
            
        except Exception as e:
            print(f"Error fetching reports list: {str(e)}")
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)


class ReportDetail(APIView):
    """GET /api/reports/{session_id}/ - Get detailed report for a specific session"""
    def get(self, request, session_id):
        try:
            session = InterviewSession.objects.get(id=session_id)
            
            # Generate or retrieve the report
            report_text = generate_report(session)
            
            # Get additional session details
            responses_count = session.responses.count()
            scored_responses = session.responses.exclude(score__isnull=True).count()
            
            # Check if truly completed (reached end steps)
            is_truly_completed = session.completed and session.current_step in ['Exit', 'Wrap-Up', 'Conclusion']
            
            return Response({
                "session_id": session.id,
                "candidate_name": session.candidate_name or "Unknown",
                "role": session.role or "Unknown",
                "completed": is_truly_completed,
                "created_at": session.created_at.isoformat(),
                "responses_count": responses_count,
                "scored_responses": scored_responses,
                "report": report_text,
                "has_resume": ResumeUpload.objects.filter(session=session).exists()
            })
            
        except InterviewSession.DoesNotExist:
            return Response({"error": "Session not found"}, status=status.HTTP_404_NOT_FOUND)
        except Exception as e:
            print(f"Error fetching report detail: {str(e)}")
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)