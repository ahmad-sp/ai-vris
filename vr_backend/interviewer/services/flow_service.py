from groq import Groq
import os
from interviewer.models import InterviewResponse, ResumeUpload
from interviewer.services.llm_service import ask_llm

# Dynamic section flow
STEPS_ORDER = [
    "Greeting",
    "Introduction",
    "Resume Questions",
    "Technical",
    "Behavioral/Situational",
    "Wrap-Up",
    "Exit"
]


def is_technical_role(role_name):
    """
    Use LLM to determine if a role is technical.
    Accepts all technical and technical-adjacent roles.
    """
    if not role_name:
        return True  # Default to accepting if no role specified
    
    role_name = role_name.strip()
    if not role_name:
        return True
    
    print(f"[FlowService] Checking if '{role_name}' is a technical role...")
    
    prompt = f"""Is "{role_name}" a technical or professional role suitable for a technical interview?

Technical roles include but are not limited to:
- Software/IT roles (engineer, developer, architect, etc.)
- Data roles (analyst, scientist, engineer)
- Product/Program management
- Design roles (UX, UI, Product Design)
- QA/Testing roles
- DevOps/Cloud/Infrastructure
- Technical leadership
- Any role that works with technology or in tech companies

Answer with just "Yes" or "No"."""
    
    try:
        response = ask_llm(prompt).strip().lower()
        # Clean up response - remove any extra content
        response = response.replace('"', '').replace("'", "").strip()
        
        # Check if response starts with or contains "yes"
        is_technical = response.startswith("yes") or "yes" in response.split()[0] if response else True
        
        print(f"[FlowService] LLM response: '{response}' -> is_technical: {is_technical}")
        return is_technical
        
    except Exception as e:
        print(f"[FlowService] is_technical_role LLM check failed: {e}")
        return True  # Accept by default - be inclusive


def get_next_step(current_step, role=None, session=None):
    """Returns next section, skipping Resume Questions if no resume uploaded."""
    if current_step not in STEPS_ORDER:
        return "Exit"
    idx = STEPS_ORDER.index(current_step)
    
    # Get next step
    next_step = STEPS_ORDER[idx + 1] if idx < len(STEPS_ORDER) - 1 else "Exit"
    
    # Skip Resume Questions if no resume uploaded
    if next_step == "Resume Questions" and session:
        # Refresh session from database to ensure we have latest data
        try:
            session.refresh_from_db()
        except:
            pass  # If refresh fails, continue with current session object
        
        # Properly check if resume exists using the model
        has_resume = ResumeUpload.objects.filter(session=session).exists()
        print(f"[FlowService] Checking resume for session {session.id} (current_step={session.current_step}): has_resume={has_resume}")
        
        if not has_resume:
            # No resume uploaded, skip to Technical
            print(f"[FlowService] ⚠️ No resume found for session {session.id}, skipping Resume Questions → Technical")
            return "Technical"
        else:
            print(f"[FlowService] ✅ Resume found for session {session.id}, proceeding to Resume Questions")
    
    return next_step


def get_remaining(session):
    """Returns remaining sections and question counts."""
    if session.current_step == "Exit":
        return 0, 0

    steps = STEPS_ORDER
    current_index = steps.index(session.current_step)
    
    # Calculate remaining sections, accounting for potential skipping of Resume Questions
    remaining_sections = 0
    for i in range(current_index + 1, len(steps) - 1):  # Exclude Exit step
        step = steps[i]
        if step == "Resume Questions":
            # Properly check if resume exists
            has_resume = ResumeUpload.objects.filter(session=session).exists()
            if has_resume:
                remaining_sections += 1
            # If no resume, skip this section (continue to next)
        else:
            remaining_sections += 1
    
    # Get the max questions for the current section from views.py
    from interviewer.views import MAX_QUESTIONS
    max_questions = MAX_QUESTIONS.get(session.current_step, 3)
    
    asked = session.responses.filter(step=session.current_step).count()
    remaining_questions = max(max_questions - asked, 0)
    
    return remaining_sections, remaining_questions
