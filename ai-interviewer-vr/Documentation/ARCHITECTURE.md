# Project Architecture Overview

## Introduction
The AI Interviewer VR application is designed to simulate interview scenarios using a virtual reality environment. The application leverages Unity for development and integrates various components to facilitate voice-based interactions, speech analysis, and personalized feedback based on user responses and submitted resumes.

## Architecture Components

### 1. **Core**
- **AppInitializer.cs**: Responsible for initializing the application, setting up necessary components, and managing configurations at startup.
- **SceneController.cs**: Manages scene transitions and controls the overall flow of the application.

### 2. **Interview**
- **InterviewManager.cs**: Oversees the interview process, including managing questions and user interactions.
- **QuestionPipeline.cs**: Handles the flow of questions, fetching and presenting them to the user based on the role and resume.
- **FeedbackGenerator.cs**: Generates personalized feedback based on user responses during the interview.

### 3. **AI**
- **ResumeParser.cs**: Parses the submitted resume to extract relevant information for customizing interview questions.
- **RoleProfileLoader.cs**: Loads role-specific profiles that define the types of questions to be asked based on the job role.
- **LLMService.cs**: Interfaces with a language model service to generate questions and responses dynamically.

### 4. **Speech**
- **SpeechRecognizer.cs**: Converts voice input from the user into text for processing.
- **SpeechSynthesizer.cs**: Converts text responses into speech, enabling the AI interviewer to communicate with the user.
- **SentimentAnalyzer.cs**: Analyzes the user's speech for sentiment, providing insights into their emotional state during the interview.

### 5. **Interaction**
- **GazeInteractor.cs**: Manages gaze-based interactions within the VR environment, allowing users to interact naturally.
- **ControllerInputHandler.cs**: Handles input from VR controllers, facilitating user interactions with the application.

### 6. **UI**
- **HUDController.cs**: Manages heads-up display (HUD) elements, providing real-time information to the user during the interview.
- **TranscriptPanel.cs**: Displays a transcript of the conversation, allowing users to review their responses.

## Assets
- **Prefabs**: Contains prefabs for the AI interviewer's avatar and the user's rig.
- **Audio**: Includes audio files for voice and sound effects used in the application.
- **Models**: Contains 3D models for avatars.
- **Animations**: Houses animations for the AI interviewer's avatar.
- **Resources**: Contains role profiles and prompts used in the application.

## Conclusion
The architecture of the AI Interviewer VR application is designed to provide a seamless and interactive interview experience. By integrating various components such as AI, speech processing, and user interaction, the application aims to deliver personalized and engaging interview simulations.