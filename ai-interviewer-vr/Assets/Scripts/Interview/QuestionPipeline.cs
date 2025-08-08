using System.Collections.Generic;
using UnityEngine;

namespace Interview
{
    public class QuestionPipeline : MonoBehaviour
    {
        private List<string> questions;
        private int currentQuestionIndex;

        void Start()
        {
            questions = new List<string>();
            LoadQuestions();
            currentQuestionIndex = 0;
        }

        private void LoadQuestions()
        {
            // Load questions from a data source (e.g., JSON file, database)
            // For now, we will use hardcoded questions for demonstration
            questions.Add("What is your greatest strength?");
            questions.Add("Can you describe a challenging situation you faced at work?");
            questions.Add("Why do you want to work for this company?");
            questions.Add("Where do you see yourself in five years?");
        }

        public string GetNextQuestion()
        {
            if (currentQuestionIndex < questions.Count)
            {
                return questions[currentQuestionIndex++];
            }
            else
            {
                return null; // No more questions
            }
        }

        public void ResetQuestions()
        {
            currentQuestionIndex = 0;
        }
    }
}