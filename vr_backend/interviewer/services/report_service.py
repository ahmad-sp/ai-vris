import os
import re
from django.conf import settings
from interviewer.models import InterviewReport
from groq import Groq

GROQ_API_KEY = os.getenv("GROQ_API_KEY")


def clean_report_content(content):
    """
    Remove any think tags, internal reasoning, or meta-commentary from report content.
    """
    if not content:
        return content
    
    # Remove <think>...</think> blocks and any similar tags
    cleaned = re.sub(r'<think>.*?</think>', '', content, flags=re.DOTALL | re.IGNORECASE)
    cleaned = re.sub(r'<reasoning>.*?</reasoning>', '', cleaned, flags=re.DOTALL | re.IGNORECASE)
    cleaned = re.sub(r'<internal>.*?</internal>', '', cleaned, flags=re.DOTALL | re.IGNORECASE)
    
    # Remove any remaining XML-like tags
    cleaned = re.sub(r'<[^>]+>.*?</[^>]+>', '', cleaned, flags=re.DOTALL)
    cleaned = re.sub(r'<[^>]+/?>', '', cleaned)
    
    # Remove lines that look like internal notes
    lines = cleaned.split('\n')
    filtered_lines = []
    for line in lines:
        line_lower = line.strip().lower()
        # Skip lines that look like internal reasoning
        if line_lower.startswith(('let me', 'i need to', 'first,', 'now,', 'okay,', 'alright,', 'looking at', 'starting with')):
            continue
        if 'i should' in line_lower or 'i will' in line_lower:
            continue
        filtered_lines.append(line)
    
    cleaned = '\n'.join(filtered_lines)
    
    # Clean up extra whitespace
    cleaned = re.sub(r'\n{3,}', '\n\n', cleaned)
    cleaned = cleaned.strip()
    
    return cleaned

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
    system_prompt = """Generate a DETAILED professional interview assessment report. Output the report content ONLY - no thinking, no reasoning, no tags.

FORMAT (follow exactly):

================================================================================
INTERVIEW ASSESSMENT REPORT
================================================================================

CANDIDATE INFORMATION
Candidate: [Full name]
Position Applied: [Role]
Interview Status: [Completed/Incomplete]
Date: [Current date if available, otherwise "On Record"]

--------------------------------------------------------------------------------
EXECUTIVE SUMMARY
--------------------------------------------------------------------------------
[3-4 sentences providing a high-level overview of the candidate's performance, key strengths, and main concerns. Be specific about what stood out positively and negatively.]

--------------------------------------------------------------------------------
DETAILED RESPONSE ANALYSIS
--------------------------------------------------------------------------------

INTRODUCTION & BACKGROUND:
[2-3 sentences analyzing how the candidate presented themselves, their communication style, and initial impression. Mention specific details they shared.]

TECHNICAL COMPETENCY:
[3-4 sentences evaluating technical knowledge demonstrated. Reference specific answers, technologies mentioned, and problem-solving approaches. Note any gaps or inaccuracies.]

BEHAVIORAL & SITUATIONAL:
[2-3 sentences on how the candidate handled behavioral questions. Note examples they provided and their approach to challenges. Write "Not assessed" if no behavioral questions were asked.]

COMMUNICATION QUALITY:
[2 sentences on clarity, articulation, and professionalism of responses. Note any issues like off-topic answers, unclear explanations, or particularly strong communication.]

--------------------------------------------------------------------------------
SCORING BREAKDOWN
--------------------------------------------------------------------------------
Section                    | Score    | Responses
---------------------------|----------|----------
Introduction               | [X.X]/10 | [N] answers
Resume/Background          | [X.X]/10 | [N] answers  
Technical                  | [X.X]/10 | [N] answers
Behavioral/Situational     | [X.X]/10 | [N] answers
Wrap-Up                    | [X.X]/10 | [N] answers
---------------------------|----------|----------
OVERALL SCORE              | [X.X]/10 | [Total] answers

[If any section was not assessed, write "Not assessed" for that row]

--------------------------------------------------------------------------------
KEY STRENGTHS
--------------------------------------------------------------------------------
1. [Specific strength with example from interview]
2. [Specific strength with example from interview]
3. [Additional strength if applicable]

--------------------------------------------------------------------------------
AREAS FOR DEVELOPMENT
--------------------------------------------------------------------------------
1. [Specific area needing improvement with reference to response]
2. [Specific area needing improvement with reference to response]
3. [Additional area if applicable]

--------------------------------------------------------------------------------
NOTABLE OBSERVATIONS
--------------------------------------------------------------------------------
[Any specific quotes, concerning responses, or exceptional answers worth highlighting. If candidate gave irrelevant answers, mention those specifically here.]

--------------------------------------------------------------------------------
RECOMMENDATION
--------------------------------------------------------------------------------
[Clear recommendation: Strong Hire / Hire / Consider with Reservations / Do Not Proceed]

Rationale: [2-3 sentences explaining the recommendation based on the scores and analysis above.]

--------------------------------------------------------------------------------
SUGGESTED NEXT STEPS
--------------------------------------------------------------------------------
1. [Specific action item]
2. [Specific action item]
3. [Additional action if needed]

================================================================================

RULES:
- Be specific and reference actual responses from the interview
- If answers were irrelevant or off-topic, explicitly mention this as a concern
- Base all assessments on the actual scores and content provided
- Do NOT include any internal notes, thinking, or XML tags
- Write in professional third-person tone
- If data is missing for a section, clearly state "Not assessed" or "Data not collected" """

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
                    {"role": "user", "content": f"Generate the interview report for this data. Output ONLY the report, nothing else:\n\n{transcript}"}
                ]
                
                print(f"[Report] Sending request to Groq with model: qwen/qwen3-32b")
                
                completion = client.chat.completions.create(
                    model="qwen/qwen3-32b",
                    messages=messages,
                    temperature=0.3,  # Lower temperature for consistent, professional output
                    max_completion_tokens=1000,
                    top_p=1,
                    stream=False,
                    stop=None
                )
                
                report_content = completion.choices[0].message.content.strip()
                
                # CRITICAL: Clean any think tags or internal reasoning from the output
                report_content = clean_report_content(report_content)
                
                print(f"[Report] Generated report length after cleaning: {len(report_content)}")
                
                if not report_content:
                    print("ERROR: Empty report content from API")
                    return generate_basic_report(session, transcript)
                
                # Save the cleaned report to the database
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
