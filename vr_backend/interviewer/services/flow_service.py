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
    """Decide if a role is technical using deterministic checks first, then LLM as fallback."""
    if not role_name:
        return False

    role_l = role_name.strip().lower()

    # Common technical roles allowlist (exact match)
    ALLOWLIST = {
        "software engineer",
        "backend engineer",
        "frontend engineer",
        "full stack developer",
        "full-stack developer",
        "data scientist",
        "ml engineer",
        "machine learning engineer",
        "ai engineer",
        "devops engineer",
        "site reliability engineer",
        "sre",
        "mobile developer",
        "android developer",
        "ios developer",
        "cloud engineer",
        "security engineer",
        "qa engineer",
        "test engineer",
        "systems engineer",
        "network engineer",
        "it support engineer",
        "data engineer",
        "data analyst",
        "ml researcher",
    }

    if role_l in ALLOWLIST:
        return True

    # Keyword heuristics (substring matches)
    KEYWORDS = [
        "engineer",
        "developer",
        "scientist",
        "ml",
        "ai",
        "devops",
        "sre",
        "cloud",
        "backend",
        "front-end",
        "frontend",
        "full stack",
        "mobile",
        "android",
        "ios",
        "security",
        "qa",
        "test",
        "systems",
        "network",
        "data",
        "it",
    ]
    if any(k in role_l for k in KEYWORDS):
        return True

    # Fallback: ask LLM
    prompt = (
        f"Is '{role_name}' a technical/IT role like software engineer, data scientist, AI engineer, etc.? "
        f"Answer only Yes or No."
    )
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
    remaining_sections = max(len(steps) - current_index - 2, 0)  # -2 for current and Exit steps
    
    # Get the max questions for the current section from views.py
    from interviewer.views import MAX_QUESTIONS
    max_questions = MAX_QUESTIONS.get(session.current_step, 3)
    
    asked = session.responses.filter(step=session.current_step).count()
    remaining_questions = max(max_questions - asked, 0)
    
    return remaining_sections, remaining_questions
