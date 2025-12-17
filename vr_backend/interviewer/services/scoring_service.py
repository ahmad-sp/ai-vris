import os
import json
import re
from groq import Groq

GROQ_API_KEY = os.getenv("GROQ_API_KEY")


def extract_json_from_response(response_text):
    """
    Extract JSON from LLM response that may contain think tags or other content.
    """
    if not response_text:
        return None
    
    # Remove <think>...</think> blocks (complete)
    cleaned = re.sub(r'<think>.*?</think>', '', response_text, flags=re.DOTALL | re.IGNORECASE)
    
    # Remove incomplete <think> blocks (no closing tag - the model cut off)
    cleaned = re.sub(r'<think>.*$', '', cleaned, flags=re.DOTALL | re.IGNORECASE)
    
    # Remove any other XML-like tags
    cleaned = re.sub(r'<[^>]+>.*?</[^>]+>', '', cleaned, flags=re.DOTALL)
    cleaned = re.sub(r'<[^>]+/?>', '', cleaned)
    cleaned = cleaned.strip()
    
    # Try to find JSON with both relevance and score
    json_patterns = [
        r'\{\s*"relevance"\s*:\s*"[^"]+"\s*,\s*"score"\s*:\s*\d+\s*\}',
        r'\{\s*"score"\s*:\s*\d+\s*,\s*"relevance"\s*:\s*"[^"]+"\s*\}',
        r'\{[^{}]*"relevance"[^{}]*"score"[^{}]*\}',
        r'\{[^{}]*"score"[^{}]*"relevance"[^{}]*\}',
        r'\{[^{}]*\}',
    ]
    
    for pattern in json_patterns:
        match = re.search(pattern, cleaned, re.DOTALL | re.IGNORECASE)
        if match:
            try:
                # Try to parse it
                json.loads(match.group(0))
                return match.group(0)
            except:
                continue
    
    # Also search in original text (in case think block wasn't properly closed)
    for pattern in json_patterns:
        match = re.search(pattern, response_text, re.DOTALL | re.IGNORECASE)
        if match:
            try:
                json.loads(match.group(0))
                return match.group(0)
            except:
                continue
    
    return None


def extract_score_from_text(response_text):
    """Try to extract score and relevance from unstructured text."""
    text_lower = response_text.lower()
    
    # Determine relevance
    relevance = "Relevant"  # Default to relevant
    if 'irrelevant' in text_lower and 'not irrelevant' not in text_lower:
        relevance = "Irrelevant"
    elif 'partially relevant' in text_lower or 'partial' in text_lower:
        relevance = "Partially Relevant"
    
    # Try to find score
    score_patterns = [
        r'score["\s:]+(\d+)',
        r'(\d+)\s*/\s*10',
        r'(\d+)\s*out of\s*10',
        r'rating["\s:]+(\d+)',
    ]
    
    for pattern in score_patterns:
        match = re.search(pattern, text_lower)
        if match:
            score = int(match.group(1))
            if 0 <= score <= 10:
                return score, relevance
    
    return None, relevance


def score_answer(question, answer):
    """
    Returns a tuple: (score:int, relevance:str)
    Uses LLM to evaluate both relevance and quality of the answer.
    """
    # Only pre-check for empty/very short answers
    if not answer or len(answer.strip()) < 2:
        return 0, "Irrelevant"
    
    # Check for single filler words only
    filler_words = ["um", "uh", "hmm"]
    cleaned_answer = answer.strip().lower().replace(",", "").replace(".", "").replace("!", "").replace("?", "")
    if cleaned_answer in filler_words:
        return 1, "Irrelevant"
    
    print(f"[Scoring] Scoring answer: '{answer[:50]}...' to question: '{question[:50]}...'")

    # Simpler, more direct prompt
    system_prompt = """You are an interview evaluator. Return ONLY a JSON object, nothing else.

Evaluate:
- relevance: "Relevant" (addresses question), "Partially Relevant" (somewhat), or "Irrelevant" (off-topic)
- score: 0-10 (7-10=strong with details, 5-6=adequate, 3-4=weak, 0-2=irrelevant)

Example output: {"relevance": "Relevant", "score": 8}

Return ONLY the JSON object. No explanation."""

    try:
        client = Groq(api_key=GROQ_API_KEY)
        
        # Try with llama model first (less likely to use think tags)
        models_to_try = ["llama-3.3-70b-versatile", "qwen/qwen3-32b"]
        
        for model in models_to_try:
            try:
                print(f"[Scoring] Trying model: {model}")
                
                messages = [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": f"Q: {question}\nA: {answer}\n\nJSON:"}
                ]
                
                completion = client.chat.completions.create(
                    model=model,
                    messages=messages,
                    temperature=0.1,
                    max_completion_tokens=50,  # Very short - just need JSON
                    top_p=1,
                    stream=False,
                    stop=None
                )
                
                response_text = completion.choices[0].message.content.strip()
                print(f"[Scoring] Raw API Response ({model}): {response_text[:200]}")
                
                # Try to extract JSON
                json_str = extract_json_from_response(response_text)
                
                if json_str:
                    print(f"[Scoring] Extracted JSON: {json_str}")
                    result_json = json.loads(json_str)
                    relevance = result_json.get("relevance", "Relevant")
                    score = int(result_json.get("score", 6))
                    score = max(0, min(10, score))
                    
                    # Enforce score consistency with relevance
                    if relevance == "Irrelevant" and score > 2:
                        score = 1
                    elif relevance == "Relevant" and score < 5:
                        score = 5
                    
                    print(f"[Scoring] Final Score: {score}, Relevance: {relevance}")
                    return score, relevance
                
                # Try extracting from text
                score, relevance = extract_score_from_text(response_text)
                if score is not None:
                    print(f"[Scoring] Extracted from text: Score={score}, Relevance={relevance}")
                    return score, relevance
                    
            except Exception as model_error:
                print(f"[Scoring] Model {model} failed: {model_error}")
                continue
        
        # All models failed - use smart fallback
        print(f"[Scoring] All models failed, using smart fallback")
        return smart_fallback_score(answer)
        
    except Exception as e:
        print(f"[Scoring] LLM error: {str(e)}")
        return smart_fallback_score(answer)


def smart_fallback_score(answer):
    """
    Smart fallback scoring based on answer characteristics.
    Used when LLM calls fail.
    """
    word_count = len(answer.split())
    answer_lower = answer.lower()
    
    # Check for obvious off-topic indicators
    off_topic_phrases = ["pizza", "food", "eat", "doctor", "hospital", "sick", "weather", "movie", "netflix"]
    professional_phrases = ["project", "developed", "built", "worked", "experience", "team", "technology", 
                           "python", "java", "machine learning", "data", "api", "software", "design"]
    
    off_topic_count = sum(1 for phrase in off_topic_phrases if phrase in answer_lower)
    professional_count = sum(1 for phrase in professional_phrases if phrase in answer_lower)
    
    # If clearly off-topic
    if off_topic_count > 2 and professional_count == 0:
        return 1, "Irrelevant"
    
    # If has professional content
    if professional_count > 0:
        if word_count > 30:
            return 7, "Relevant"
        elif word_count > 15:
            return 6, "Relevant"
        else:
            return 5, "Relevant"
    
    # Generic answer
    if word_count > 20:
        return 6, "Relevant"
    elif word_count > 10:
        return 5, "Partially Relevant"
    else:
        return 4, "Partially Relevant"