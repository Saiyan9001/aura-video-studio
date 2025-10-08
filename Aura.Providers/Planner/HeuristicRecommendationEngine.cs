using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Planner;

/// <summary>
/// Heuristic-based recommendation engine that provides sensible defaults
/// without requiring an LLM. Falls back when no API keys or in OfflineOnly mode.
/// </summary>
public class HeuristicRecommendationEngine : IRecommendationEngine
{
    private readonly ILogger<HeuristicRecommendationEngine> _logger;

    public HeuristicRecommendationEngine(ILogger<HeuristicRecommendationEngine> logger)
    {
        _logger = logger;
    }

    public Task<PlanRecommendations> GenerateRecommendationsAsync(
        Brief brief,
        PlanSpec planSpec,
        AudiencePersona? persona,
        PlanConstraints? constraints,
        CancellationToken ct)
    {
        _logger.LogInformation("Generating heuristic recommendations for topic: {Topic}", brief.Topic);

        var recommendations = new PlanRecommendations
        {
            Outline = GenerateOutline(brief, planSpec),
            SceneCount = DetermineSceneCount(planSpec.TargetDuration),
            ShotsPerScene = DetermineShotsPerScene(planSpec.Pacing, planSpec.Style),
            BRollPercentage = DetermineBRollPercentage(planSpec.Style, brief.Goal),
            OverlayDensity = DetermineOverlayDensity(planSpec.Density, planSpec.Style),
            ReadingLevel = DetermineReadingLevel(brief.Audience, persona),
            VoiceRate = DetermineVoiceRate(planSpec.Pacing),
            VoicePitch = DetermineVoicePitch(brief.Tone),
            MusicTempoCurve = DetermineMusicTempoCurve(planSpec.Pacing, planSpec.TargetDuration),
            MusicIntensityCurve = DetermineMusicIntensityCurve(planSpec.Style, planSpec.TargetDuration),
            CaptionStyle = DetermineCaptionStyle(brief.Aspect, brief.Audience),
            ThumbnailPrompt = GenerateThumbnailPrompt(brief),
            SeoTitle = GenerateSeoTitle(brief),
            SeoDescription = GenerateSeoDescription(brief, planSpec),
            SeoTags = GenerateSeoTags(brief)
        };

        _logger.LogInformation("Generated recommendations: {SceneCount} scenes, {ShotsPerScene} shots/scene, {BRoll}% B-roll",
            recommendations.SceneCount, recommendations.ShotsPerScene, recommendations.BRollPercentage);

        return Task.FromResult(recommendations);
    }

    private string GenerateOutline(Brief brief, PlanSpec planSpec)
    {
        var sceneCount = DetermineSceneCount(planSpec.TargetDuration);
        var outline = new List<string>
        {
            $"# {brief.Topic}",
            "",
            "## Structure:",
            "1. Hook & Introduction (15-20% of duration)",
            "   - Open with attention-grabbing statement or question",
            "   - Introduce the topic and why it matters",
            "   - Preview what will be covered",
            ""
        };

        // Add body sections
        int bodySections = sceneCount - 2; // Subtract intro and conclusion
        for (int i = 1; i <= bodySections; i++)
        {
            outline.Add($"{i + 1}. Main Point {i} ({(60 / bodySections)}% of middle section)");
            outline.Add($"   - Key concept or insight #{i}");
            outline.Add($"   - Supporting evidence or examples");
            outline.Add($"   - Transition to next point");
            outline.Add("");
        }

        outline.Add($"{bodySections + 2}. Conclusion & Call-to-Action (15-20% of duration)");
        outline.Add("   - Summarize key takeaways");
        outline.Add("   - Reinforce main message");
        outline.Add("   - Clear call-to-action (subscribe, visit website, etc.)");

        return string.Join("\n", outline);
    }

    private int DetermineSceneCount(TimeSpan duration)
    {
        // Rule: 1 scene per 30-45 seconds for good pacing
        int baseCount = (int)Math.Ceiling(duration.TotalSeconds / 35);
        return Math.Clamp(baseCount, 3, 20);
    }

    private int DetermineShotsPerScene(Pacing pacing, string style)
    {
        // More shots = faster cutting, more dynamic
        int baseShots = pacing switch
        {
            Pacing.Chill => 2,
            Pacing.Conversational => 3,
            Pacing.Fast => 5,
            _ => 3
        };

        // Educational content typically has fewer cuts
        if (style.Contains("Educational", StringComparison.OrdinalIgnoreCase))
        {
            baseShots = Math.Max(1, baseShots - 1);
        }

        return Math.Clamp(baseShots, 1, 8);
    }

    private double DetermineBRollPercentage(string style, string? goal)
    {
        // B-roll percentage based on content type
        if (style.Contains("Documentary", StringComparison.OrdinalIgnoreCase))
            return 60.0;
        
        if (style.Contains("Tutorial", StringComparison.OrdinalIgnoreCase))
            return 40.0;

        if (style.Contains("Vlog", StringComparison.OrdinalIgnoreCase))
            return 20.0;

        if (goal?.Contains("Entertain", StringComparison.OrdinalIgnoreCase) == true)
            return 50.0;

        // Default balanced approach
        return 35.0;
    }

    private double DetermineOverlayDensity(Density density, string style)
    {
        // Overlay density: text overlays, graphics, etc.
        double baseDensity = density switch
        {
            Density.Sparse => 0.3,
            Density.Balanced => 0.5,
            Density.Dense => 0.8,
            _ => 0.5
        };

        // Educational content benefits from more overlays
        if (style.Contains("Educational", StringComparison.OrdinalIgnoreCase))
        {
            baseDensity = Math.Min(1.0, baseDensity + 0.2);
        }

        return Math.Clamp(baseDensity, 0.0, 1.0);
    }

    private string DetermineReadingLevel(string? audience, AudiencePersona? persona)
    {
        var expertiseLevel = persona?.ExpertiseLevel?.ToLowerInvariant() ?? "";
        var audienceLower = audience?.ToLowerInvariant() ?? "";

        if (expertiseLevel.Contains("expert") || audienceLower.Contains("professional"))
            return "Advanced (College+)";

        if (expertiseLevel.Contains("intermediate") || audienceLower.Contains("intermediate"))
            return "Intermediate (High School)";

        if (audienceLower.Contains("children") || audienceLower.Contains("kids"))
            return "Elementary (Grade 5-6)";

        // Default to accessible for general audiences
        return "General (Grade 8-10)";
    }

    private double DetermineVoiceRate(Pacing pacing)
    {
        // Voice rate: 0.5 (slow) to 2.0 (fast), default 1.0
        return pacing switch
        {
            Pacing.Chill => 0.85,
            Pacing.Conversational => 1.0,
            Pacing.Fast => 1.15,
            _ => 1.0
        };
    }

    private double DetermineVoicePitch(string tone)
    {
        var toneLower = tone.ToLowerInvariant();

        // Pitch: -20 (lower) to +20 (higher), default 0
        if (toneLower.Contains("serious") || toneLower.Contains("professional"))
            return -2.0;

        if (toneLower.Contains("energetic") || toneLower.Contains("enthusiastic"))
            return 3.0;

        if (toneLower.Contains("calm") || toneLower.Contains("soothing"))
            return -1.0;

        return 0.0; // Neutral
    }

    private string DetermineMusicTempoCurve(Pacing pacing, TimeSpan duration)
    {
        // Tempo curve describes how music energy changes over time
        var totalSeconds = (int)duration.TotalSeconds;

        return pacing switch
        {
            Pacing.Chill => $"0s:70bpm,{totalSeconds / 2}s:65bpm,{totalSeconds}s:70bpm",
            Pacing.Conversational => $"0s:90bpm,{totalSeconds / 2}s:85bpm,{totalSeconds}s:95bpm",
            Pacing.Fast => $"0s:120bpm,{totalSeconds / 2}s:125bpm,{totalSeconds}s:130bpm",
            _ => $"0s:90bpm,{totalSeconds / 2}s:85bpm,{totalSeconds}s:95bpm"
        };
    }

    private string DetermineMusicIntensityCurve(string style, TimeSpan duration)
    {
        // Intensity curve: 0.0 (ambient) to 1.0 (full energy)
        var totalSeconds = (int)duration.TotalSeconds;
        var mid = totalSeconds / 2;
        var threeQuarters = (totalSeconds * 3) / 4;

        // Typical curve: start medium, build in middle, peak near end, fade out
        if (style.Contains("Dramatic", StringComparison.OrdinalIgnoreCase))
            return $"0s:0.5,{mid}s:0.7,{threeQuarters}s:0.9,{totalSeconds}s:0.6";

        if (style.Contains("Calm", StringComparison.OrdinalIgnoreCase))
            return $"0s:0.3,{mid}s:0.4,{threeQuarters}s:0.35,{totalSeconds}s:0.3";

        // Default: gradual build and release
        return $"0s:0.4,{mid}s:0.6,{threeQuarters}s:0.7,{totalSeconds}s:0.4";
    }

    private string DetermineCaptionStyle(Aspect aspect, string? audience)
    {
        // Caption styles optimized for different formats
        if (aspect == Aspect.Vertical9x16)
        {
            // Vertical format (TikTok, Reels, Shorts)
            return "Dynamic Word-by-Word (Center, Bold, Large)";
        }

        if (aspect == Aspect.Square1x1)
        {
            // Square format (Instagram)
            return "Centered Block (Medium, Clean)";
        }

        // Widescreen format (YouTube)
        var audienceLower = audience?.ToLowerInvariant() ?? "";
        if (audienceLower.Contains("young") || audienceLower.Contains("youth"))
        {
            return "Animated Full Sentences (Bottom, Colorful)";
        }

        return "Traditional Subtitles (Bottom, Standard)";
    }

    private string GenerateThumbnailPrompt(Brief brief)
    {
        var tone = brief.Tone.ToLowerInvariant();
        var topic = brief.Topic;

        var style = tone.Contains("professional") ? "professional, clean, modern design" :
                   tone.Contains("energetic") ? "vibrant, colorful, dynamic composition" :
                   tone.Contains("calm") ? "serene, minimal, soothing colors" :
                   "eye-catching, clear, professional";

        return $"YouTube thumbnail: {topic}. {style}. Bold text overlay with clear title. High contrast, attention-grabbing. 1280x720 resolution.";
    }

    private string GenerateSeoTitle(Brief brief)
    {
        var topic = brief.Topic;
        var goal = brief.Goal ?? "Learn About";

        // SEO best practices: 60 characters max, front-load keywords
        var title = $"{topic} - {goal} | Complete Guide";
        
        // Truncate if too long
        if (title.Length > 60)
        {
            title = topic.Length > 50 ? topic.Substring(0, 50) + "..." : topic;
        }

        return title;
    }

    private string GenerateSeoDescription(Brief brief, PlanSpec planSpec)
    {
        var durationMin = (int)Math.Ceiling(planSpec.TargetDuration.TotalMinutes);
        var topic = brief.Topic;
        var audience = brief.Audience ?? "everyone";

        // SEO best practices: 155 characters max, include keywords and value prop
        var description = $"Learn about {topic} in this {durationMin}-minute guide for {audience}. " +
                         $"Clear explanations, practical insights, and actionable takeaways.";

        // Truncate if too long
        if (description.Length > 155)
        {
            description = description.Substring(0, 152) + "...";
        }

        return description;
    }

    private List<string> GenerateSeoTags(Brief brief)
    {
        var tags = new List<string>();
        var topic = brief.Topic;

        // Extract key terms from topic
        var words = topic.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Add primary keyword
        tags.Add(topic.ToLowerInvariant());

        // Add individual significant words (3+ characters)
        foreach (var word in words.Where(w => w.Length >= 3))
        {
            tags.Add(word.ToLowerInvariant());
        }

        // Add related terms based on goal/audience
        if (brief.Goal?.Contains("Educational", StringComparison.OrdinalIgnoreCase) == true)
        {
            tags.AddRange(new[] { "tutorial", "guide", "how-to", "learn" });
        }

        if (brief.Audience?.Contains("Beginners", StringComparison.OrdinalIgnoreCase) == true)
        {
            tags.AddRange(new[] { "beginner", "basics", "introduction" });
        }

        // Add tone-based tags
        if (brief.Tone.Contains("Professional", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("professional");
        }

        // Limit to 10-15 most relevant tags, remove duplicates
        return tags.Distinct().Take(15).ToList();
    }
}
