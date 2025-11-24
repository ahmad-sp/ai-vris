import os
import google.generativeai as genai
from django.conf import settings
from PyPDF2 import PdfReader
import io

GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
genai.configure(api_key=GEMINI_API_KEY)


def extract_text_from_pdf(pdf_file):
    """Extract text from PDF file using PyPDF2."""
    try:
        pdf_reader = PdfReader(io.BytesIO(pdf_file.read()))
        text = ""
        for page in pdf_reader.pages:
            text += page.extract_text()
        return text
    except Exception as e:
        print(f"PDF extraction error: {str(e)}")
        return None


def parse_resume_with_gemini(resume_text, role):
    """Use Gemini to extract and summarize resume details based on role."""
    try:
        model = genai.GenerativeModel('gemini-2.0-flash')
        
        prompt = f"""
        You are an expert resume analyzer. Analyze the following resume and extract key information 
        relevant to the role of {role}. 
        
        Resume text:
        {resume_text}
        
        Please provide a structured summary including:
        1. Key skills and technical proficiencies
        2. Work experience relevant to {role}
        3. Educational background
        4. Notable achievements or projects
        5. Areas of expertise that match {role} requirements
        
        Format the response as a concise summary that will help generate relevant interview questions.
        Focus on information most relevant to the {role} position.
        """
        
        response = model.generate_content(prompt)
        return response.text.strip()
        
    except Exception as e:
        print(f"Gemini parsing error: {str(e)}")
        return None


def process_resume_upload(pdf_file, role):
    """Complete pipeline: PDF -> Text extraction -> Gemini parsing -> Summary."""
    try:
        # Step 1: Extract text from PDF
        resume_text = extract_text_from_pdf(pdf_file)
        if not resume_text:
            return {"error": "Failed to extract text from PDF"}
        
        # Step 2: Parse and summarize with Gemini
        resume_summary = parse_resume_with_gemini(resume_text, role)
        if not resume_summary:
            return {"error": "Failed to parse resume with Gemini"}
        
        return {
            "success": True,
            "raw_text": resume_text,
            "summary": resume_summary,
            "role": role
        }
        
    except Exception as e:
        print(f"Resume processing error: {str(e)}")
        return {"error": f"Resume processing failed: {str(e)}"}
