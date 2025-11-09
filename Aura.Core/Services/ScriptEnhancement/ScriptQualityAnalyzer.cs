using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.ScriptEnhancement;

/// <summary>
/// Analyzes script quality across multiple dimensions
/// Provides actionable feedback for refinement
/// </summary>
public class ScriptQualityAnalyzer
{
    private readonly ILogger<ScriptQualityAnalyzer> _logger;
    
    private const double TargetWordsPerMinute = 155.0; // 150-160 WPM target
    private const double MinWordsPerMinute = 120.0;
    private const double MaxWordsPerMinute = 180.0;

    public ScriptQualityAnalyzer(ILogger<ScriptQualityAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Perform comprehensive quality analysis on a script
    /// </summary>
    public Task<ScriptQualityMetrics> AnalyzeAsync(
        Script script, 
        Brief brief, 
        PlanSpec planSpec,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing script quality for: {Title}", script.Title);

        var metrics = new ScriptQualityMetrics
        {
            AssessedAt = DateTime.UtcNow,
            Iteration = 0
        };

        metrics.NarrativeCoherence = AnalyzeNarrativeCoherence(script);
        metrics.PacingAppropriateness = AnalyzePacing(script, planSpec);
        metrics.AudienceAlignment = AnalyzeAudienceAlignment(script, brief);
        metrics.VisualClarity = AnalyzeVisualClarity(script);
        metrics.EngagementPotential = AnalyzeEngagementPotential(script);

        metrics.CalculateOverallScore();

        metrics.Issues = IdentifyIssues(script, brief, planSpec);
        metrics.Suggestions = GenerateSuggestions(metrics, script);
        metrics.Strengths = IdentifyStrengths(metrics, script);

        _logger.LogInformation("Quality analysis complete. Overall score: {Score:F1}", metrics.OverallScore);

        return Task.FromResult(metrics);
    }

    /// <summary>
    /// Validate reading speed for natural speech
    /// </summary>
    public ReadingSpeedValidation ValidateReadingSpeed(Script script)
    {
        var validation = new ReadingSpeedValidation();
        
        foreach (var scene in script.Scenes)
        {
            var wordCount = CountWords(scene.Narration);
            var durationMinutes = scene.Duration.TotalMinutes;
            
            if (durationMinutes > 0)
            {
                var wpm = wordCount / durationMinutes;
                validation.SceneReadingSpeeds[scene.Number] = wpm;
                
                if (wpm < MinWordsPerMinute)
                {
                    validation.Issues.Add($"Scene {scene.Number}: Too slow ({wpm:F0} WPM). May feel sluggish.");
                }
                else if (wpm > MaxWordsPerMinute)
                {
                    validation.Issues.Add($"Scene {scene.Number}: Too fast ({wpm:F0} WPM). May be hard to follow.");
                }
            }
        }

        var totalWords = script.Scenes.Sum(s => CountWords(s.Narration));
        var totalMinutes = script.TotalDuration.TotalMinutes;
        validation.OverallReadingSpeed = totalMinutes > 0 ? totalWords / totalMinutes : 0;
        validation.IsWithinRange = validation.OverallReadingSpeed >= MinWordsPerMinute 
                                  && validation.OverallReadingSpeed <= MaxWordsPerMinute;

        _logger.LogDebug("Reading speed validation: {WPM:F1} WPM, within range: {InRange}", 
            validation.OverallReadingSpeed, validation.IsWithinRange);

        return validation;
    }

    /// <summary>
    /// Validate scene count matches duration appropriately
    /// </summary>
    public SceneCountValidation ValidateSceneCount(Script script, PlanSpec planSpec)
    {
        var validation = new SceneCountValidation();
        
        var durationSeconds = planSpec.TargetDuration.TotalSeconds;
        validation.ActualSceneCount = script.Scenes.Count;
        
        validation.RecommendedMinScenes = planSpec.Pacing switch
        {
            Pacing.Chill => Math.Max(2, (int)(durationSeconds / 15)),
            Pacing.Conversational => Math.Max(3, (int)(durationSeconds / 10)),
            Pacing.Fast => Math.Max(4, (int)(durationSeconds / 6)),
            _ => Math.Max(3, (int)(durationSeconds / 10))
        };

        validation.RecommendedMaxScenes = planSpec.Pacing switch
        {
            Pacing.Chill => (int)(durationSeconds / 8),
            Pacing.Conversational => (int)(durationSeconds / 5),
            Pacing.Fast => (int)(durationSeconds / 3),
            _ => (int)(durationSeconds / 5)
        };

        validation.IsOptimal = validation.ActualSceneCount >= validation.RecommendedMinScenes
                             && validation.ActualSceneCount <= validation.RecommendedMaxScenes;

        if (validation.ActualSceneCount < validation.RecommendedMinScenes)
        {
            validation.Issue = $"Too few scenes ({validation.ActualSceneCount}). Recommended: {validation.RecommendedMinScenes}-{validation.RecommendedMaxScenes}";
        }
        else if (validation.ActualSceneCount > validation.RecommendedMaxScenes)
        {
            validation.Issue = $"Too many scenes ({validation.ActualSceneCount}). May feel fragmented. Recommended: {validation.RecommendedMinScenes}-{validation.RecommendedMaxScenes}";
        }

        return validation;
    }

    /// <summary>
    /// Validate visual prompt specificity
    /// </summary>
    public VisualPromptValidation ValidateVisualPrompts(Script script)
    {
        var validation = new VisualPromptValidation();
        
        foreach (var scene in script.Scenes)
        {
            var specificity = CalculateVisualSpecificity(scene.VisualPrompt);
            validation.SceneSpecificity[scene.Number] = specificity;
            
            if (specificity < 40)
            {
                validation.VaguePrompts.Add($"Scene {scene.Number}: '{scene.VisualPrompt}' is too vague. Add specific details.");
            }
            else if (specificity > 90)
            {
                validation.Strengths.Add($"Scene {scene.Number}: Highly specific visual prompt");
            }
        }

        validation.AverageSpecificity = validation.SceneSpecificity.Values.Average();
        validation.AllPromptsSpecific = validation.AverageSpecificity >= 60;

        return validation;
    }

    /// <summary>
    /// Validate narrative flow and coherence
    /// </summary>
    public NarrativeFlowValidation ValidateNarrativeFlow(Script script)
    {
        var validation = new NarrativeFlowValidation();
        
        if (script.Scenes.Count < 2)
        {
            validation.HasFlow = true;
            return validation;
        }

        for (int i = 1; i < script.Scenes.Count; i++)
        {
            var previousScene = script.Scenes[i - 1];
            var currentScene = script.Scenes[i];
            
            var coherenceScore = CalculateSceneCoherence(previousScene.Narration, currentScene.Narration);
            
            if (coherenceScore < 0.3)
            {
                validation.DisconnectedTransitions.Add(
                    $"Weak transition between Scene {previousScene.Number} and {currentScene.Number}");
            }
        }

        var hasOpeningHook = HasStrongOpening(script.Scenes[0].Narration);
        var hasClosingStatement = HasStrongClosing(script.Scenes[^1].Narration);
        
        if (!hasOpeningHook)
        {
            validation.Issues.Add("Opening lacks a strong hook to grab attention");
        }
        
        if (!hasClosingStatement)
        {
            validation.Issues.Add("Ending lacks a clear conclusion or call-to-action");
        }

        validation.HasStrongOpening = hasOpeningHook;
        validation.HasStrongClosing = hasClosingStatement;
        validation.HasFlow = validation.DisconnectedTransitions.Count == 0;
        validation.CoherenceScore = 100 - (validation.DisconnectedTransitions.Count * 15);

        return validation;
    }

    /// <summary>
    /// Check content appropriateness (basic checks)
    /// </summary>
    public ContentAppropriatenessValidation ValidateContentAppropriateness(Script script)
    {
        var validation = new ContentAppropriatenessValidation();
        
        var combinedText = string.Join(" ", script.Scenes.Select(s => s.Narration));
        
        var profanityPatterns = new[] { "damn", "hell", "crap" };
        var sensitivePhrases = new[] { "guarantee", "promise", "never fail" };
        
        foreach (var pattern in profanityPatterns)
        {
            if (combinedText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                validation.Warnings.Add($"Contains potentially inappropriate language: '{pattern}'");
            }
        }

        foreach (var phrase in sensitivePhrases)
        {
            if (combinedText.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                validation.Cautions.Add($"Contains strong claim: '{phrase}'. Consider softening or providing evidence.");
            }
        }

        validation.IsAppropriate = validation.Warnings.Count == 0;

        return validation;
    }

    private double AnalyzeNarrativeCoherence(Script script)
    {
        var score = 70.0;

        if (script.Scenes.Count == 0) return 0;

        if (script.Scenes.Count == 1)
        {
            var hasStructure = HasBasicStructure(script.Scenes[0].Narration);
            return hasStructure ? 80.0 : 60.0;
        }

        var hasOpening = HasStrongOpening(script.Scenes[0].Narration);
        var hasClosing = HasStrongClosing(script.Scenes[^1].Narration);
        
        if (hasOpening) score += 10;
        if (hasClosing) score += 10;

        var avgCoherence = 0.0;
        for (int i = 1; i < script.Scenes.Count; i++)
        {
            avgCoherence += CalculateSceneCoherence(
                script.Scenes[i - 1].Narration, 
                script.Scenes[i].Narration);
        }
        avgCoherence /= (script.Scenes.Count - 1);
        
        score += avgCoherence * 10;

        return Math.Min(100, score);
    }

    private double AnalyzePacing(Script script, PlanSpec planSpec)
    {
        var readingValidation = ValidateReadingSpeed(script);
        var sceneValidation = ValidateSceneCount(script, planSpec);

        var score = 70.0;

        if (readingValidation.IsWithinRange)
        {
            score += 15;
        }
        else
        {
            var deviation = Math.Abs(readingValidation.OverallReadingSpeed - TargetWordsPerMinute);
            score += Math.Max(0, 15 - (deviation / 10));
        }

        if (sceneValidation.IsOptimal)
        {
            score += 15;
        }
        else
        {
            score += 5;
        }

        return Math.Min(100, score);
    }

    private double AnalyzeAudienceAlignment(Script script, Brief brief)
    {
        var score = 70.0;

        var combinedText = string.Join(" ", script.Scenes.Select(s => s.Narration)).ToLower();
        
        var hasDirectAddress = combinedText.Contains("you") || combinedText.Contains("your");
        if (hasDirectAddress) score += 10;

        var toneMatch = brief.Tone.ToLower() switch
        {
            "casual" or "friendly" => combinedText.Contains("hey") || combinedText.Contains("let's"),
            "formal" or "professional" => !combinedText.Contains("gonna") && !combinedText.Contains("wanna"),
            _ => true
        };
        
        if (toneMatch) score += 10;

        if (!string.IsNullOrEmpty(brief.Topic))
        {
            var topicWords = brief.Topic.ToLower().Split(' ');
            var topicMentions = topicWords.Count(word => combinedText.Contains(word));
            score += Math.Min(10, topicMentions * 2);
        }

        return Math.Min(100, score);
    }

    private double AnalyzeVisualClarity(Script script)
    {
        var validation = ValidateVisualPrompts(script);
        return validation.AverageSpecificity;
    }

    private double AnalyzeEngagementPotential(Script script)
    {
        var score = 60.0;

        if (script.Scenes.Count > 0)
        {
            var opening = script.Scenes[0].Narration.ToLower();
            
            if (opening.Contains("?")) score += 5;
            if (Regex.IsMatch(opening, @"\d+")) score += 5;
            if (opening.Contains("discover") || opening.Contains("learn") || opening.Contains("secret")) score += 5;
            
            var hasVariety = script.Scenes.Select(s => CountWords(s.Narration)).Distinct().Count() > script.Scenes.Count / 2;
            if (hasVariety) score += 10;

            var avgSentenceLength = script.Scenes.Average(s => CountWords(s.Narration) / Math.Max(1, CountSentences(s.Narration)));
            if (avgSentenceLength >= 10 && avgSentenceLength <= 20) score += 10;
        }

        return Math.Min(100, score);
    }

    private List<string> IdentifyIssues(Script script, Brief brief, PlanSpec planSpec)
    {
        var issues = new List<string>();

        var readingSpeed = ValidateReadingSpeed(script);
        issues.AddRange(readingSpeed.Issues);

        var sceneCount = ValidateSceneCount(script, planSpec);
        if (!sceneCount.IsOptimal && sceneCount.Issue != null)
        {
            issues.Add(sceneCount.Issue);
        }

        var visualPrompts = ValidateVisualPrompts(script);
        issues.AddRange(visualPrompts.VaguePrompts);

        var narrativeFlow = ValidateNarrativeFlow(script);
        issues.AddRange(narrativeFlow.Issues);
        issues.AddRange(narrativeFlow.DisconnectedTransitions);

        return issues;
    }

    private List<string> GenerateSuggestions(ScriptQualityMetrics metrics, Script script)
    {
        var suggestions = new List<string>();

        if (metrics.NarrativeCoherence < 70)
        {
            suggestions.Add("Strengthen narrative flow with better transitions between scenes");
        }

        if (metrics.PacingAppropriateness < 70)
        {
            suggestions.Add("Adjust pacing to match target reading speed (150-160 WPM)");
        }

        if (metrics.AudienceAlignment < 70)
        {
            suggestions.Add("Use more direct address ('you', 'your') to connect with audience");
        }

        if (metrics.VisualClarity < 70)
        {
            suggestions.Add("Add more specific details to visual prompts");
        }

        if (metrics.EngagementPotential < 70)
        {
            suggestions.Add("Strengthen opening hook and vary sentence structure");
        }

        return suggestions;
    }

    private List<string> IdentifyStrengths(ScriptQualityMetrics metrics, Script script)
    {
        var strengths = new List<string>();

        if (metrics.NarrativeCoherence >= 85)
        {
            strengths.Add("Excellent narrative coherence and flow");
        }

        if (metrics.PacingAppropriateness >= 85)
        {
            strengths.Add("Optimal pacing for natural speech");
        }

        if (metrics.AudienceAlignment >= 85)
        {
            strengths.Add("Strong audience connection and relevance");
        }

        if (metrics.VisualClarity >= 85)
        {
            strengths.Add("Highly specific visual descriptions");
        }

        if (metrics.EngagementPotential >= 85)
        {
            strengths.Add("Compelling content with strong engagement hooks");
        }

        return strengths;
    }

    private bool HasStrongOpening(string text)
    {
        var lower = text.ToLower();
        return lower.Contains("?") 
            || Regex.IsMatch(lower, @"^\s*(imagine|discover|learn|ever wonder|what if|did you know)")
            || Regex.IsMatch(text, @"\d+%|\d+ (million|billion|thousand)");
    }

    private bool HasStrongClosing(string text)
    {
        var lower = text.ToLower();
        return lower.Contains("remember") 
            || lower.Contains("action") 
            || lower.Contains("start") 
            || lower.Contains("try")
            || lower.Contains("subscribe")
            || lower.Contains("summary")
            || lower.Contains("conclusion");
    }

    private bool HasBasicStructure(string text)
    {
        var sentences = CountSentences(text);
        return sentences >= 3;
    }

    private double CalculateSceneCoherence(string scene1, string scene2)
    {
        var words1 = scene1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = scene2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);
        
        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }

    private int CalculateVisualSpecificity(string visualPrompt)
    {
        var score = 40;
        
        var descriptiveWords = new[] { "professional", "modern", "bright", "dark", "detailed", "clear", "vibrant", "close-up" };
        var contextWords = new[] { "office", "outdoor", "indoor", "studio", "kitchen", "workspace", "background" };
        var actionWords = new[] { "working", "typing", "presenting", "showing", "demonstrating", "explaining" };
        
        var lower = visualPrompt.ToLower();
        
        foreach (var word in descriptiveWords)
        {
            if (lower.Contains(word)) score += 8;
        }
        
        foreach (var word in contextWords)
        {
            if (lower.Contains(word)) score += 10;
        }
        
        foreach (var word in actionWords)
        {
            if (lower.Contains(word)) score += 6;
        }
        
        if (visualPrompt.Split(' ').Length > 5) score += 10;
        
        return Math.Min(100, score);
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private int CountSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Regex.Matches(text, @"[.!?]+").Count;
    }
}

/// <summary>
/// Reading speed validation result
/// </summary>
public class ReadingSpeedValidation
{
    public double OverallReadingSpeed { get; set; }
    public bool IsWithinRange { get; set; }
    public Dictionary<int, double> SceneReadingSpeeds { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Scene count validation result
/// </summary>
public class SceneCountValidation
{
    public int ActualSceneCount { get; set; }
    public int RecommendedMinScenes { get; set; }
    public int RecommendedMaxScenes { get; set; }
    public bool IsOptimal { get; set; }
    public string? Issue { get; set; }
}

/// <summary>
/// Visual prompt validation result
/// </summary>
public class VisualPromptValidation
{
    public double AverageSpecificity { get; set; }
    public bool AllPromptsSpecific { get; set; }
    public Dictionary<int, int> SceneSpecificity { get; set; } = new();
    public List<string> VaguePrompts { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
}

/// <summary>
/// Narrative flow validation result
/// </summary>
public class NarrativeFlowValidation
{
    public bool HasFlow { get; set; }
    public bool HasStrongOpening { get; set; }
    public bool HasStrongClosing { get; set; }
    public double CoherenceScore { get; set; }
    public List<string> DisconnectedTransitions { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Content appropriateness validation result
/// </summary>
public class ContentAppropriatenessValidation
{
    public bool IsAppropriate { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Cautions { get; set; } = new();
}
