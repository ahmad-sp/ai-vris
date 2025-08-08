using NUnit.Framework;

[TestFixture]
public class ResumeParserTests
{
    private ResumeParser _resumeParser;

    [SetUp]
    public void Setup()
    {
        _resumeParser = new ResumeParser();
    }

    [Test]
    public void Parse_ValidResume_ReturnsExpectedData()
    {
        // Arrange
        string resumeText = "John Doe\nSoftware Engineer\nExperience: 5 years\nSkills: C#, Unity, VR Development";
        
        // Act
        var result = _resumeParser.Parse(resumeText);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("John Doe", result.Name);
        Assert.AreEqual("Software Engineer", result.JobTitle);
        Assert.AreEqual(5, result.ExperienceYears);
        Assert.Contains("C#", result.Skills);
        Assert.Contains("Unity", result.Skills);
        Assert.Contains("VR Development", result.Skills);
    }

    [Test]
    public void Parse_EmptyResume_ReturnsNull()
    {
        // Arrange
        string resumeText = "";
        
        // Act
        var result = _resumeParser.Parse(resumeText);
        
        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public void Parse_InvalidResumeFormat_ReturnsNull()
    {
        // Arrange
        string resumeText = "Invalid format text";
        
        // Act
        var result = _resumeParser.Parse(resumeText);
        
        // Assert
        Assert.IsNull(result);
    }
}