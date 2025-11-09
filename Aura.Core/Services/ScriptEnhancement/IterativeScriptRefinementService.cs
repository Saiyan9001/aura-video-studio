using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Interfaces;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Aura.Core.Services.PromptManagement;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.ScriptEnhancement;

/// <summary>
/// Service for iteratively refining scripts based on quality analysis
/// Implements automated improvement cycles and manual refinement
/// </summary>
public class IterativeScriptRefinementService
{
    private readonly ILogger<IterativeScriptRefinementService> _logger;
    private readonly ScriptQualityAnalyzer _qualityAnalyzer;
    private readonly AdvancedScriptPromptBuilder _promptBuilder;
    private readonly IScriptLlmProvider _llmProvider;

    public IterativeScriptRefinementService(
        ILogger<IterativeScriptRefinementService> logger,
        ScriptQualityAnalyzer qualityAnalyzer,
        AdvancedScriptPromptBuilder promptBuilder,
        IScriptLlmProvider llmProvider)
    {
        _logger = logger;
        _qualityAnalyzer = qualityAnalyzer;
        _promptBuilder = promptBuilder;
        _llmProvider = llmProvider;
    }

    /// <summary>
    /// Auto-refine script by analyzing quality and improving weak areas
    /// </summary>
    public async Task<ScriptRefinementResult> AutoRefineScriptAsync(
        Script originalScript,
        Brief brief,
        PlanSpec planSpec,
        VideoType videoType,
        ScriptRefinementConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new ScriptRefinementConfig();
        config.Validate();

        _logger.LogInformation("Starting auto-refinement for script: {Title}", originalScript.Title);

        var result = new ScriptRefinementResult
        {
            Success = true,
            TotalPasses = 0
        };

        var startTime = DateTime.UtcNow;
        var currentScript = originalScript;
        var previousMetrics = await _qualityAnalyzer.AnalyzeAsync(currentScript, brief, planSpec, cancellationToken);
        
        result.IterationMetrics.Add(previousMetrics);
        _logger.LogInformation("Initial quality score: {Score:F1}", previousMetrics.OverallScore);

        for (int pass = 1; pass <= config.MaxRefinementPasses; pass++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Success = false;
                result.StopReason = "Cancelled by user";
                break;
            }

            _logger.LogInformation("Starting refinement pass {Pass}/{MaxPasses}", pass, config.MaxRefinementPasses);

            if (previousMetrics.OverallScore >= config.QualityThreshold)
            {
                result.StopReason = $"Quality threshold met: {previousMetrics.OverallScore:F1} >= {config.QualityThreshold}";
                _logger.LogInformation(result.StopReason);
                break;
            }

            try
            {
                var weaknesses = IdentifyWeaknesses(previousMetrics);
                var refinedScript = await RefineScriptAsync(
                    currentScript, 
                    weaknesses, 
                    brief,
                    planSpec,
                    videoType, 
                    cancellationToken);

                var newMetrics = await _qualityAnalyzer.AnalyzeAsync(refinedScript, brief, planSpec, cancellationToken);
                newMetrics.Iteration = pass;
                result.IterationMetrics.Add(newMetrics);

                var improvement = newMetrics.OverallScore - previousMetrics.OverallScore;
                _logger.LogInformation("Pass {Pass} complete. Quality: {Old:F1} → {New:F1} (Δ {Improvement:+F1})",
                    pass, previousMetrics.OverallScore, newMetrics.OverallScore, improvement);

                if (improvement < config.MinimumImprovement)
                {
                    result.StopReason = $"Minimal improvement achieved ({improvement:F1} < {config.MinimumImprovement})";
                    _logger.LogInformation(result.StopReason);
                    break;
                }

                currentScript = refinedScript;
                previousMetrics = newMetrics;
                result.TotalPasses = pass;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during refinement pass {Pass}", pass);
                result.Success = false;
                result.ErrorMessage = $"Refinement failed at pass {pass}: {ex.Message}";
                break;
            }
        }

        if (result.TotalPasses == config.MaxRefinementPasses && string.IsNullOrEmpty(result.StopReason))
        {
            result.StopReason = $"Maximum passes reached ({config.MaxRefinementPasses})";
        }

        result.FinalScript = ConvertScriptToText(currentScript);
        result.TotalDuration = DateTime.UtcNow - startTime;
        result.CritiqueSummary = GenerateCritiqueSummary(result.IterationMetrics);

        _logger.LogInformation("Auto-refinement complete. Final score: {Score:F1}, Passes: {Passes}",
            result.FinalMetrics?.OverallScore ?? 0, result.TotalPasses);

        return result;
    }

    /// <summary>
    /// Manual refinement triggered by user with specific improvement goal
    /// </summary>
    public async Task<Script> ImproveScriptAsync(
        Script script,
        string improvementGoal,
        Brief brief,
        PlanSpec planSpec,
        VideoType videoType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual improvement requested: {Goal}", improvementGoal);

        var weaknesses = new List<string> { improvementGoal };
        return await RefineScriptAsync(script, weaknesses, brief, planSpec, videoType, cancellationToken);
    }

    /// <summary>
    /// Regenerate a specific scene while preserving context
    /// </summary>
    public async Task<ScriptScene> RegenerateSceneAsync(
        Script script,
        int sceneNumber,
        string improvementGoal,
        Brief brief,
        PlanSpec planSpec,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Regenerating scene {SceneNumber} with goal: {Goal}", sceneNumber, improvementGoal);

        var scene = script.Scenes.FirstOrDefault(s => s.Number == sceneNumber);
        if (scene == null)
        {
            throw new ArgumentException($"Scene {sceneNumber} not found");
        }

        var previousScene = sceneNumber > 1 ? script.Scenes[sceneNumber - 2] : null;
        var nextScene = sceneNumber < script.Scenes.Count ? script.Scenes[sceneNumber] : null;

        var prompt = _promptBuilder.BuildSceneRegenerationPrompt(
            sceneNumber,
            scene.Narration,
            previousScene?.Narration ?? "Start of video",
            nextScene?.Narration ?? "End of video",
            improvementGoal);

        var request = new ScriptGenerationRequest
        {
            Brief = brief,
            PlanSpec = planSpec with { TargetDuration = scene.Duration },
            CorrelationId = Guid.NewGuid().ToString()
        };

        var regeneratedScript = await _llmProvider.GenerateScriptAsync(request, cancellationToken);

        if (regeneratedScript.Scenes.Count > 0)
        {
            var regeneratedScene = regeneratedScript.Scenes[0] with
            {
                Number = sceneNumber,
                Duration = scene.Duration
            };

            _logger.LogInformation("Scene {SceneNumber} regenerated successfully", sceneNumber);
            return regeneratedScene;
        }

        _logger.LogWarning("Scene regeneration produced no results, returning original");
        return scene;
    }

    /// <summary>
    /// Generate multiple variations of a script for A/B testing
    /// </summary>
    public async Task<List<ScriptVariation>> GenerateScriptVariationsAsync(
        Script originalScript,
        Brief brief,
        PlanSpec planSpec,
        VideoType videoType,
        int variationCount = 3,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {Count} script variations", variationCount);

        var variations = new List<ScriptVariation>();
        var originalText = ConvertScriptToText(originalScript);

        var focuses = new[]
        {
            "More emotional and storytelling-focused",
            "More data-driven and statistical",
            "More conversational and casual",
            "More authoritative and professional",
            "More action-oriented with stronger CTAs"
        };

        for (int i = 0; i < Math.Min(variationCount, focuses.Length); i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var focus = focuses[i];
                var prompt = _promptBuilder.BuildVariationPrompt(originalText, focus);

                var request = new ScriptGenerationRequest
                {
                    Brief = brief with { Topic = $"{brief.Topic} (Variation: {focus})" },
                    PlanSpec = planSpec,
                    CorrelationId = Guid.NewGuid().ToString()
                };

                var variationScript = await _llmProvider.GenerateScriptAsync(request, cancellationToken);
                var metrics = await _qualityAnalyzer.AnalyzeAsync(variationScript, brief, planSpec, cancellationToken);

                variations.Add(new ScriptVariation
                {
                    VariationId = $"V{i + 1}",
                    Name = $"Variation {i + 1}: {focus}",
                    Script = variationScript,
                    QualityScore = metrics.OverallScore,
                    Focus = focus
                });

                _logger.LogInformation("Generated variation {Index}: {Focus} (score: {Score:F1})",
                    i + 1, focus, metrics.OverallScore);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate variation {Index}", i + 1);
            }
        }

        return variations;
    }

    /// <summary>
    /// Optimize the opening hook of a script
    /// </summary>
    public async Task<Script> OptimizeHookAsync(
        Script script,
        Brief brief,
        int targetSeconds = 3,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing hook for first {Seconds} seconds", targetSeconds);

        if (script.Scenes.Count == 0)
        {
            return script;
        }

        var firstScene = script.Scenes[0];
        var currentHook = firstScene.Narration;

        var prompt = _promptBuilder.BuildHookOptimizationPrompt(currentHook, brief, targetSeconds);

        var hookDuration = TimeSpan.FromSeconds(targetSeconds);
        var hookRequest = new ScriptGenerationRequest
        {
            Brief = brief with { Topic = $"Hook: {brief.Topic}" },
            PlanSpec = new PlanSpec(hookDuration, Pacing.Fast, Density.Balanced, "attention-grabbing"),
            CorrelationId = Guid.NewGuid().ToString()
        };

        var hookScript = await _llmProvider.GenerateScriptAsync(hookRequest, cancellationToken);

        if (hookScript.Scenes.Count > 0)
        {
            var optimizedScene = firstScene with
            {
                Narration = hookScript.Scenes[0].Narration
            };

            var updatedScenes = new List<ScriptScene> { optimizedScene };
            updatedScenes.AddRange(script.Scenes.Skip(1));

            _logger.LogInformation("Hook optimized successfully");
            return script with { Scenes = updatedScenes };
        }

        _logger.LogWarning("Hook optimization failed, returning original script");
        return script;
    }

    private async Task<Script> RefineScriptAsync(
        Script script,
        List<string> weaknesses,
        Brief brief,
        PlanSpec planSpec,
        VideoType videoType,
        CancellationToken cancellationToken)
    {
        var scriptText = ConvertScriptToText(script);
        var refinementPrompt = _promptBuilder.BuildRefinementPrompt(scriptText, weaknesses, videoType);

        var systemPrompt = _promptBuilder.BuildSystemPrompt(videoType, brief, planSpec);

        var request = new ScriptGenerationRequest
        {
            Brief = brief with { Topic = $"{brief.Topic} (Refinement)" },
            PlanSpec = planSpec,
            CorrelationId = Guid.NewGuid().ToString()
        };

        var refinedScript = await _llmProvider.GenerateScriptAsync(request, cancellationToken);

        return refinedScript with
        {
            Title = script.Title,
            CorrelationId = script.CorrelationId
        };
    }

    private List<string> IdentifyWeaknesses(ScriptQualityMetrics metrics)
    {
        var weaknesses = new List<string>();

        if (metrics.NarrativeCoherence < 75)
        {
            weaknesses.Add("Improve narrative coherence and logical flow between scenes");
        }

        if (metrics.PacingAppropriateness < 75)
        {
            weaknesses.Add("Adjust pacing to ensure natural reading speed (150-160 WPM)");
        }

        if (metrics.AudienceAlignment < 75)
        {
            weaknesses.Add("Strengthen audience connection with more direct address and relevant examples");
        }

        if (metrics.VisualClarity < 75)
        {
            weaknesses.Add("Add more specific and detailed visual descriptions");
        }

        if (metrics.EngagementPotential < 75)
        {
            weaknesses.Add("Enhance engagement with stronger hooks and varied sentence structure");
        }

        if (weaknesses.Count == 0)
        {
            weaknesses.Add("Polish overall quality and clarity");
        }

        return weaknesses;
    }

    private string ConvertScriptToText(Script script)
    {
        var lines = new List<string>();
        
        foreach (var scene in script.Scenes)
        {
            lines.Add($"## Scene {scene.Number}");
            lines.Add($"Narration: {scene.Narration}");
            lines.Add($"Visual: {scene.VisualPrompt}");
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    private string GenerateCritiqueSummary(List<ScriptQualityMetrics> iterations)
    {
        if (iterations.Count == 0)
        {
            return "No iterations completed";
        }

        var initial = iterations[0];
        var final = iterations[^1];
        var improvement = final.OverallScore - initial.OverallScore;

        return $"Quality improved from {initial.OverallScore:F1} to {final.OverallScore:F1} (+{improvement:F1}) over {iterations.Count - 1} refinement passes. " +
               $"Key improvements: {string.Join(", ", final.Strengths.Take(3))}";
    }
}

/// <summary>
/// Script variation for A/B testing
/// </summary>
public class ScriptVariation
{
    public string VariationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Script Script { get; set; } = null!;
    public double QualityScore { get; set; }
    public string Focus { get; set; } = string.Empty;
}
