# AI Pipeline Documentation

## Overview
The AI pipeline in the mobile VR application is designed to facilitate dynamic interview scenarios by leveraging advanced AI techniques. This pipeline is responsible for generating personalized interview questions based on the user's resume and the specific job role they are applying for. Additionally, it provides feedback based on the user's responses during the interview.

## Components

### 1. Resume Parsing
- **File:** `Assets/Scripts/AI/ResumeParser.cs`
- **Functionality:** This component extracts relevant information from the user's submitted resume. It identifies key skills, experiences, and qualifications that are crucial for tailoring the interview questions.

### 2. Role Profile Loading
- **File:** `Assets/Scripts/AI/RoleProfileLoader.cs`
- **Functionality:** This component loads role-specific profiles that define the types of questions to be asked. It utilizes JSON files located in `Assets/Resources/RoleProfiles/` to fetch the appropriate question sets based on the job role.

### 3. Question Generation
- **File:** `Assets/Scripts/Interview/QuestionPipeline.cs`
- **Functionality:** This component manages the flow of questions during the interview. It fetches questions from the role profile and presents them to the user in a structured manner.

### 4. Language Model Service
- **File:** `Assets/Scripts/AI/LLMService.cs`
- **Functionality:** This component interfaces with a language model service to generate additional questions and responses. It enhances the interview experience by providing contextually relevant follow-up questions based on user responses.

### 5. Feedback Generation
- **File:** `Assets/Scripts/Interview/FeedbackGenerator.cs`
- **Functionality:** This component analyzes user responses and generates personalized feedback. It assesses the quality of answers and provides insights to help users improve their interview skills.

## Workflow
1. **Resume Submission:** The user submits their resume through the application.
2. **Parsing:** The `ResumeParser` extracts relevant information from the resume.
3. **Profile Loading:** The `RoleProfileLoader` retrieves the appropriate question set based on the job role.
4. **Question Presentation:** The `QuestionPipeline` presents questions to the user during the interview.
5. **Response Analysis:** User responses are analyzed by the `FeedbackGenerator`, which provides real-time feedback.
6. **Continuous Improvement:** The AI system learns from user interactions to improve future question generation and feedback accuracy.

## Conclusion
The AI pipeline is a critical component of the mobile VR application, enabling a personalized and interactive interview experience. By integrating resume parsing, role-specific question generation, and feedback mechanisms, the application aims to enhance the user's interview preparation and performance.