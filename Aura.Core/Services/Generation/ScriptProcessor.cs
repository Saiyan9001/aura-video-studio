using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Generation;

/// <summary>
/// Processes and optimizes generated scripts for video production
/// </summary>
public class ScriptProcessor
{
    private readonly ILogger<ScriptProcessor> _logger;
    
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SentenceSpacingRegex = new(@"([.!?])\s*([A-Z])", RegexOptions.Compiled);
    private static readonly Regex PunctuationSpacingRegex = new(@"([,;:])\s*", RegexOptions.Compiled);
    private static readonly Regex WordRegex = new(@"\b[a-zA-Z]{3,}\b", RegexOptions.Compiled);

    public ScriptProcessor(ILogger<ScriptProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate and adjust scene timing to match target duration
    /// </summary>
    public Script ValidateSceneTiming(Script script, TimeSpan targetDuration)
    {
        _logger.LogInformation("Validating scene timing for script with {SceneCount} scenes, target duration: {Duration}s",
            script.Scenes.Count, targetDuration.TotalSeconds);

        if (script.Scenes.Count == 0)
        {
            _logger.LogWarning("Script has no scenes, cannot validate timing");
            return script;
        }

        var currentTotalDuration = script.Scenes.Sum(s => s.Duration.TotalSeconds);
        var targetSeconds = targetDuration.TotalSeconds;

        if (Math.Abs(currentTotalDuration - targetSeconds) < 1.0)
        {
            _logger.LogDebug("Scene timing is already within acceptable range");
            return script with { TotalDuration = targetDuration };
        }

        var scaleFactor = targetSeconds / currentTotalDuration;
        _logger.LogInformation("Adjusting scene durations by factor of {Factor:F2}", scaleFactor);

        var adjustedScenes = script.Scenes.Select(scene =>
        {
            var newDuration = TimeSpan.FromSeconds(scene.Duration.TotalSeconds * scaleFactor);
            newDuration = TimeSpan.FromSeconds(Math.Max(2.0, newDuration.TotalSeconds));
            
            return scene with { Duration = newDuration };
        }).ToList();

        var newTotalDuration = TimeSpan.FromSeconds(adjustedScenes.Sum(s => s.Duration.TotalSeconds));

        return script with 
        { 
            Scenes = adjustedScenes,
            TotalDuration = newTotalDuration
        };
    }

    /// <summary>
    /// Optimize narration flow for natural pacing and readability
    /// </summary>
    public Script OptimizeNarrationFlow(Script script)
    {
        _logger.LogInformation("Optimizing narration flow for {SceneCount} scenes", script.Scenes.Count);

        var optimizedScenes = script.Scenes.Select(scene =>
        {
            var narration = scene.Narration;
            
            narration = MultipleSpacesRegex.Replace(narration, " ");
            narration = narration.Trim();
            narration = SentenceSpacingRegex.Replace(narration, "$1 $2");
            narration = PunctuationSpacingRegex.Replace(narration, "$1 ");
            
            return scene with { Narration = narration };
        }).ToList();

        return script with { Scenes = optimizedScenes };
    }

    /// <summary>
    /// Enhance visual prompts for better image generation
    /// </summary>
    public Script EnhanceVisualPrompts(Script script, string visualProvider)
    {
        _logger.LogInformation("Enhancing visual prompts for {Provider}", visualProvider);

        var enhancedScenes = script.Scenes.Select(scene =>
        {
            var enhancedPrompt = scene.VisualPrompt;

            if (visualProvider.Contains("Stability", StringComparison.OrdinalIgnoreCase) ||
                visualProvider.Contains("Diffusion", StringComparison.OrdinalIgnoreCase))
            {
                if (!enhancedPrompt.Contains("high quality", StringComparison.OrdinalIgnoreCase))
                {
                    enhancedPrompt = $"{enhancedPrompt}, high quality, detailed, professional";
                }
            }
            else if (visualProvider.Contains("Stock", StringComparison.OrdinalIgnoreCase))
            {
                var keywords = ExtractKeywords(scene.Narration);
                enhancedPrompt = string.Join(" ", keywords.Take(5));
            }

            return scene with { VisualPrompt = enhancedPrompt };
        }).ToList();

        return script with { Scenes = enhancedScenes };
    }

    /// <summary>
    /// Apply appropriate transitions based on video style
    /// </summary>
    public Script ApplyTransitions(Script script, string videoStyle)
    {
        _logger.LogInformation("Applying transitions for style: {Style}", videoStyle);

        var style = videoStyle.ToLowerInvariant();
        
        var defaultTransition = style switch
        {
            var s when s.Contains("fast") || s.Contains("dynamic") => TransitionType.Cut,
            var s when s.Contains("smooth") || s.Contains("cinematic") => TransitionType.Dissolve,
            var s when s.Contains("professional") => TransitionType.Fade,
            _ => TransitionType.Cut
        };

        var scenesWithTransitions = new List<ScriptScene>();
        
        for (int i = 0; i < script.Scenes.Count; i++)
        {
            var scene = script.Scenes[i];
            var transition = defaultTransition;

            if (i == script.Scenes.Count - 1)
            {
                transition = TransitionType.Fade;
            }
            else if (i == 0 && style.Contains("cinematic"))
            {
                transition = TransitionType.Fade;
            }

            scenesWithTransitions.Add(scene with { Transition = transition });
        }

        return script with { Scenes = scenesWithTransitions };
    }

    /// <summary>
    /// Calculate reading speed requirements for voice synthesis
    /// </summary>
    public double CalculateReadingSpeed(string narration, TimeSpan duration)
    {
        var wordCount = CountWords(narration);
        var minutes = duration.TotalMinutes;

        if (minutes == 0)
        {
            return 150.0;
        }

        var wordsPerMinute = wordCount / minutes;
        
        _logger.LogDebug("Calculated reading speed: {WPM} words/minute for {Words} words in {Minutes:F2} minutes",
            wordsPerMinute, wordCount, minutes);

        return wordsPerMinute;
    }

    /// <summary>
    /// Ensure scenes are balanced in duration and content
    /// </summary>
    public Script EnsureSceneBalance(Script script)
    {
        _logger.LogInformation("Ensuring scene balance for {SceneCount} scenes", script.Scenes.Count);

        if (script.Scenes.Count < 2)
        {
            return script;
        }

        var averageDuration = script.Scenes.Average(s => s.Duration.TotalSeconds);
        var averageWordCount = script.Scenes.Average(s => CountWords(s.Narration));

        var balancedScenes = script.Scenes.Select(scene =>
        {
            var wordCount = CountWords(scene.Narration);
            var durationSeconds = scene.Duration.TotalSeconds;

            if (wordCount > averageWordCount * 1.5 && durationSeconds < averageDuration)
            {
                var newDuration = TimeSpan.FromSeconds(Math.Min(durationSeconds * 1.2, averageDuration * 1.3));
                _logger.LogDebug("Scene {Number}: Extending duration from {Old}s to {New}s (high word count)",
                    scene.Number, durationSeconds, newDuration.TotalSeconds);
                return scene with { Duration = newDuration };
            }
            else if (wordCount < averageWordCount * 0.5 && durationSeconds > averageDuration)
            {
                var newDuration = TimeSpan.FromSeconds(Math.Max(durationSeconds * 0.8, averageDuration * 0.7));
                _logger.LogDebug("Scene {Number}: Reducing duration from {Old}s to {New}s (low word count)",
                    scene.Number, durationSeconds, newDuration.TotalSeconds);
                return scene with { Duration = newDuration };
            }

            return scene;
        }).ToList();

        var newTotalDuration = TimeSpan.FromSeconds(balancedScenes.Sum(s => s.Duration.TotalSeconds));

        return script with 
        { 
            Scenes = balancedScenes,
            TotalDuration = newTotalDuration
        };
    }

    /// <summary>
    /// Count words in text
    /// </summary>
    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Extract keywords from text for visual prompts
    /// </summary>
    private List<string> ExtractKeywords(string text)
    {
        var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
            "been", "being", "have", "has", "had", "do", "does", "did", "will",
            "would", "should", "could", "may", "might", "can", "this", "that",
            "these", "those", "it", "its"
        };

        var words = WordRegex.Matches(text)
            .Cast<Match>()
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => !commonWords.Contains(w))
            .Distinct()
            .ToList();

        return words;
    }
}
