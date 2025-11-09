using System;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Aura.Core.Services.PromptManagement;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Services;

/// <summary>
/// Tests for AdvancedScriptPromptBuilder
/// </summary>
public class AdvancedScriptPromptBuilderTests
{
    private readonly Mock<ILogger<AdvancedScriptPromptBuilder>> _loggerMock;
    private readonly AdvancedScriptPromptBuilder _promptBuilder;

    public AdvancedScriptPromptBuilderTests()
    {
        _loggerMock = new Mock<ILogger<AdvancedScriptPromptBuilder>>();
        _promptBuilder = new AdvancedScriptPromptBuilder(_loggerMock.Object);
    }

    [Fact]
    public void BuildSystemPrompt_Educational_ContainsLearningGuidelines()
    {
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var prompt = _promptBuilder.BuildSystemPrompt(VideoType.Educational, brief, planSpec);

        Assert.Contains("Educational Video Guidelines", prompt);
        Assert.Contains("learning objective", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("examples", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSystemPrompt_Marketing_ContainsMarketingGuidelines()
    {
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var prompt = _promptBuilder.BuildSystemPrompt(VideoType.Marketing, brief, planSpec);

        Assert.Contains("Marketing Video Guidelines", prompt);
        Assert.Contains("hook", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("call-to-action", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("emotional connection", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSystemPrompt_Entertainment_ContainsStoryGuidelines()
    {
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var prompt = _promptBuilder.BuildSystemPrompt(VideoType.Entertainment, brief, planSpec);

        Assert.Contains("Entertainment Video Guidelines", prompt);
        Assert.Contains("story arc", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tension", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("emotional", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAllBriefDetails()
    {
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var prompt = _promptBuilder.BuildUserPrompt(brief, planSpec, VideoType.General);

        Assert.Contains(brief.Topic, prompt);
        Assert.Contains(brief.Audience!, prompt);
        Assert.Contains(brief.Goal!, prompt);
        Assert.Contains(brief.Tone, prompt);
        Assert.Contains("30", prompt); // Duration in seconds
    }

    [Fact]
    public void BuildUserPrompt_Marketing_IncludesMarketingRequirements()
    {
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var prompt = _promptBuilder.BuildUserPrompt(brief, planSpec, VideoType.Marketing);

        Assert.Contains("Marketing Requirements", prompt);
        Assert.Contains("call-to-action", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first 3 seconds", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUserPrompt_Educational_IncludesEducationalRequirements()
    {
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var prompt = _promptBuilder.BuildUserPrompt(brief, planSpec, VideoType.Educational);

        Assert.Contains("Educational Requirements", prompt);
        Assert.Contains("learning objectives", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("examples", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRefinementPrompt_IncludesWeaknessesAndOriginalScript()
    {
        var originalScript = "Scene 1: Hello world. Scene 2: This is a test.";
        var weaknesses = new System.Collections.Generic.List<string>
        {
            "Improve narrative flow",
            "Add more specific visual descriptions"
        };

        var prompt = _promptBuilder.BuildRefinementPrompt(originalScript, weaknesses, VideoType.General);

        Assert.Contains("Improve the following video script", prompt);
        Assert.Contains(originalScript, prompt);
        Assert.Contains("Improve narrative flow", prompt);
        Assert.Contains("Add more specific visual descriptions", prompt);
    }

    [Fact]
    public void BuildHookOptimizationPrompt_IncludesHookTechniques()
    {
        var currentHook = "Welcome to our video";
        var brief = CreateTestBrief();

        var prompt = _promptBuilder.BuildHookOptimizationPrompt(currentHook, brief, 3);

        Assert.Contains(currentHook, prompt);
        Assert.Contains("3 seconds", prompt);
        Assert.Contains("curiosity", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("question", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("statistic", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSceneRegenerationPrompt_IncludesContext()
    {
        var sceneNumber = 2;
        var currentScene = "This is scene 2";
        var previousScene = "This is scene 1";
        var nextScene = "This is scene 3";
        var goal = "Make it more engaging";

        var prompt = _promptBuilder.BuildSceneRegenerationPrompt(
            sceneNumber, currentScene, previousScene, nextScene, goal);

        Assert.Contains($"Scene {sceneNumber}", prompt);
        Assert.Contains(currentScene, prompt);
        Assert.Contains(previousScene, prompt);
        Assert.Contains(nextScene, prompt);
        Assert.Contains(goal, prompt);
        Assert.Contains("smooth transitions", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildVariationPrompt_IncludesOriginalAndFocus()
    {
        var originalScript = "Original script content";
        var variationFocus = "More emotional approach";

        var prompt = _promptBuilder.BuildVariationPrompt(originalScript, variationFocus);

        Assert.Contains(originalScript, prompt);
        Assert.Contains(variationFocus, prompt);
        Assert.Contains("alternative version", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maintain the core message", prompt);
    }

    [Fact]
    public void GetMultiShotExamples_Educational_ReturnsExamples()
    {
        var examples = _promptBuilder.GetMultiShotExamples(VideoType.Educational);

        Assert.NotEmpty(examples);
        Assert.Contains("learning", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example", examples, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMultiShotExamples_Marketing_ReturnsExamples()
    {
        var examples = _promptBuilder.GetMultiShotExamples(VideoType.Marketing);

        Assert.NotEmpty(examples);
        Assert.Contains("problem", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("solution", examples, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMultiShotExamples_Entertainment_ReturnsExamples()
    {
        var examples = _promptBuilder.GetMultiShotExamples(VideoType.Entertainment);

        Assert.NotEmpty(examples);
        Assert.Contains("Setup", examples, StringComparison.OrdinalIgnoreCase);
    }

    private Brief CreateTestBrief()
    {
        return new Brief(
            Topic: "Video Editing Tutorial",
            Audience: "Beginners",
            Goal: "Teach basic concepts",
            Tone: "friendly",
            Language: "en",
            Aspect: Aspect.Widescreen16x9
        );
    }

    private PlanSpec CreateTestPlanSpec()
    {
        return new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(30),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "educational"
        );
    }
}
