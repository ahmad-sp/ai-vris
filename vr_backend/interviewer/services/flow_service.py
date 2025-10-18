from .llm_service import ask_llm
from interviewer.models import InterviewResponse

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
    """Use AI to decide if a role is technical."""
    prompt = f"Is '{role_name}' a technical/IT role like software engineer, data scientist, AI engineer, etc.? Answer only Yes or No."
    try:
        response = ask_llm(prompt).strip().lower()
        return response.startswith("yes")
    except Exception:
        return True  # fallback to allow


def get_next_step(current_step, role=None):
    """Returns next section."""
    if current_step not in STEPS_ORDER:
        return "Exit"
    idx = STEPS_ORDER.index(current_step)
    if idx >= len(STEPS_ORDER) - 1:
        return "Exit"
    return STEPS_ORDER[idx + 1]


def get_remaining(session):
    """Returns remaining sections and question counts."""
    if session.current_step == "Exit":
        return 0, 0

    steps = STEPS_ORDER
    current_index = steps.index(session.current_step)
    remaining_sections = max(len(steps) - current_index - 2, 0)
    asked = session.responses.filter(step=session.current_step).count()
    remaining_questions = max(3 - asked, 0)
    return remaining_sections, remaining_questions
