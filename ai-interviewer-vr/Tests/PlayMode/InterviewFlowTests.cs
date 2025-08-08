using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;

public class InterviewFlowTests
{
    [UnityTest]
    public IEnumerator TestInterviewFlow()
    {
        // Arrange
        var interviewManager = new InterviewManager();
        interviewManager.StartInterview("software_engineer");

        // Act
        yield return new WaitForSeconds(1); // Simulate waiting for the first question

        // Assert
        Assert.IsNotNull(interviewManager.CurrentQuestion);
        Assert.AreEqual("What is your experience with software development?", interviewManager.CurrentQuestion.Text);
    }

    [UnityTest]
    public IEnumerator TestFeedbackGeneration()
    {
        // Arrange
        var feedbackGenerator = new FeedbackGenerator();
        var userResponse = "I have worked on several projects using C# and Unity.";

        // Act
        var feedback = feedbackGenerator.GenerateFeedback(userResponse);

        // Assert
        Assert.IsNotNull(feedback);
        Assert.IsTrue(feedback.Contains("good experience"));
    }

    [UnityTest]
    public IEnumerator TestRoleProfileLoading()
    {
        // Arrange
        var roleProfileLoader = new RoleProfileLoader();
        roleProfileLoader.LoadProfile("software_engineer");

        // Act
        yield return new WaitForSeconds(1); // Simulate loading time

        // Assert
        Assert.IsNotNull(roleProfileLoader.Questions);
        Assert.IsTrue(roleProfileLoader.Questions.Count > 0);
    }
}