import requests
from django.conf import settings
from .scoring_service import OPENROUTER_URL

def generate_report(session):
    """
    Generate a final interview report using stored Q&A + scores.
    """
    headers = {
        "Authorization": f"Bearer {settings.OPENROUTER_API_KEY}",
        "Content-Type": "application/json",
    }

    # Build transcript from DB
    transcript = ""
    for resp in session.responses.all():
        transcript += f"\nStep: {resp.step}\nQ: {resp.question}\nA: {resp.answer}\nScore: {resp.score}\n"

    system_prompt = """
    You are an interview evaluator.  
    Create a structured report with:
    - Candidate Name & Role
    - Average Score (1-5)
    - Strengths
    - Weaknesses
    - Recommendation (Hire / Maybe / Reject)
    - Bullet point summary of each interview step with score
    Keep it professional and concise.
    """

    payload = {
        "model": "gpt-4o-mini",
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": transcript}
        ]
    }

    response = requests.post(OPENROUTER_URL, headers=headers, json=payload)
    response.raise_for_status()
    data = response.json()
    report_text = data["choices"][0]["message"]["content"].strip()
    return report_text
