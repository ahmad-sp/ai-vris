using UnityEngine;

public class FeedbackGenerator : MonoBehaviour
{
    public string GenerateFeedback(string userResponse, string expectedResponse)
    {
        // Basic feedback generation logic
        if (userResponse.Equals(expectedResponse, System.StringComparison.OrdinalIgnoreCase))
        {
            return "Great job! Your response was exactly what we were looking for.";
        }
        else if (userResponse.Length < expectedResponse.Length / 2)
        {
            return "Your response was quite brief. Consider elaborating more on your thoughts.";
        }
        else
        {
            return "Good effort! However, try to align your response more closely with the expected answer.";
        }
    }

    public void ProvidePersonalizedFeedback(string userResponse, string expectedResponse)
    {
        string feedback = GenerateFeedback(userResponse, expectedResponse);
        Debug.Log(feedback);
        // Additional logic to display feedback in the VR environment can be added here
    }
}