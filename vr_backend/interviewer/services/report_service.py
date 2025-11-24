import requests
from django.conf import settings
from interviewer.models import InterviewReport

OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"

def generate_report(session):
    """
    Generate an interview report using stored Q&A + scores.
    Works for both completed and incomplete interviews.
    Saves the report to the database and returns the content.
    """
    # Check if report already exists for this session
    try:
        existing_report = InterviewReport.objects.filter(session=session).first()
        if existing_report:
            return existing_report.content
    except Exception as e:
        print(f"Error checking for existing report: {str(e)}")

    # Build transcript from DB
    transcript = f"Candidate: {session.candidate_name}\nRole: {session.role}\n\n"
    section_scores = {}
    total_scores = []
    for resp in session.responses.all():
        transcript += f"\nStep: {resp.step}\nQ: {resp.question}\n"
        if resp.answer:
            transcript += f"A: {resp.answer}\n"
        if resp.answer and resp.score is not None:
            transcript += f"Score: {resp.score}\n"
            total_scores.append(resp.score)
            section_scores.setdefault(resp.step, []).append(resp.score)
            if hasattr(resp, 'relevance') and resp.relevance is not None:
                transcript += f"Relevance: {resp.relevance}\n"

    if section_scores:
        transcript += "\nSection Score Summary:\n"
        for step, scores in section_scores.items():
            avg = sum(scores) / len(scores)
            transcript += f"{step}: {avg:.1f}/10 based on {len(scores)} scored answer(s)\n"
        overall = sum(total_scores) / len(total_scores)
        transcript += f"Overall Score: {overall:.1f}/10 across {len(total_scores)} scored answer(s)\n"
    else:
        transcript += "\nSection Score Summary:\nNo scored answers yet.\n"
    system_prompt = """
    You are an interview evaluator. Produce plain-text output (no Markdown bold, no asterisks) using this layout exactly:

    Candidate Summary – {Role} (Interview {Completed/Incomplete})
    Candidate: <name>
    Role: <role>
    Status: Completed Interview / Incomplete Interview

    Summary of Responses
    Introduction:
    <1-2 sentence summary or "No data collected yet.">
    Technical Question(s):
    <summaries of each key response>

    Section Scores
    Introduction: <avg>/10 (<#> answers)
    Resume Questions: <avg>/10 (<#> answers)
    Technical: <avg>/10 (<#> answers)
    Behavioral/Situational: <avg>/10 (<#> answers)
    Wrap-Up: <avg>/10 (<#> answers)
    Overall Score: <overall>/10 (<total answers>)

    Strengths
    - <strength 1>
    - <strength 2>

    Areas for Improvement
    - <area 1>
    - <area 2>

    Preliminary Assessment
    <2-3 sentences. Mention if the interview is incomplete.>

    Recommendations / Next Steps
    - <action item 1>
    - <action item 2>

    Keep sentences concise and professional. If information is missing, explicitly state that it was not gathered. Use the section scores and total score to justify the strengths, areas for improvement, and recommendations.
    """

    payload = {
        "model": "x-ai/grok-4.1-fast:free",
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": transcript}
        ],
        # Stay within the 1180-token credit limit reported by OpenRouter.
        # This is a hard upper bound for the completion tokens.
        "max_tokens": 800,
    }

    try:
        # Get API key from settings
        headers = {
            "Authorization": f"Bearer {settings.OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
        }
        
        # Only call the API if we have responses
        if session.responses.count() > 0:
            response = requests.post(OPENROUTER_URL, headers=headers, json=payload, timeout=30)
            response.raise_for_status()
            data = response.json()
            if "choices" not in data or not data["choices"]:
                raise ValueError("Invalid response format from OpenRouter API")
            
            report_content = data["choices"][0]["message"]["content"].strip()
            
            # Save the report to the database
            InterviewReport.objects.update_or_create(
                session=session,
                defaults={'content': report_content}
            )
            
            return report_content
        else:
            return "No interview responses available to generate a report."
            
    except requests.exceptions.RequestException as e:
        error_msg = f"Error generating report: {str(e)}"
        print(error_msg)
        if 'response' in locals() and hasattr(response, 'text'):
            print(f"Response content: {response.text[:500]}")
        return "Error generating report. The interview may be too short or there might be an API issue."
