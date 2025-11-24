import os
import requests
from django.conf import settings

OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY")
OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"


def ask_llm(prompt):
    """Send prompt safely to OpenRouter LLM and return text."""
    try:
        payload = {
            "model": "x-ai/grok-4.1-fast:free",
            "messages": [
                {
                    "role": "system",
                    "content": (
                        "You are a professional interviewer AI conducting structured interviews. "
                        "Always stay relevant to the candidate's role and the interview section. "
                        "Be polite, conversational, and professional — no off-topic or casual chat."
                    ),
                },
                {"role": "user", "content": prompt},
            ],
        }

        headers = {
            "Authorization": f"Bearer {OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
        }

        response = requests.post(OPENROUTER_URL, headers=headers, json=payload, timeout=45)
        print("STATUS:", response.status_code)
        print("TEXT:", response.text)
        
        data = response.json()
        return data["choices"][0]["message"]["content"].strip()

    except Exception as e:
        print("LLM ERROR:", str(e))
        return "Let's continue with the next question."


def generate_interviewer_text(role, current_step, previous_answer=None):
    """Generate context-aware interviewer questions dynamically."""
    role_context = (
        f"You are Jake, a professional interviewer AI conducting structured interviews.\n"
        f"You are conducting a professional interview for the position of **{role}**.\n"
        f"Current section: **{current_step}**.\n"
        "Your tone should be conversational, confident, and polite.\n"
        "Avoid personal or casual questions — focus only on professional or technical aspects."
    )

    # 🎤 1. Greeting / Introduction
    if current_step.lower() in ["greeting", "introduction"] and not previous_answer:
        prompt = f"""
{role_context}

Start the interview by greeting the candidate warmly.
Introduce yourself briefly as the interviewer.
Then ask the first question to start the {current_step} section — something like “Can you tell me about yourself?”,
but word it naturally and professionally.
Avoid being robotic or too formal.
"""
        return ask_llm(prompt)

    # 🚪 2. Exit / Wrap-up Section
    if current_step.lower() in ["wrap-up", "exit", "thankyou", "conclusion"]:
        prompt = f"""
{role_context}

Conclude the interview politely.
Thank the candidate for their time, say it was a pleasure speaking with them,
and mention that feedback or results will be shared soon.
Do NOT ask any more questions — this is the end of the session.
"""
        return ask_llm(prompt)

    # 💬 3. Ongoing Questions
    if previous_answer:
        prompt = f"""
{role_context}

The candidate just said: "{previous_answer}"

Your job:
- Appreciate their response briefly (1 short line only).
- Ask the next relevant follow-up question that fits the {current_step} section.
- Keep it concise and related to {role}.
- Avoid repeating or generic questions.
"""
    else:
        prompt = f"""
{role_context}

Start the {current_step} section by asking a relevant, role-based technical or behavioral question.
Keep it short, natural, and clear — not robotic.
"""

    return ask_llm(prompt)
