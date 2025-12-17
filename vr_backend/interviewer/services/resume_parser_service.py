import os
import json
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


def verify_is_resume(text):
    """
    Use Gemini to verify if the extracted text is actually a resume.
    Returns a dict with 'is_resume' (bool), 'confidence' (float), and 'reason' (str).
    """
    try:
        model = genai.GenerativeModel('models/gemini-2.5-flash')
        
        # Use only first 3000 characters to speed up verification
        sample_text = text[:3000] if len(text) > 3000 else text
        
        prompt = f"""
        You are a document classifier. Analyze the following text extracted from a PDF and determine if it is a resume/CV.

        Text to analyze:
        {sample_text}

        A resume/CV typically contains:
        - Personal information (name, contact details, email, phone)
        - Work experience or employment history
        - Education background
        - Skills or technical proficiencies
        - Sometimes: projects, certifications, achievements

        Documents that are NOT resumes include:
        - Academic papers or research articles
        - Business documents or reports
        - Invoices or receipts
        - Random text or unrelated documents
        - Cover letters (these are related but NOT resumes)
        - Marketing materials or brochures

        Respond ONLY with a valid JSON object in this exact format (no markdown, no code blocks):
        {{"is_resume": true/false, "confidence": 0.0-1.0, "reason": "brief explanation"}}
        """
        
        response = model.generate_content(prompt)
        response_text = response.text.strip()
        
        # Clean up response if it contains markdown code blocks
        if response_text.startswith("```"):
            lines = response_text.split("\n")
            response_text = "\n".join(lines[1:-1])
        
        print(f"[ResumeVerify] Raw response: {response_text}")
        
        result = json.loads(response_text)
        
        return {
            "is_resume": result.get("is_resume", False),
            "confidence": result.get("confidence", 0.0),
            "reason": result.get("reason", "Unknown")
        }
        
    except json.JSONDecodeError as e:
        print(f"[ResumeVerify] JSON parsing error: {str(e)}")
        # If we can't parse the response, default to checking for common resume keywords
        # as a fallback
        resume_keywords = ["experience", "education", "skills", "email", "phone", "work", "employment"]
        found_keywords = sum(1 for kw in resume_keywords if kw.lower() in text.lower())
        
        if found_keywords >= 3:
            return {
                "is_resume": True,
                "confidence": 0.6,
                "reason": "Fallback: Found multiple resume-related keywords"
            }
        return {
            "is_resume": False,
            "confidence": 0.5,
            "reason": "Could not verify document type - insufficient resume indicators"
        }
    except Exception as e:
        print(f"[ResumeVerify] Verification error: {str(e)}")
        return {
            "is_resume": False,
            "confidence": 0.0,
            "reason": f"Verification failed: {str(e)}"
        }


def parse_resume_with_gemini(resume_text, role):
    """Use Gemini to extract and summarize resume details based on role."""
    try:
        model = genai.GenerativeModel('models/gemini-2.5-flash')
        
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
        Keep the summary under 500 words.
        """
        
        response = model.generate_content(prompt)
        return response.text.strip()
        
    except Exception as e:
        print(f"Gemini parsing error: {str(e)}")
        return None


def process_resume_upload(pdf_file, role):
    """Complete pipeline: PDF -> Text extraction -> Verification -> Gemini parsing -> Summary."""
    try:
        print(f"[ResumeParser] Starting resume processing for role: {role}")
        
        # Step 1: Extract text from PDF
        print("[ResumeParser] Step 1: Extracting text from PDF...")
        resume_text = extract_text_from_pdf(pdf_file)
        if not resume_text:
            print("[ResumeParser] ERROR: Failed to extract text from PDF")
            return {"error": "Failed to extract text from PDF"}
        
        print(f"[ResumeParser] Successfully extracted {len(resume_text)} characters from PDF")
        
        # Step 2: Verify the document is actually a resume
        print("[ResumeParser] Step 2: Verifying document is a resume...")
        verification = verify_is_resume(resume_text)
        print(f"[ResumeParser] Verification result: is_resume={verification['is_resume']}, confidence={verification['confidence']}, reason={verification['reason']}")
        
        if not verification['is_resume']:
            print(f"[ResumeParser] ERROR: Document is not a resume - {verification['reason']}")
            return {
                "error": f"The uploaded file does not appear to be a resume. {verification['reason']}",
                "is_invalid_document": True,
                "verification": verification
            }
        
        # Low confidence warning (but still proceed)
        if verification['confidence'] < 0.7:
            print(f"[ResumeParser] WARNING: Low confidence resume detection ({verification['confidence']})")
        
        # Step 3: Parse and summarize with Gemini
        print("[ResumeParser] Step 3: Parsing with Gemini...")
        resume_summary = parse_resume_with_gemini(resume_text, role)
        if not resume_summary:
            print("[ResumeParser] ERROR: Failed to parse resume with Gemini")
            return {"error": "Failed to parse resume with Gemini"}
        
        print(f"[ResumeParser] Successfully generated summary with {len(resume_summary)} characters")
        
        return {
            "success": True,
            "raw_text": resume_text,
            "summary": resume_summary,
            "role": role,
            "verification": verification
        }
        
    except Exception as e:
        print(f"[ResumeParser] ERROR: Resume processing error: {str(e)}")
        return {"error": f"Resume processing failed: {str(e)}"}
