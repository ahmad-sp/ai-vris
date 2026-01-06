import os
from groq import Groq
from PyPDF2 import PdfReader
import io

GROQ_API_KEY = os.getenv("GROQ_API_KEY")


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


def parse_resume_with_groq(resume_text, role):
    """Use Groq to extract and summarize resume details based on role."""
    try:
        if not GROQ_API_KEY:
            print("Groq parsing error: GROQ_API_KEY is not set")
            return None

        client = Groq(api_key=GROQ_API_KEY)

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
""".strip()

        completion = client.chat.completions.create(
            model="llama-3.1-8b-instant",
            messages=[
                {
                    "role": "system",
                    "content": "You are a precise resume analyzer. Output only the requested summary.",
                },
                {"role": "user", "content": prompt},
            ],
            temperature=0.2,
        )

        content = (completion.choices[0].message.content or "").strip()
        return content or None

    except Exception as e:
        print(f"Groq parsing error: {str(e)}")
        return None


def process_resume_upload(pdf_file, role):
    """Complete pipeline: PDF -> Text extraction -> Groq parsing -> Summary."""
    try:
        print(f"[ResumeParser] Starting resume processing for role: {role}")
        
        # Step 1: Extract text from PDF
        print("[ResumeParser] Step 1: Extracting text from PDF...")
        resume_text = extract_text_from_pdf(pdf_file)
        if not resume_text:
            print("[ResumeParser] ERROR: Failed to extract text from PDF")
            return {"error": "Failed to extract text from PDF"}
        
        print(f"[ResumeParser] Successfully extracted {len(resume_text)} characters from PDF")
        
        # Step 2: Parse and summarize with Groq
        print("[ResumeParser] Step 2: Parsing with Groq...")
        resume_summary = parse_resume_with_groq(resume_text, role)
        if not resume_summary:
            print("[ResumeParser] ERROR: Failed to parse resume with Groq")
            return {"error": "Failed to parse resume with Groq"}
        
        print(f"[ResumeParser] Successfully generated summary with {len(resume_summary)} characters")
        
        return {
            "success": True,
            "raw_text": resume_text,
            "summary": resume_summary,
            "role": role
        }
        
    except Exception as e:
        print(f"[ResumeParser] ERROR: Resume processing error: {str(e)}")
        return {"error": f"Resume processing failed: {str(e)}"}
