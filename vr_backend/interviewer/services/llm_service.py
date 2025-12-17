import os
from groq import Groq

GROQ_API_KEY = os.getenv("GROQ_API_KEY")


import re

def ask_llm(prompt):
    """Send prompt safely to Groq LLM and return text."""
    try:
        print(f"[LLM] API Key exists: {bool(GROQ_API_KEY)}")
        print(f"[LLM] Prompt length: {len(prompt)}")
        
        client = Groq(api_key=GROQ_API_KEY)
        
        messages = [
            {
                "role": "system",
                "content": (
                    "You are Jake, a professional interviewer conducting a structured job interview. "
                    "Maintain a professional, composed, and encouraging demeanor throughout the interview. "
                    "Your goal is to assess the candidate's qualifications, skills, and cultural fit. "
                    "OUTPUT REQUIREMENTS: "
                    "1. Respond with ONLY the spoken interview content - questions, acknowledgments, or statements. "
                    "2. Never include internal notes, reasoning, commentary, or any meta-text. "
                    "3. Never use prefixes like 'Interviewer:', 'Jake:', or similar labels. "
                    "4. Keep responses professional, clear, and conversational."
                ),
            },
            {"role": "user", "content": prompt},
        ]
        
        print(f"[LLM] Sending request to Groq with model: qwen/qwen3-32b")
        
        completion = client.chat.completions.create(
            model="qwen/qwen3-32b",
            messages=messages,
            temperature=0.7,  # Lower temperature for more focused/professional output
            max_completion_tokens=1024,
            top_p=1,
            stream=False,
            stop=None
        )
        
        content = completion.choices[0].message.content.strip()
        
        # Clean up any internal reasoning or tags that may appear in output
        content = re.sub(r'<[^>]+>.*?</[^>]+>', '', content, flags=re.DOTALL).strip()
        content = re.sub(r'<[^>]+>', '', content).strip()
        
        print(f"[LLM] Generated content length: {len(content)}")
        return content

    except Exception as e:
        print("LLM ERROR:", str(e))
        return "Let's continue with the next question."


def generate_interviewer_text(role, current_step, previous_answer=None, resume_summary=None):
    """Generate context-aware interviewer questions dynamically."""
    role_context = (
        f"You are Jake, a professional interviewer conducting a structured job interview.\n"
        f"Position: {role}\n"
        f"Current Section: {current_step}\n\n"
        "INTERVIEW GUIDELINES:\n"
        "- Maintain a professional, composed, and encouraging tone\n"
        "- Ask clear, relevant questions appropriate for the role and section\n"
        "- Focus on professional qualifications, experience, and skills\n\n"
        "OUTPUT RULES:\n"
        "- Respond with only the spoken interview content\n"
        "- Do not include any prefixes, labels, or internal notes\n"
        "- No meta-commentary or reasoning - just the interview dialogue"
    )

    # 1. Greeting / Introduction
    if current_step.lower() in ["greeting", "introduction"] and not previous_answer:
        prompt = f"""
{role_context}

Begin the interview with a professional greeting and introduce yourself briefly.
Then ask an opening question to learn about the candidate's background.

Keep the greeting warm but professional. The opening question should invite the candidate
to share their professional background and what brings them to this opportunity.
"""
        return ask_llm(prompt)

    # 2. Exit / Wrap-up Section
    if current_step.lower() in ["wrap-up", "exit", "thankyou", "conclusion"]:
        prompt = f"""
{role_context}

Conclude the interview professionally.
- Thank the candidate for their time and participation
- Express that it was a pleasure speaking with them
- Mention that the team will follow up regarding next steps
- Keep the closing warm and professional
- Do not ask any additional questions
"""
        return ask_llm(prompt)

    # Resume Questions Section - FIRST QUESTION
    if current_step.lower() == "resume questions" and not previous_answer:
        if resume_summary:
            print(f"[LLMService] Generating FIRST resume question with summary (length: {len(resume_summary)})")
            prompt = f"""
{role_context}

Candidate Resume Summary:
{resume_summary}

This is the Resume Questions section. Ask a single, focused question about the candidate's background.

QUESTION GUIDELINES:
- Select ONE specific element from their resume that is relevant to the {role} position
- Ask about it in a way that invites detailed discussion
- Focus on experience, projects, or skills that demonstrate their qualifications
- Keep the question clear and professional

Ask only one question. Be specific and reference something from their resume.
"""
            return ask_llm(prompt)
        else:
            print(f"[LLMService] Resume Questions step but no resume_summary provided")
            prompt = f"""
{role_context}

Ask a professional question about the candidate's relevant experience and background for the {role} position.
Focus on their professional journey and key accomplishments.
"""
            return ask_llm(prompt)


    # Ongoing Questions (including resume questions follow-up)
    if previous_answer:
        prompt = f"""
{role_context}

Candidate's response: "{previous_answer}"
"""
        
        # Add resume context if we're in resume questions section
        if current_step.lower() == "resume questions" and resume_summary:
            prompt += f"""

Candidate Resume Summary:
{resume_summary}

CRITICAL INSTRUCTIONS FOR RESUME QUESTIONS FOLLOW-UP:
- Acknowledge their answer briefly (1 short sentence like "That's interesting!" or "I see")
- Ask ONLY ONE follow-up question based on what they just said
- Make it feel like a natural conversation, not an interrogation
- Dig deeper into what they mentioned, or explore a related aspect from their resume
- DO NOT ask multiple questions at once
- DO NOT repeat questions about things they already explained
- DO NOT ask generic questions - be specific to their experience
- Make each question build on the previous answer or explore a new area from their resume

Examples of GOOD follow-ups:
- "That's fascinating! How did you handle [specific challenge they mentioned]?"
- "I see. What made you decide to use [technology they mentioned]?"
- "Interesting approach! Did you face any obstacles when implementing that?"

Examples of BAD follow-ups (DO NOT DO THIS):
- Asking about something they already explained in detail
- "Can you tell me about X? And also Y? And what about Z?" (Multiple questions)
- Generic questions that ignore what they just said

Remember: ONE question. Build on their answer or explore something new from their resume. Keep it conversational.
"""
        else:
            prompt += f"""

RESPONSE GUIDELINES:
- Provide brief acknowledgment of their answer
- Ask the next relevant question for the {current_step} section
- Ensure the question is appropriate for the {role} position
- Keep the interview moving forward professionally
"""
        
        return ask_llm(prompt)
    
    # Initial question for other sections (not resume, not greeting)
    else:
        prompt = f"""
{role_context}
"""
        
        if current_step.lower() == "resume questions" and resume_summary:
            prompt += f"""

Candidate Resume Summary:
{resume_summary}

Ask one specific question about their background. Focus on experience or skills relevant to the {role} position.
"""
        elif current_step.lower() == "technical":
            prompt += f"""

Begin the Technical section with a relevant technical question for the {role} position.
The question should assess the candidate's technical knowledge and problem-solving abilities.
"""
        elif current_step.lower() == "behavioral/situational":
            prompt += f"""

Begin the Behavioral section with a situational question relevant to the {role} position.
Ask about how they handled a specific professional situation or challenge.
"""
        else:
            prompt += f"""

Begin the {current_step} section with an appropriate question for the {role} position.
Keep the question clear, professional, and relevant.
"""
        
        return ask_llm(prompt)

