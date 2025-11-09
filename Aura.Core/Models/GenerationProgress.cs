using System;
using System.Collections.Generic;

namespace Aura.Core.Models;

/// <summary>
/// Detailed progress information for video generation pipeline
/// Supports weighted progress calculation, substage tracking, and time estimation
/// </summary>
public record GenerationProgress
{
    /// <summary>
    /// Current stage of generation (Brief, Script, TTS, Images, Rendering, Complete)
    /// </summary>
    public string Stage { get; init; } = string.Empty;

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public double OverallPercent { get; init; }

    /// <summary>
    /// Progress within current stage (0-100)
    /// </summary>
    public double StagePercent { get; init; }

    /// <summary>
    /// Human-readable message describing current operation
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Substage details (e.g., "Generating scene 3 of 5")
    /// </summary>
    public string? SubstageDetail { get; init; }

    /// <summary>
    /// Current item being processed
    /// </summary>
    public int? CurrentItem { get; init; }

    /// <summary>
    /// Total items to process in current stage
    /// </summary>
    public int? TotalItems { get; init; }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Elapsed time since job start
    /// </summary>
    public TimeSpan? ElapsedTime { get; init; }

    /// <summary>
    /// Timestamp of this progress update
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional metadata about current operation
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Stage weights for progress calculation
/// Total should sum to 100 for accurate overall progress
/// </summary>
public static class StageWeights
{
    public const double Brief = 5.0;        // 0-5%: Brief validation
    public const double Script = 20.0;      // 5-25%: Script generation
    public const double TTS = 30.0;         // 25-55%: TTS synthesis (longest stage)
    public const double Images = 25.0;      // 55-80%: Image generation/selection
    public const double Rendering = 15.0;   // 80-95%: Video rendering
    public const double PostProcess = 5.0;  // 95-100%: Post-processing and finalization

    /// <summary>
    /// Calculate overall progress given stage and stage completion percentage
    /// </summary>
    public static double CalculateOverallProgress(string stage, double stagePercent)
    {
        double baseProgress = stage.ToLowerInvariant() switch
        {
            "brief" or "initialization" => 0.0,
            "script" or "planning" => Brief,
            "tts" or "audio" or "voice" => Brief + Script,
            "images" or "visuals" or "assets" => Brief + Script + TTS,
            "rendering" or "render" or "encode" => Brief + Script + TTS + Images,
            "postprocess" or "post" or "complete" => Brief + Script + TTS + Images + Rendering,
            _ => 0.0
        };

        double stageWeight = stage.ToLowerInvariant() switch
        {
            "brief" or "initialization" => Brief,
            "script" or "planning" => Script,
            "tts" or "audio" or "voice" => TTS,
            "images" or "visuals" or "assets" => Images,
            "rendering" or "render" or "encode" => Rendering,
            "postprocess" or "post" or "complete" => PostProcess,
            _ => 0.0
        };

        return Math.Min(100.0, baseProgress + (stagePercent / 100.0) * stageWeight);
    }
}

/// <summary>
/// Helper for creating progress updates with consistent formatting
/// </summary>
public static class ProgressBuilder
{
    public static GenerationProgress CreateBriefProgress(double percent, string message, string? correlationId = null)
    {
        return new GenerationProgress
        {
            Stage = "Brief",
            OverallPercent = StageWeights.CalculateOverallProgress("brief", percent),
            StagePercent = percent,
            Message = message,
            CorrelationId = correlationId
        };
    }

    public static GenerationProgress CreateScriptProgress(double percent, string message, string? correlationId = null)
    {
        return new GenerationProgress
        {
            Stage = "Script",
            OverallPercent = StageWeights.CalculateOverallProgress("script", percent),
            StagePercent = percent,
            Message = message,
            CorrelationId = correlationId
        };
    }

    public static GenerationProgress CreateTtsProgress(
        double percent, 
        string message, 
        int? currentScene = null, 
        int? totalScenes = null,
        string? correlationId = null)
    {
        return new GenerationProgress
        {
            Stage = "TTS",
            OverallPercent = StageWeights.CalculateOverallProgress("tts", percent),
            StagePercent = percent,
            Message = message,
            SubstageDetail = currentScene.HasValue && totalScenes.HasValue 
                ? $"Synthesizing scene {currentScene} of {totalScenes}"
                : null,
            CurrentItem = currentScene,
            TotalItems = totalScenes,
            CorrelationId = correlationId
        };
    }

    public static GenerationProgress CreateImageProgress(
        double percent, 
        string message, 
        int? currentImage = null, 
        int? totalImages = null,
        string? correlationId = null)
    {
        return new GenerationProgress
        {
            Stage = "Images",
            OverallPercent = StageWeights.CalculateOverallProgress("images", percent),
            StagePercent = percent,
            Message = message,
            SubstageDetail = currentImage.HasValue && totalImages.HasValue 
                ? $"Generating image {currentImage} of {totalImages}"
                : null,
            CurrentItem = currentImage,
            TotalItems = totalImages,
            CorrelationId = correlationId
        };
    }

    public static GenerationProgress CreateRenderProgress(
        double percent, 
        string message, 
        TimeSpan? elapsed = null,
        TimeSpan? remaining = null,
        string? correlationId = null)
    {
        return new GenerationProgress
        {
            Stage = "Rendering",
            OverallPercent = StageWeights.CalculateOverallProgress("rendering", percent),
            StagePercent = percent,
            Message = message,
            ElapsedTime = elapsed,
            EstimatedTimeRemaining = remaining,
            CorrelationId = correlationId
        };
    }

    public static GenerationProgress CreateCompleteProgress(string? correlationId = null)
    {
        return new GenerationProgress
        {
            Stage = "Complete",
            OverallPercent = 100.0,
            StagePercent = 100.0,
            Message = "Video generation complete",
            CorrelationId = correlationId
        };
    }
}
