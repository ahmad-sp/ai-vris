import os
import google.generativeai as genai
from dotenv import load_dotenv

load_dotenv()

GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
if not GEMINI_API_KEY:
    print("GEMINI_API_KEY not found in environment variables.")
    exit(1)

genai.configure(api_key=GEMINI_API_KEY)

def test_model(model_name):
    print(f"Testing model: {model_name}")
    try:
        model = genai.GenerativeModel(model_name)
        response = model.generate_content("Hello, are you working?")
        print(f"Success! Response: {response.text}")
        return True
    except Exception as e:
        print(f"Failed: {e}")
        return False

print("--- Verifying Gemini Models ---")
test_model('models/gemini-2.5-flash')
test_model('gemini-2.5-flash')
test_model('models/gemini-1.5-flash')
test_model('gemini-1.5-flash')
