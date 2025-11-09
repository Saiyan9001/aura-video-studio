using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Interfaces;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Aura.Core.Services.PromptManagement;
using Aura.Core.Services.ScriptEnhancement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// API controller for advanced script generation and refinement features
/// </summary>
[ApiController]
[Route("api/advanced-script")]
public class AdvancedScriptController : ControllerBase
{
    private readonly ILogger<AdvancedScriptController> _logger;
    private readonly IScriptLlmProvider _llmProvider;
    private readonly AdvancedScriptPromptBuilder _promptBuilder;
    private readonly ScriptQualityAnalyzer _qualityAnalyzer;
    private readonly IterativeScriptRefinementService _refinementService;

    public AdvancedScriptController(
        ILogger<AdvancedScriptController> logger,
        IScriptLlmProvider llmProvider,
        AdvancedScriptPromptBuilder promptBuilder,
        ScriptQualityAnalyzer qualityAnalyzer,
        IterativeScriptRefinementService refinementService)
    {
        _logger = logger;
        _llmProvider = llmProvider;
        _promptBuilder = promptBuilder;
        _qualityAnalyzer = qualityAnalyzer;
        _refinementService = refinementService;
    }

    /// <summary>
    /// Generate a high-quality script with advanced prompting
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateScript(
        [FromBody] AdvancedGenerateScriptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating script for topic: {Topic}, type: {Type}, CorrelationId: {CorrelationId}",
                request.Brief.Topic, request.VideoType, HttpContext.TraceIdentifier);

            var scriptRequest = new ScriptGenerationRequest
            {
                Brief = request.Brief,
                PlanSpec = request.PlanSpec,
                ModelOverride = request.ModelOverride,
                TemperatureOverride = request.TemperatureOverride,
                CorrelationId = HttpContext.TraceIdentifier
            };

            var script = await _llmProvider.GenerateScriptAsync(scriptRequest, cancellationToken);

            var qualityMetrics = await _qualityAnalyzer.AnalyzeAsync(
                script, 
                request.Brief, 
                request.PlanSpec, 
                cancellationToken);

            _logger.LogInformation("Script generated successfully. Quality score: {Score:F1}, CorrelationId: {CorrelationId}",
                qualityMetrics.OverallScore, HttpContext.TraceIdentifier);

            return Ok(new
            {
                success = true,
                script,
                qualityMetrics,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating script, CorrelationId: {CorrelationId}", HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Analyze script quality without regeneration
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeScript(
        [FromBody] AnalyzeScriptQualityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Analyzing script quality, CorrelationId: {CorrelationId}", 
                HttpContext.TraceIdentifier);

            var metrics = await _qualityAnalyzer.AnalyzeAsync(
                request.Script,
                request.Brief,
                request.PlanSpec,
                cancellationToken);

            var readingSpeed = _qualityAnalyzer.ValidateReadingSpeed(request.Script);
            var sceneCount = _qualityAnalyzer.ValidateSceneCount(request.Script, request.PlanSpec);
            var visualPrompts = _qualityAnalyzer.ValidateVisualPrompts(request.Script);
            var narrativeFlow = _qualityAnalyzer.ValidateNarrativeFlow(request.Script);
            var contentCheck = _qualityAnalyzer.ValidateContentAppropriateness(request.Script);

            return Ok(new
            {
                success = true,
                metrics,
                validations = new
                {
                    readingSpeed,
                    sceneCount,
                    visualPrompts,
                    narrativeFlow,
                    contentCheck
                },
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing script, CorrelationId: {CorrelationId}", 
                HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Auto-refine script based on quality analysis
    /// </summary>
    [HttpPost("refine")]
    public async Task<IActionResult> RefineScript(
        [FromBody] AdvancedRefineScriptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting script refinement, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);

            var result = await _refinementService.AutoRefineScriptAsync(
                request.Script,
                request.Brief,
                request.PlanSpec,
                request.VideoType,
                request.Config,
                cancellationToken);

            return Ok(new
            {
                success = result.Success,
                result,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refining script, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Manual improvement with specific goal
    /// </summary>
    [HttpPost("improve")]
    public async Task<IActionResult> ImproveScript(
        [FromBody] AdvancedImproveScriptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual script improvement: {Goal}, CorrelationId: {CorrelationId}",
                request.ImprovementGoal, HttpContext.TraceIdentifier);

            var improvedScript = await _refinementService.ImproveScriptAsync(
                request.Script,
                request.ImprovementGoal,
                request.Brief,
                request.PlanSpec,
                request.VideoType,
                cancellationToken);

            var metrics = await _qualityAnalyzer.AnalyzeAsync(
                improvedScript,
                request.Brief,
                request.PlanSpec,
                cancellationToken);

            return Ok(new
            {
                success = true,
                script = improvedScript,
                qualityMetrics = metrics,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error improving script, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Regenerate a specific scene
    /// </summary>
    [HttpPost("regenerate-scene")]
    public async Task<IActionResult> RegenerateScene(
        [FromBody] AdvancedRegenerateSceneRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Regenerating scene {SceneNumber}, CorrelationId: {CorrelationId}",
                request.SceneNumber, HttpContext.TraceIdentifier);

            var regeneratedScene = await _refinementService.RegenerateSceneAsync(
                request.Script,
                request.SceneNumber,
                request.ImprovementGoal,
                request.Brief,
                request.PlanSpec,
                cancellationToken);

            return Ok(new
            {
                success = true,
                scene = regeneratedScene,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating scene, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Generate multiple script variations for A/B testing
    /// </summary>
    [HttpPost("variations")]
    public async Task<IActionResult> GenerateVariations(
        [FromBody] AdvancedGenerateVariationsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating {Count} script variations, CorrelationId: {CorrelationId}",
                request.VariationCount, HttpContext.TraceIdentifier);

            var variations = await _refinementService.GenerateScriptVariationsAsync(
                request.Script,
                request.Brief,
                request.PlanSpec,
                request.VideoType,
                request.VariationCount,
                cancellationToken);

            return Ok(new
            {
                success = true,
                variations,
                count = variations.Count,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating variations, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Optimize the opening hook
    /// </summary>
    [HttpPost("optimize-hook")]
    public async Task<IActionResult> OptimizeHook(
        [FromBody] AdvancedOptimizeHookRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Optimizing hook for {Seconds} seconds, CorrelationId: {CorrelationId}",
                request.TargetSeconds, HttpContext.TraceIdentifier);

            var optimizedScript = await _refinementService.OptimizeHookAsync(
                request.Script,
                request.Brief,
                request.TargetSeconds,
                cancellationToken);

            return Ok(new
            {
                success = true,
                script = optimizedScript,
                correlationId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing hook, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                correlationId = HttpContext.TraceIdentifier
            });
        }
    }
}

/// <summary>
/// Request DTOs for advanced script generation API
/// </summary>
public record AdvancedGenerateScriptRequest
{
    public Brief Brief { get; init; } = null!;
    public PlanSpec PlanSpec { get; init; } = null!;
    public VideoType VideoType { get; init; } = VideoType.General;
    public string? ModelOverride { get; init; }
    public double? TemperatureOverride { get; init; }
}

public record AnalyzeScriptQualityRequest
{
    public Script Script { get; init; } = null!;
    public Brief Brief { get; init; } = null!;
    public PlanSpec PlanSpec { get; init; } = null!;
}

public record AdvancedRefineScriptRequest
{
    public Script Script { get; init; } = null!;
    public Brief Brief { get; init; } = null!;
    public PlanSpec PlanSpec { get; init; } = null!;
    public VideoType VideoType { get; init; } = VideoType.General;
    public ScriptRefinementConfig? Config { get; init; }
}

public record AdvancedImproveScriptRequest
{
    public Script Script { get; init; } = null!;
    public Brief Brief { get; init; } = null!;
    public PlanSpec PlanSpec { get; init; } = null!;
    public VideoType VideoType { get; init; } = VideoType.General;
    public string ImprovementGoal { get; init; } = string.Empty;
}

public record AdvancedRegenerateSceneRequest
{
    public Script Script { get; init; } = null!;
    public int SceneNumber { get; init; }
    public string ImprovementGoal { get; init; } = string.Empty;
    public Brief Brief { get; init; } = null!;
    public PlanSpec PlanSpec { get; init; } = null!;
}

public record AdvancedGenerateVariationsRequest
{
    public Script Script { get; init; } = null!;
    public Brief Brief { get; init; } = null!;
    public PlanSpec PlanSpec { get; init; } = null!;
    public VideoType VideoType { get; init; } = VideoType.General;
    public int VariationCount { get; init; } = 3;
}

public record AdvancedOptimizeHookRequest
{
    public Script Script { get; init; } = null!;
    public Brief Brief { get; init; } = null!;
    public int TargetSeconds { get; init; } = 3;
}
