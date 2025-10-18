import requests
from django.conf import settings
import json

OPENROUTER_URL = "https://openrouter.ai/v1/chat/completions"

def score_answer(question, answer):
    """
    Returns a tuple: (score:int, relevance:str)
    """
    headers = {
        "Authorization": f"Bearer {settings.OPENROUTER_API_KEY}",
        "Content-Type": "application/json",
    }

    system_prompt = """
    You are a professional interview evaluator.
    Evaluate the candidate's answer for both quality and relevance.

    1) Relevance: Does the answer address the question? (Relevant / Irrelevant)
    2) Score: 1-5 (5 = excellent, 1 = very poor)

    ONLY RETURN JSON:
    {
        "relevance": "<Relevant|Irrelevant>",
        "score": <int>
    }
    """

    payload = {
        "model": "gpt-4o-mini",
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": f"Question: {question}\nAnswer: {answer}"}
        ]
    }

    try:
        response = requests.post(OPENROUTER_URL, headers=headers, json=payload, timeout=30)
        response.raise_for_status()
        data = response.json()
        result_json = json.loads(data["choices"][0]["message"]["content"].strip())
        relevance = result_json.get("relevance", "Relevant")
        score = int(result_json.get("score", 3))
        return score, relevance
    except Exception as e:
        print("Scoring LLM error:", str(e))
        return 3, "Relevant"