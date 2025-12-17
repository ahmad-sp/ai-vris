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
                    "You are Jake, a professional interviewer specializing in LOGIC, PROBLEM-SOLVING, and ANALYTICAL REASONING. "
                    "Your focus is on testing the candidate's logical thinking, algorithmic reasoning, and problem-solving abilities. "
                    "Maintain a professional, composed, and encouraging tone throughout. "
                    "FOCUS AREAS: Logic puzzles, algorithmic thinking, pattern recognition, analytical reasoning, and problem-solving strategies. "
                    "CRITICAL OUTPUT RULES: "
                    "1. Output ONLY the spoken response/question. "
                    "2. Do NOT output internal thoughts, <think> tags, or reasoning. "
                    "3. Do NOT use prefixes like 'Interviewer:' or 'Jake:'. "
                    "4. Keep questions focused on LOGIC and PROBLEM-SOLVING. "
                    "5. Ask questions that test analytical thinking and reasoning abilities."
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
        
        # 🧹 CLEANUP: Remove <think>...</think> blocks if the model generates them
        content = re.sub(r'<think>.*?</think>', '', content, flags=re.DOTALL).strip()
        
        print(f"[LLM] Generated content length: {len(content)}")
        return content

    except Exception as e:
        print("LLM ERROR:", str(e))
        return "Let's continue with the next question."


def generate_interviewer_text(role, current_step, previous_answer=None, resume_summary=None):
    """Generate context-aware interviewer questions dynamically."""
    role_context = (
        f"You are Jake, a professional interviewer specializing in LOGIC and PROBLEM-SOLVING.\n"
        f"You are conducting a logic-focused interview for the position of **{role}**.\n"
        f"Current section: **{current_step}**.\n"
        "Your tone should be conversational, confident, and polite.\n"
        "Focus on LOGIC PUZZLES, ALGORITHMIC THINKING, PATTERN RECOGNITION, and ANALYTICAL REASONING.\n"
        "Ask questions that test problem-solving abilities, not generic behavioral or casual questions.\n"
        "IMPORTANT: Do NOT add any prefixes like 'Interviewer:', 'Jake:', or any role labels to your responses. "
        "Respond directly with the interview questions or statements only."
    )

    # 1. Greeting / Introduction
    if current_step.lower() in ["greeting", "introduction"] and not previous_answer:
        prompt = f"""
{role_context}

Start the interview by greeting the candidate warmly.
Then ask the first question to start the {current_step} section — ask about their interest in logic and problem-solving,
or ask them to solve a simple logic puzzle to warm up.
Keep it natural and friendly but focused on logical thinking.
IMPORTANT: Output only the greeting and question text, without any prefixes like 'Interviewer:' or 'Next question:'.
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
IMPORTANT: Output only the closing message, without any prefixes like 'Interviewer:' or 'Next question:'.
"""
        return ask_llm(prompt)

    # 💼 4. Resume Questions Section - FIRST QUESTION
    if current_step.lower() == "resume questions" and not previous_answer:
        if resume_summary:
            print(f"[LLMService] Generating FIRST resume question with summary (length: {len(resume_summary)})")
            prompt = f"""
{role_context}

Candidate Resume Summary:
{resume_summary}

You are starting the Resume Questions section. This is your FIRST question about their resume.

CRITICAL INSTRUCTIONS:
- Ask ONLY ONE question - not multiple questions
- Make it conversational and natural, like you're having a dialogue
- Pick ONE specific thing from their resume that interests you most
- Ask about it in a curious, engaging way
- DO NOT list multiple questions or use bullet points
- DO NOT ask "Can you tell me about X? And also Y? And Z?"
- Just ask about ONE thing that caught your attention

Examples of GOOD questions (pick ONE similar approach):
- "I noticed you worked with TensorFlow and PyTorch - what made you choose one over the other for your projects?"
- "Your VR Interview Simulator project sounds fascinating! Can you walk me through how you built that?"
- "I see you did an internship at Elevate Labs - what was the most challenging part of that experience?"

Examples of BAD questions (DO NOT DO THIS):
- "Can you tell me about your experience with X? Also, what about Y? And how did you handle Z?" (Multiple questions)
- Listing 5-10 questions all at once

Remember: ONE question only. Make it specific to their resume. Make it conversational.
IMPORTANT: Output only the question text, without any prefixes like 'Interviewer:' or 'Next question:'.
"""
            return ask_llm(prompt)
        else:
            print(f"[LLMService] ⚠️ Resume Questions step but no resume_summary provided!")
            # Fallback to generic question if resume summary is missing
            prompt = f"""
{role_context}

Ask ONE logic-focused question based on analytical thinking or problem-solving skills relevant to the {role} position.
For example: "Can you walk me through your approach to solving complex problems?" or "Tell me about a time you had to think logically under pressure."
Make it conversational and engaging.
IMPORTANT: Output only the question text, without any prefixes like 'Interviewer:' or 'Next question:'.
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

Your job:
- Appreciate their response briefly (1 short line only).
- Ask the next LOGIC-FOCUSED question that fits the {current_step} section.
- Focus on: logic puzzles, algorithms, pattern recognition, or analytical reasoning.
- Examples: "Here's a logic puzzle...", "How would you approach...", "What's your strategy for..."
- Keep it concise and related to logical thinking for the {role} role.
- Avoid repeating or generic questions.
"""
        
        prompt += """
IMPORTANT: Output only the response text, without any prefixes like 'Interviewer:' or 'Next question:'.
"""
        return ask_llm(prompt)
    
    # 💬 6. Initial question for other sections (not resume, not greeting)
    else:
        prompt = f"""
{role_context}
"""
        
        # This shouldn't happen for resume questions anymore (handled above)
        # But keep as fallback
        if current_step.lower() == "resume questions" and resume_summary:
            prompt += f"""

Candidate Resume Summary:
{resume_summary}

Ask ONE specific question about their resume. Pick something interesting from their background.
Make it conversational and engaging.
"""
        else:
            prompt += f"""

Start the {current_step} section by asking a LOGIC or PROBLEM-SOLVING question.
Examples:
- Logic puzzles: "If you have 3 switches and one light bulb in another room..."
- Algorithmic thinking: "How would you find the missing number in a sequence?"
- Pattern recognition: "What comes next in this pattern: 2, 4, 8, 16...?"
- Analytical reasoning: "How would you optimize a search algorithm?"
Keep it short, natural, and clear — not robotic.
"""
        
        prompt += """
IMPORTANT: Output only the question text, without any prefixes like 'Interviewer:' or 'Next question:'.
"""
        
        return ask_llm(prompt)

