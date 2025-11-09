using Xunit;
using Aura.Core.Templates;
using System;

namespace Aura.Tests;

public class ScriptTemplatesTests
{
    [Theory]
    [InlineData("Product Demo", VideoType.ProductDemo)]
    [InlineData("Product Feature Showcase", VideoType.ProductDemo)]
    [InlineData("How to Tutorial", VideoType.Tutorial)]
    [InlineData("Learn Programming Guide", VideoType.Tutorial)]
    [InlineData("Marketing Launch", VideoType.Marketing)]
    [InlineData("Promotional Video", VideoType.Marketing)]
    [InlineData("Educational Content", VideoType.Educational)]
    [InlineData("Teaching Math Concepts", VideoType.Educational)]
    [InlineData("Welcome to Aura", VideoType.Welcome)]
    [InlineData("Getting Started with Aura", VideoType.Welcome)]
    [InlineData("Random Topic", VideoType.General)]
    public void DetermineVideoType_Should_DetectCorrectType(string topic, VideoType expectedType)
    {
        // Act
        var actualType = ScriptTemplates.DetermineVideoType(topic);

        // Assert
        Assert.Equal(expectedType, actualType);
    }

    [Theory]
    [InlineData(VideoType.ProductDemo)]
    [InlineData(VideoType.Tutorial)]
    [InlineData(VideoType.Marketing)]
    [InlineData(VideoType.Educational)]
    [InlineData(VideoType.Welcome)]
    [InlineData(VideoType.General)]
    public void GenerateFromTemplate_Should_ProduceNonEmptyScript(VideoType videoType)
    {
        // Arrange
        const string topic = "Test Topic";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(videoType, topic, targetWordCount);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains(topic, script);
    }

    [Fact]
    public void GenerateFromTemplate_ProductDemo_Should_HaveExpectedStructure()
    {
        // Arrange
        const string topic = "Amazing Product";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.ProductDemo, topic, targetWordCount);

        // Assert
        Assert.Contains("# Amazing Product", script);
        Assert.Contains("## Introduction", script);
        Assert.Contains("## Key Features", script);
        Assert.Contains("## Conclusion", script);
        Assert.Contains("standout features", script.ToLowerInvariant());
    }

    [Fact]
    public void GenerateFromTemplate_Tutorial_Should_HaveStepStructure()
    {
        // Arrange
        const string topic = "Programming Basics";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.Tutorial, topic, targetWordCount);

        // Assert
        Assert.Contains("# Programming Basics", script);
        Assert.Contains("## Introduction", script);
        Assert.Contains("## Step 1", script);
        Assert.Contains("## Step 3", script);
        Assert.Contains("## Summary", script);
        Assert.Contains("tutorial", script.ToLowerInvariant());
    }

    [Fact]
    public void GenerateFromTemplate_Marketing_Should_HavePersuasiveStructure()
    {
        // Arrange
        const string topic = "New Service Launch";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.Marketing, topic, targetWordCount);

        // Assert
        Assert.Contains("# New Service Launch", script);
        Assert.Contains("## Opening Hook", script);
        Assert.Contains("## The Problem", script);
        Assert.Contains("## The Solution", script);
        Assert.Contains("## Call to Action", script);
        Assert.Contains("game-changer", script.ToLowerInvariant());
    }

    [Fact]
    public void GenerateFromTemplate_Educational_Should_HaveAcademicStructure()
    {
        // Arrange
        const string topic = "Climate Science";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.Educational, topic, targetWordCount);

        // Assert
        Assert.Contains("# Climate Science", script);
        Assert.Contains("## Introduction", script);
        Assert.Contains("## Background and Context", script);
        Assert.Contains("## Core Concepts", script);
        Assert.Contains("## Conclusion", script);
        Assert.Contains("educational", script.ToLowerInvariant());
    }

    [Fact]
    public void GenerateFromTemplate_Welcome_Should_HaveOnboardingContent()
    {
        // Arrange
        const string topic = "Welcome to Aura Video Studio";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.Welcome, topic, targetWordCount);

        // Assert
        Assert.Contains("# Welcome to Aura Video Studio", script);
        Assert.Contains("## Welcome", script);
        Assert.Contains("## What You Can Do", script);
        Assert.Contains("## Getting Started", script);
        Assert.Contains("Aura Video Studio", script);
        Assert.Contains("AI", script);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(300)]
    public void GenerateFromTemplate_Should_ApproximateTargetWordCount(int targetWordCount)
    {
        // Arrange
        const string topic = "Test Topic";

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.General, topic, targetWordCount);

        // Assert
        var wordCount = script.Split(new[] { ' ', '\t', '\n', '\r' }, 
            StringSplitOptions.RemoveEmptyEntries).Length;
        
        // Should be within reasonable range of target (templates are flexible)
        var minWords = targetWordCount * 0.4;  // More lenient lower bound
        var maxWords = targetWordCount * 1.5;
        
        Assert.True(wordCount >= minWords, 
            $"Expected at least {minWords} words, got {wordCount}");
        Assert.True(wordCount <= maxWords, 
            $"Expected at most {maxWords} words, got {wordCount}");
    }

    [Fact]
    public void GenerateFromTemplate_Should_IncludeTopicInContent()
    {
        // Arrange
        const string topic = "Quantum Computing Fundamentals";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.Educational, topic, targetWordCount);

        // Assert
        var occurrences = CountOccurrences(script, topic);
        Assert.True(occurrences >= 2, 
            $"Topic should appear at least 2 times in the script, found {occurrences}");
    }

    [Fact]
    public void GenerateFromTemplate_Should_HaveMarkdownHeaders()
    {
        // Arrange
        const string topic = "Test Topic";
        const int targetWordCount = 200;

        // Act
        var script = ScriptTemplates.GenerateFromTemplate(VideoType.General, topic, targetWordCount);

        // Assert
        Assert.Contains("# ", script); // H1 header
        Assert.Contains("## ", script); // H2 headers
        
        var lines = script.Split('\n');
        var headerCount = lines.Count(line => line.StartsWith("##"));
        Assert.True(headerCount >= 3, $"Expected at least 3 section headers, got {headerCount}");
    }

    [Fact]
    public void DetermineVideoType_Should_BeCaseInsensitive()
    {
        // Arrange & Act
        var lowerCase = ScriptTemplates.DetermineVideoType("product demo");
        var upperCase = ScriptTemplates.DetermineVideoType("PRODUCT DEMO");
        var mixedCase = ScriptTemplates.DetermineVideoType("PrOdUcT DeMo");

        // Assert
        Assert.Equal(VideoType.ProductDemo, lowerCase);
        Assert.Equal(VideoType.ProductDemo, upperCase);
        Assert.Equal(VideoType.ProductDemo, mixedCase);
    }

    [Theory]
    [InlineData("demo product feature showcase", VideoType.ProductDemo)]
    [InlineData("how to guide tutorial learn", VideoType.Tutorial)]
    [InlineData("promo marketing launch announce", VideoType.Marketing)]
    [InlineData("education teach explain understand", VideoType.Educational)]
    public void DetermineVideoType_Should_DetectFromMultipleKeywords(string topic, VideoType expectedType)
    {
        // Act
        var actualType = ScriptTemplates.DetermineVideoType(topic);

        // Assert
        Assert.Equal(expectedType, actualType);
    }

    private static int CountOccurrences(string text, string searchTerm)
    {
        int count = 0;
        int index = 0;
        
        while ((index = text.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += searchTerm.Length;
        }
        
        return count;
    }
}
