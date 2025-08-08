using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

public class ResumeParser
{
    public Dictionary<string, string> ParseResume(string resumeFilePath)
    {
        if (!File.Exists(resumeFilePath))
        {
            throw new FileNotFoundException("Resume file not found.", resumeFilePath);
        }

        string resumeContent = File.ReadAllText(resumeFilePath);
        return ExtractInformation(resumeContent);
    }

    private Dictionary<string, string> ExtractInformation(string resumeContent)
    {
        var extractedData = new Dictionary<string, string>();

        // Example parsing logic (this should be replaced with actual parsing logic)
        // Here we assume the resume is in a simple key:value format
        var lines = resumeContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ':' }, 2);
            if (parts.Length == 2)
            {
                extractedData[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return extractedData;
    }
}