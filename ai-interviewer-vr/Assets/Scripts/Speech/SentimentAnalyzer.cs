using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Speech
{
    public class SentimentAnalyzer : MonoBehaviour
    {
        private Dictionary<string, float> sentimentScores;

        void Start()
        {
            sentimentScores = new Dictionary<string, float>
            {
                { "positive", 1.0f },
                { "neutral", 0.0f },
                { "negative", -1.0f }
            };
        }

        public string AnalyzeSentiment(string userInput)
        {
            // Simple sentiment analysis based on keywords
            if (string.IsNullOrEmpty(userInput))
            {
                return "neutral";
            }

            var positiveWords = new List<string> { "good", "great", "excellent", "happy", "love" };
            var negativeWords = new List<string> { "bad", "terrible", "hate", "sad", "angry" };

            float score = 0;

            var words = userInput.ToLower().Split(' ');

            foreach (var word in words)
            {
                if (positiveWords.Contains(word))
                {
                    score += sentimentScores["positive"];
                }
                else if (negativeWords.Contains(word))
                {
                    score += sentimentScores["negative"];
                }
            }

            if (score > 0)
            {
                return "positive";
            }
            else if (score < 0)
            {
                return "negative";
            }
            else
            {
                return "neutral";
            }
        }
    }
}