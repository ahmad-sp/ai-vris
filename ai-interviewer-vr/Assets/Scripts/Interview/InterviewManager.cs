using UnityEngine;
using System.Collections.Generic;

public class InterviewManager : MonoBehaviour
{
    private List<string> questions;
    private int currentQuestionIndex;

    void Start()
    {
        InitializeInterview();
    }

    private void InitializeInterview()
    {
        questions = LoadQuestions();
        currentQuestionIndex = 0;
        AskNextQuestion();
    }

    private List<string> LoadQuestions()
    {
        // Load questions from a predefined source (e.g., JSON file, database)
        return new List<string>
        {
            "Tell me about yourself.",
            "What are your strengths and weaknesses?",
            "Why do you want to work here?",
            "Describe a challenging situation you faced and how you handled it."
        };
    }

    private void AskNextQuestion()
    {
        if (currentQuestionIndex < questions.Count)
        {
            string question = questions[currentQuestionIndex];
            // Trigger speech synthesizer to ask the question
            Debug.Log("Asking question: " + question);
            currentQuestionIndex++;
        }
        else
        {
            EndInterview();
        }
    }

    private void EndInterview()
    {
        // Provide feedback and end the interview
        Debug.Log("Interview completed. Thank you for your responses.");
    }

    public void ReceiveUserResponse(string response)
    {
        // Process the user's response and provide feedback
        Debug.Log("User response: " + response);
        // Call method to analyze response and generate feedback
        AskNextQuestion();
    }
}