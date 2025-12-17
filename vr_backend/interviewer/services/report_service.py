import os
from django.conf import settings
from interviewer.models import InterviewReport
from groq import Groq

GROQ_API_KEY = os.getenv("GROQ_API_KEY")

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

    # Determine true completion status (must have reached end steps)
    is_truly_completed = session.completed and session.current_step in ['Exit', 'Wrap-Up', 'Conclusion']
    status_str = "Completed Interview" if is_truly_completed else "Incomplete Interview"

    # Build transcript from DB
    transcript = f"Candidate: {session.candidate_name}\nRole: {session.role}\nStatus: {status_str}\n\n"
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

    try:
        # Get API key from environment
        if not GROQ_API_KEY:
            print("ERROR: Groq API key not found")
            return "Error generating report: API key not configured."
            
        # Only call the API if we have responses
        if session.responses.count() > 0:
            try:
                client = Groq(api_key=GROQ_API_KEY)
                
                messages = [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": transcript}
                ]
                
                print(f"[Report] Sending request to Groq with model: qwen/qwen3-32b")
                
                completion = client.chat.completions.create(
                    model="qwen/qwen3-32b",
                    messages=messages,
                    temperature=1,
                    max_completion_tokens=800,
                    top_p=1,
                    stream=False,
                    stop=None
                )
                
                report_content = completion.choices[0].message.content.strip()
                
                print(f"[Report] Generated report length: {len(report_content)}")
                
                if not report_content:
                    print("ERROR: Empty report content from API")
                    return generate_basic_report(session, transcript)
                
                # Save the report to the database
                InterviewReport.objects.update_or_create(
                    session=session,
                    defaults={'content': report_content}
                )
                
                return report_content
                
            except Exception as api_error:
                print(f"Report API Error: {str(api_error)}")
                return generate_basic_report(session, transcript)
        else:
            return generate_basic_report(session, transcript)
            
    except Exception as e:
        error_msg = f"Error generating report: {str(e)}"
        print(error_msg)
        return "Error generating report. The interview may be too short or there might be an API issue."


def generate_basic_report(session, transcript):
    """Generate a basic report without AI when API fails."""
    try:
        # Count responses and calculate basic stats
        responses = session.responses.all()
        scored_responses = [r for r in responses if r.score is not None]
        
        # Calculate basic scores
        if scored_responses:
            avg_score = sum(r.score for r in scored_responses) / len(scored_responses)
            score_summary = f"Overall Score: {avg_score:.1f}/10 across {len(scored_responses)} answers"
        else:
            score_summary = "No scored answers yet"
        
        # Determine true completion status (must have reached end steps)
        # Interrupting an interview sets completed=True but step remains intermediate
        is_truly_completed = session.completed and session.current_step in ['Exit', 'Wrap-Up', 'Conclusion']

        # Create basic report
        basic_report = f"""Candidate Summary – {session.role} (Interview {'Completed' if is_truly_completed else 'Incomplete'})
Candidate: {session.candidate_name}
Role: {session.role}
Status: {'Completed Interview' if is_truly_completed else 'Incomplete Interview'}

Summary of Responses
Total Questions Asked: {len(responses)}
Questions Answered: {len([r for r in responses if r.answer])}

Section Scores
{score_summary}

Strengths
- Interview completed successfully
- Responses were collected and evaluated

Areas for Improvement
- Consider providing more detailed responses to questions
- Ensure all questions are addressed comprehensively

Preliminary Assessment
{'Interview was completed with ' + str(len(scored_responses)) + ' scored responses.' if scored_responses else 'Interview was completed but no responses were scored.'}

Recommendations / Next Steps
- Review the detailed Q&A in the interview system
- Consider additional technical assessments if needed
- Schedule follow-up interview for deeper evaluation

Note: This is a basic report generated automatically due to AI service limitations.
"""
        
        # Save the basic report to database
        InterviewReport.objects.update_or_create(
            session=session,
            defaults={'content': basic_report}
        )
        return basic_report
        
    except Exception as e:
        print(f"Error generating basic report: {str(e)}")
        return f"Basic report generation failed for session {session.id}. Please contact support."
