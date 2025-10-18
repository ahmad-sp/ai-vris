from django.urls import path
from .views import InterviewStep, InterviewReport, RestartInterviewView, SessionList, SessionDetail,AudioToTextView

urlpatterns = [
    path("interview/", InterviewStep.as_view(), name="interview"),
    path("report/<int:session_id>/", InterviewReport.as_view(), name="interview-report"),
    path("interview/restart/", RestartInterviewView.as_view(), name="interview-restart"),
    path("interview/sessions/", SessionList.as_view(), name="interview-sessions"),
    path("interview/sessions/<int:session_id>/", SessionDetail.as_view(), name="interview-session-detail"),
    path("audio-to-text/", AudioToTextView.as_view(), name="audio_to_text"),
]
