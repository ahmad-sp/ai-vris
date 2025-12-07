import os
import requests
from django.conf import settings

OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY")
OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"


def ask_llm(prompt):
    """Send prompt safely to OpenRouter LLM and return text."""
    try:
        print(f"[LLM] API Key exists: {bool(OPENROUTER_API_KEY)}")
        print(f"[LLM] Prompt length: {len(prompt)}")
        
        payload = {
            "model": "google/gemma-3-27b-it:free",
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

        print(f"[LLM] Sending request to: {OPENROUTER_URL}")
        response = requests.post(OPENROUTER_URL, headers=headers, json=payload, timeout=45)
        print("STATUS:", response.status_code)
        print("TEXT:", response.text)
        
        if response.status_code != 200:
            print(f"[LLM] HTTP Error: {response.status_code}")
            return "I apologize, but I'm having trouble connecting right now. Let's continue with the next question."
        
        try:
            data = response.json()
            if not data.get("choices"):
                print("[LLM] No choices in response")
                return "I apologize, but I'm having trouble generating a response right now. Let's continue."
            
            content = data["choices"][0]["message"]["content"].strip()
            print(f"[LLM] Generated content length: {len(content)}")
            return content
            
        except Exception as json_error:
            print(f"[LLM] JSON parsing error: {json_error}")
            return "I apologize, but I'm having trouble processing the response right now."

    except Exception as e:
        print("LLM ERROR:", str(e))
        return "Let's continue with the next question."


def generate_interviewer_text(role, current_step, previous_answer=None, resume_summary=None):
    """Generate context-aware interviewer questions dynamically."""
    role_context = (
        f"You are Jake, a professional interviewer AI conducting structured interviews.\n"
        f"You are conducting a professional interview for the position of **{role}**.\n"
        f"Current section: **{current_step}**.\n"
        "Your tone should be conversational, confident, and polite.\n"
        "Avoid personal or casual questions — focus only on professional or technical aspects.\n"
        "IMPORTANT: Do NOT add any prefixes like 'Interviewer:', 'Jake:', or any role labels to your responses. "
        "Respond directly with the interview questions or statements only."
    )

    # 1. Greeting / Introduction
    if current_step.lower() in ["greeting", "introduction"] and not previous_answer:
        prompt = f"""
{role_context}

Start the interview by greeting the candidate warmly.
Then ask the first question to start the {current_step} section — something like "Can you tell me about yourself?",
but word it naturally and professionally.
Avoid being robotic or too formal.
Do NOT introduce yourself with a name or add any prefixes to your response.
"""
        return ask_llm(prompt)

    # 2. Exit / Wrap-up Section
    if current_step.lower() in ["wrap-up", "exit", "thankyou", "conclusion"]:
        prompt = f"""
{role_context}

Conclude the interview politely.
Thank the candidate for their time, say it was a pleasure speaking with them,
and mention that feedback or results will be shared soon.
Do NOT ask any more questions — this is the end of the session.
"""
        return ask_llm(prompt)

    # 💼 4. Resume Questions Section
    if current_step.lower() == "resume questions":
        if resume_summary:
            print(f"[LLMService] Generating resume question with summary (length: {len(resume_summary)})")
            prompt = f"""
{role_context}

Candidate Resume Summary:
{resume_summary}

Based on the candidate's resume summary above, ask specific, relevant questions about:
- Their experience and skills mentioned in the resume
- Projects or achievements they've highlighted
- How their background relates to the {role} position
- Any gaps or areas you'd like to explore further

Ask questions that show you've actually reviewed their resume and are genuinely interested in their background.
Make the questions specific to their experience, not generic.
"""
            return ask_llm(prompt)
        else:
            print(f"[LLMService] ⚠️ Resume Questions step but no resume_summary provided!")
            # Fallback to generic question if resume summary is missing
            prompt = f"""
{role_context}

Ask a question about the candidate's professional background and experience relevant to the {role} position.
"""
            return ask_llm(prompt)

    # 💬 5. Ongoing Questions (including resume questions follow-up)
    if previous_answer:
        prompt = f"""
{role_context}

The candidate just said: "{previous_answer}"
"""
        
        # Add resume context if we're in resume questions section
        if current_step.lower() == "resume questions" and resume_summary:
            prompt += f"""

Candidate Resume Summary (for context):
{resume_summary}
"""
        
        prompt += f"""

Your job:
- Appreciate their response briefly (1 short line only).
- Ask the next relevant follow-up question that fits the {current_step} section.
- Keep it concise and related to {role}.
- If this is resume questions, continue asking about their experience, skills, or background based on their resume.
- Avoid repeating or generic questions.
"""
    else:
        prompt = f"""
{role_context}
"""
        
        # Add resume context for initial resume questions
        if current_step.lower() == "resume questions" and resume_summary:
            prompt += f"""

Candidate Resume Summary:
{resume_summary}

Based on the candidate's resume summary above, ask specific, relevant questions about:
- Their experience and skills mentioned in the resume
- Projects or achievements they've highlighted
- How their background relates to the {role} position

Ask questions that show you've actually reviewed their resume and are genuinely interested in their background.
Make the questions specific to their experience, not generic.
"""
        else:
            prompt += f"""

Start the {current_step} section by asking a relevant, role-based technical or behavioral question.
Keep it short, natural, and clear — not robotic.
"""

    return ask_llm(prompt)
