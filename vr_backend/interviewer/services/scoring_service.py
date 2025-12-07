import requests
from django.conf import settings
import json

OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"

def score_answer(question, answer):
    """
    Returns a tuple: (score:int, relevance:str)
    """
    headers = {
        "Authorization": f"Bearer {settings.OPENROUTER_API_KEY}",
        "Content-Type": "application/json",
    }

    system_prompt = """
    You are a professional interview evaluator. Rate each answer based on:

    1) Relevance — does the answer address the question directly? (Relevant / Irrelevant)
    2) Score — integer 0-10 (10 = outstanding, 5 = average, 0 = no answer or off-topic ,score in a scale of 0 to 10).

    ONLY RETURN JSON:
    {
        "relevance": "<Relevant|Irrelevant>",
        "score": <int from 0-10>
    }
    """

    payload = {
        "model": "google/gemma-3-27b-it:free",
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": f"Question: {question}\nAnswer: {answer}"}
        ],
        "max_tokens": 200,
    }

    try:
        response = requests.post(OPENROUTER_URL, headers=headers, json=payload, timeout=30)
        response.raise_for_status()
        data = response.json()
        result_json = json.loads(data["choices"][0]["message"]["content"].strip())
        relevance = result_json.get("relevance", "Relevant")
        score = int(result_json.get("score", 5))
        score = max(0, min(10, score))
        return score, relevance
    except Exception as e:
        print("Scoring LLM error:", str(e))
        return 3, "Relevant"