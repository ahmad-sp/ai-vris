import os
import json
from groq import Groq

GROQ_API_KEY = os.getenv("GROQ_API_KEY")


def score_answer(question, answer):
    """
    Returns a tuple: (score:int, relevance:str)
    """
    # Quick pre-check for obviously bad answers ONLY
    if not answer or len(answer.strip()) < 3:
        return 0, "Irrelevant"
    
    # Check for filler-only answers (very strict check)
    filler_words = ["um", "uh", "hmm"]  # Only check for pure filler, not legitimate answers
    cleaned_answer = answer.strip().lower().replace(",", "").replace(".", "")
    if cleaned_answer in filler_words and len(cleaned_answer.split()) == 1:
        return 0, "Irrelevant"
    
    print(f"[Scoring] Scoring answer: '{answer[:50]}...' to question: '{question[:50]}...'")

    system_prompt = """
    You are a professional interview evaluator. Rate each answer based on:
    1) Relevance — does the answer address the question directly? (Relevant / Irrelevant)
    2) Score — integer 0-10:
       - 0: No answer, just filler words (um, uh, etc.), or completely off-topic
       - 1-2: Very poor answer, minimal substance, barely addresses question
       - 3-4: Poor answer, some substance but lacks depth or clarity
       - 5-6: Average answer, addresses question but could be better
       - 7-8: Good answer, clear and relevant with decent substance
       - 9-10: Excellent answer, comprehensive, insightful, and well-articulated

    SCORING GUIDELINES:
    - If the answer directly addresses the question asked, mark it "Relevant"
    - If the answer provides specific examples, projects, or experience, score 7-8
    - If the answer shows education and internship experience, score 7-8
    - Only mark "Irrelevant" if the answer is completely off-topic or is just filler words
    - Only give 0-2 for answers that are truly minimal, off-topic, or just filler
    - Give 5-6 for basic answers that address the question
    - Give 7-8 for good answers with specific details and examples
    - Give 9-10 for exceptional, comprehensive answers

    IMPORTANT: Be generous with scoring for relevant, detailed answers.

    ONLY RETURN JSON:
    {
        "relevance": "Relevant/Irrelevant",
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
        return 0, "Irrelevant"