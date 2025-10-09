using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Recommendations;

/// <summary>
/// Deterministic heuristic-based recommendation engine that works offline
/// without requiring LLM access
/// </summary>
public class HeuristicRecommendationEngine : IRecommendationEngine
{
    private readonly ILogger<HeuristicRecommendationEngine> _logger;
    private readonly Random _random;

    public HeuristicRecommendationEngine(ILogger<HeuristicRecommendationEngine> logger)
    {
        _logger = logger;
        _random = new Random(42); // Fixed seed for deterministic output
    }

    public Task<PlannerRecommendations> GenerateRecommendationsAsync(
        RecommendationRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Generating heuristic recommendations for topic: {Topic}", 
            request.Brief.Topic);

        // Generate outline based on brief and plan
        var outline = GenerateOutline(request.Brief, request.PlanSpec);

        // Calculate scene count based on duration
        var sceneCount = CalculateSceneCount(
            request.PlanSpec.TargetDuration,
            request.Constraints?.MinSceneCount,
            request.Constraints?.MaxSceneCount);

        // Calculate shots per scene based on pacing
        var shotsPerScene = CalculateShotsPerScene(request.PlanSpec.Pacing);

        // Calculate B-roll percentage based on style and density
        var bRollPercentage = CalculateBRollPercentage(
            request.PlanSpec.Style,
            request.PlanSpec.Density,
            request.Constraints);

        // Calculate overlay density
        var overlayDensity = CalculateOverlayDensity(request.PlanSpec.Density);

        // Determine reading level based on audience
        var readingLevel = DetermineReadingLevel(
            request.Brief.Audience, 
            request.AudiencePersona);

        // Generate voice recommendations
        var voiceRec = GenerateVoiceRecommendations(
            request.PlanSpec.Pacing,
            request.Brief.Tone);

        // Generate music recommendations
        var musicRec = GenerateMusicRecommendations(
            request.PlanSpec.Pacing,
            request.Brief.Tone);

        // Generate caption recommendations
        var captionRec = GenerateCaptionRecommendations(request.Brief.Aspect);

        // Generate thumbnail prompt
        var thumbnailPrompt = GenerateThumbnailPrompt(request.Brief);

        // Generate SEO recommendations
        var seoRec = GenerateSeoRecommendations(request.Brief);

        var recommendations = new PlannerRecommendations(
            Outline: outline,
            SceneCount: sceneCount,
            ShotsPerScene: shotsPerScene,
            BRollPercentage: bRollPercentage,
            OverlayDensity: overlayDensity,
            ReadingLevel: readingLevel,
            Voice: voiceRec,
            Music: musicRec,
            Captions: captionRec,
            ThumbnailPrompt: thumbnailPrompt,
            Seo: seoRec
        );

        _logger.LogInformation(
            "Generated recommendations: {SceneCount} scenes, {ShotsPerScene} shots/scene, " +
            "{BRollPercentage:F1}% B-roll",
            sceneCount, shotsPerScene, bRollPercentage);

        return Task.FromResult(recommendations);
    }

    private string GenerateOutline(Brief brief, PlanSpec spec)
    {
        var sections = new List<string>
        {
            "1. Introduction",
            $"   - Hook: Capture attention with compelling {brief.Topic} opening",
            "   - Preview: Overview of what's covered",
            ""
        };

        // Determine number of main sections based on duration
        int mainSections = spec.TargetDuration.TotalMinutes switch
        {
            <= 2 => 2,
            <= 5 => 3,
            <= 10 => 4,
            _ => 5
        };

        for (int i = 2; i <= mainSections + 1; i++)
        {
            sections.Add($"{i}. Main Point {i - 1}");
            sections.Add($"   - Key concept or demonstration");
            sections.Add($"   - Supporting details and examples");
            sections.Add("");
        }

        sections.Add($"{mainSections + 2}. Conclusion");
        sections.Add("   - Recap main points");
        sections.Add("   - Call to action");

        return string.Join("\n", sections);
    }

    private int CalculateSceneCount(TimeSpan duration, int? minScenes, int? maxScenes)
    {
        // Base calculation: ~30 seconds per scene
        int baseCount = (int)Math.Ceiling(duration.TotalSeconds / 30);

        // Apply bounds
        int min = minScenes ?? 3;
        int max = maxScenes ?? 20;

        return Math.Clamp(baseCount, min, max);
    }

    private int CalculateShotsPerScene(Pacing pacing)
    {
        return pacing switch
        {
            Pacing.Chill => 2,
            Pacing.Conversational => 3,
            Pacing.Fast => 4,
            _ => 3
        };
    }

    private double CalculateBRollPercentage(
        string style,
        Density density,
        PlannerConstraints? constraints)
    {
        // Base B-roll percentage by style
        double basePercentage = style.ToLowerInvariant() switch
        {
            var s when s.Contains("tutorial") => 30.0,
            var s when s.Contains("educational") => 40.0,
            var s when s.Contains("vlog") => 20.0,
            var s when s.Contains("documentary") => 50.0,
            var s when s.Contains("review") => 35.0,
            _ => 30.0
        };

        // Adjust by density
        double densityMultiplier = density switch
        {
            Density.Sparse => 0.8,
            Density.Balanced => 1.0,
            Density.Dense => 1.2,
            _ => 1.0
        };

        double percentage = basePercentage * densityMultiplier;

        // Apply constraints
        if (constraints?.MinBRollPercentage.HasValue == true)
        {
            percentage = Math.Max(percentage, constraints.MinBRollPercentage.Value);
        }

        if (constraints?.MaxBRollPercentage.HasValue == true)
        {
            percentage = Math.Min(percentage, constraints.MaxBRollPercentage.Value);
        }

        return Math.Round(percentage, 1);
    }

    private double CalculateOverlayDensity(Density density)
    {
        return density switch
        {
            Density.Sparse => 0.3,
            Density.Balanced => 0.5,
            Density.Dense => 0.7,
            _ => 0.5
        };
    }

    private string DetermineReadingLevel(string? audience, string? persona)
    {
        // Check persona first for more specific guidance
        if (!string.IsNullOrWhiteSpace(persona))
        {
            var lowerPersona = persona.ToLowerInvariant();
            if (lowerPersona.Contains("expert") || lowerPersona.Contains("professional"))
                return "Advanced (College+)";
            if (lowerPersona.Contains("student") || lowerPersona.Contains("learner"))
                return "Intermediate (High School)";
            if (lowerPersona.Contains("beginner") || lowerPersona.Contains("child"))
                return "Basic (Middle School)";
        }

        // Fall back to audience
        if (!string.IsNullOrWhiteSpace(audience))
        {
            var lowerAudience = audience.ToLowerInvariant();
            if (lowerAudience.Contains("expert") || lowerAudience.Contains("professional"))
                return "Advanced (College+)";
            if (lowerAudience.Contains("beginner") || lowerAudience.Contains("general"))
                return "Basic (Middle School)";
        }

        return "Intermediate (High School)";
    }

    private VoiceRecommendations GenerateVoiceRecommendations(Pacing pacing, string tone)
    {
        // Base rate from pacing
        double rate = pacing switch
        {
            Pacing.Chill => 0.85,
            Pacing.Conversational => 1.0,
            Pacing.Fast => 1.15,
            _ => 1.0
        };

        // Pitch from tone
        double pitch = tone.ToLowerInvariant() switch
        {
            var t when t.Contains("professional") => 0.0,
            var t when t.Contains("casual") => 2.0,
            var t when t.Contains("enthusiastic") => 3.0,
            var t when t.Contains("serious") => -1.0,
            _ => 0.0
        };

        return new VoiceRecommendations(Rate: rate, Pitch: pitch);
    }

    private MusicRecommendations GenerateMusicRecommendations(Pacing pacing, string tone)
    {
        // Tempo based on pacing
        int tempo = pacing switch
        {
            Pacing.Chill => 80,
            Pacing.Conversational => 100,
            Pacing.Fast => 120,
            _ => 100
        };

        // Intensity curve based on tone
        string curve = tone.ToLowerInvariant() switch
        {
            var t when t.Contains("dramatic") => "crescendo",
            var t when t.Contains("calm") => "steady-low",
            var t when t.Contains("exciting") => "dynamic",
            _ => "steady-medium"
        };

        return new MusicRecommendations(Tempo: tempo, IntensityCurve: curve);
    }

    private CaptionRecommendations GenerateCaptionRecommendations(Aspect aspect)
    {
        // Style based on aspect ratio (vertical videos often use more dynamic captions)
        string style = aspect switch
        {
            Aspect.Vertical9x16 => "word-by-word-highlight",
            Aspect.Square1x1 => "center-block",
            Aspect.Widescreen16x9 => "bottom-block",
            _ => "bottom-block"
        };

        string position = aspect switch
        {
            Aspect.Vertical9x16 => "center",
            _ => "bottom"
        };

        int fontSize = aspect switch
        {
            Aspect.Vertical9x16 => 48,
            Aspect.Square1x1 => 42,
            Aspect.Widescreen16x9 => 36,
            _ => 36
        };

        return new CaptionRecommendations(
            Style: style,
            Position: position,
            FontSize: fontSize,
            Color: "#FFFFFF"
        );
    }

    private string GenerateThumbnailPrompt(Brief brief)
    {
        var elements = new List<string>
        {
            $"Topic: {brief.Topic}",
            "High contrast, eye-catching composition",
            "Bold text overlay with key hook",
        };

        // Add tone-specific guidance
        if (!string.IsNullOrWhiteSpace(brief.Tone))
        {
            var lowerTone = brief.Tone.ToLowerInvariant();
            if (lowerTone.Contains("professional"))
                elements.Add("Clean, professional aesthetic");
            else if (lowerTone.Contains("casual"))
                elements.Add("Friendly, approachable style");
            else if (lowerTone.Contains("exciting"))
                elements.Add("Dynamic, energetic colors");
        }

        // Add aspect-specific guidance
        elements.Add(brief.Aspect switch
        {
            Aspect.Vertical9x16 => "Vertical format (9:16)",
            Aspect.Square1x1 => "Square format (1:1)",
            _ => "Widescreen format (16:9)"
        });

        return string.Join("; ", elements);
    }

    private SeoRecommendations GenerateSeoRecommendations(Brief brief)
    {
        // Generate title
        var title = $"{brief.Topic}";
        if (!string.IsNullOrWhiteSpace(brief.Goal))
        {
            title += $" - {brief.Goal}";
        }
        // Limit to ~60 characters for SEO
        if (title.Length > 60)
        {
            title = title.Substring(0, 57) + "...";
        }

        // Generate description
        var description = $"Learn about {brief.Topic}. ";
        if (!string.IsNullOrWhiteSpace(brief.Audience))
        {
            description += $"Perfect for {brief.Audience}. ";
        }
        if (!string.IsNullOrWhiteSpace(brief.Goal))
        {
            description += $"This video will help you {brief.Goal}.";
        }
        else
        {
            description += "Watch now to discover more!";
        }

        // Generate tags from topic
        var tags = GenerateTagsFromTopic(brief.Topic);

        return new SeoRecommendations(
            Title: title,
            Description: description,
            Tags: tags
        );
    }

    private string[] GenerateTagsFromTopic(string topic)
    {
        var tags = new List<string> { topic };

        // Split topic into words and add relevant variations
        var words = topic.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Add individual significant words (longer than 3 chars)
        foreach (var word in words.Where(w => w.Length > 3))
        {
            tags.Add(word.ToLowerInvariant());
        }

        // Add common related terms
        tags.AddRange(new[] { "tutorial", "guide", "how-to", "explained" });

        // Remove duplicates and limit to 10 tags
        return tags.Distinct().Take(10).ToArray();
    }
}
