import os
import json
from groq import Groq

GROQ_API_KEY = os.getenv("GROQ_API_KEY")


def score_answer(question, answer):
    """
    Returns a tuple: (score:int, relevance:str)
    """
    # Quick pre-check for obviously bad answers ONLY
    if not answer or len(answer.strip()) < 2:
        return 0, "Irrelevant"
    
    # Check for filler-only answers (very strict check)
    filler_words = ["um", "uh", "hmm"]  # Only check for pure filler, not legitimate answers
    cleaned_answer = answer.strip().lower().replace(",", "").replace(".", "")
    if cleaned_answer in filler_words and len(cleaned_answer.split()) == 1:
        return 1, "Partially Relevant"  # More generous - give 1 point for attempting
    
    print(f"[Scoring] Scoring answer: '{answer[:50]}...' to question: '{question[:50]}...'")

    system_prompt = """
    You are a GENEROUS and SUPPORTIVE interview evaluator. Your role is to encourage candidates and recognize their efforts.
    
    Rate each answer based on:
    1) Relevance — does the answer attempt to address the question? (Relevant / Partially Relevant / Irrelevant)
    2) Score — integer 0-10:
       - 0-1: Only for completely blank, nonsensical, or pure filler responses
       - 2-3: Very brief but attempts to answer (e.g., "I don't know much about that")
       - 4-5: Short answer that partially addresses the question
       - 6-7: Decent answer that addresses the question adequately
       - 8-9: Good answer with some detail, examples, or clarity
       - 10: Exceptional, comprehensive, and insightful answer

    LIBERAL SCORING GUIDELINES (BE GENEROUS):
    - If the answer makes ANY attempt to address the question, mark it "Relevant" or "Partially Relevant"
    - Even brief answers (1-2 sentences) should get at least 4-5 if they're on topic
    - Answers with any specific details, examples, or personal experience should get 6-8
    - Education, internships, projects, or work experience mentioned = automatic 7-8 minimum
    - Only mark "Irrelevant" if the answer is COMPLETELY off-topic or just filler words
    - Give the benefit of the doubt - if unsure, score higher
    - Partial answers are better than no answers - reward attempts (minimum 3-4)
    - Any answer showing thought or effort deserves at least 5-6
    - Reserve 0-2 ONLY for truly empty, nonsensical, or pure filler responses
    
    IMPORTANT RULES:
    - Default to scoring 6-7 for most reasonable answers
    - Be lenient with brevity - short answers can still be good
    - Reward any specificity or personal examples with 7-9
    - Only give low scores (0-3) for truly poor responses
    - When in doubt, score HIGHER not lower
    - Recognize that candidates may be nervous - be supportive

    ONLY RETURN JSON:
    {
        "relevance": "Relevant/Partially Relevant/Irrelevant",
        "score": 0-10
    }
    """

    try:
        client = Groq(api_key=GROQ_API_KEY)
        
        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": f"Question: {question}\nAnswer: {answer}"}
        ]
        
        print(f"[Scoring] Sending request to Groq with model: llama-3.1-8b-instant")
        
        completion = client.chat.completions.create(
            model="llama-3.1-8b-instant",
            messages=messages,
            temperature=1,
            max_completion_tokens=1024,
            top_p=1,
            stream=False,
            stop=None
        )
        
        response_text = completion.choices[0].message.content.strip()
        print(f"[Scoring] API Response: {response_text[:200]}...")
        
        result_json = json.loads(response_text)
        relevance = result_json.get("relevance", "Irrelevant")
        score = int(result_json.get("score", 0))
        score = max(0, min(10, score))
        
        print(f"[Scoring] Final Score: {score}, Relevance: {relevance}")
        return score, relevance
    except Exception as e:
        print("Scoring LLM error:", str(e))
        # More generous fallback - if there's an answer with substance, give benefit of doubt
        if answer and len(answer.strip()) > 10:
            print("[Scoring] LLM failed but answer has substance, giving benefit of doubt with score 5")
            return 5, "Relevant"  # Give average score instead of 0
        return 0, "Irrelevant"