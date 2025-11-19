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
    for resp in session.responses.all():
        transcript += f"\nStep: {resp.step}\nQ: {resp.question}\n"
        if resp.answer:
            transcript += f"A: {resp.answer}\n"
        if resp.score is not None:
            transcript += f"Score: {resp.score}\n"
            if hasattr(resp, 'relevance') and resp.relevance is not None:
                transcript += f"Relevance: {resp.relevance}\n"
    system_prompt = """
    You are an interview evaluator. Create a structured report with:
    - Candidate Name & Role
    - Interview Status (Completed/Incomplete)
    - Summary of responses
    - Strengths identified
    - Areas for improvement
    - Current assessment based on available data
    - Next steps or recommendations
    
    If the interview is incomplete, note that the assessment is based on partial data.
    Keep the report professional and constructive.
    """

    payload = {
        "model": "openai/gpt-4-turbo-preview",
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": transcript}
        ]
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
