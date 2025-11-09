using System;
using System.Collections.Generic;
using System.Linq;

namespace Aura.Core.Errors;

/// <summary>
/// Exception thrown when a video generation pipeline encounters an error
/// </summary>
public class PipelineException : AuraException
{
    /// <summary>
    /// The stage of the pipeline where the error occurred
    /// </summary>
    public string Stage { get; }

    /// <summary>
    /// Number of tasks completed before failure
    /// </summary>
    public int CompletedTasks { get; }

    /// <summary>
    /// Total number of tasks in the pipeline
    /// </summary>
    public int TotalTasks { get; }

    /// <summary>
    /// Provider failures that led to this pipeline exception
    /// </summary>
    public List<ProviderException> ProviderFailures { get; }

    /// <summary>
    /// Time elapsed before failure occurred
    /// </summary>
    public TimeSpan ElapsedBeforeFailure { get; }

    public PipelineException(
        string stage,
        string message,
        int completedTasks = 0,
        int totalTasks = 0,
        string? userMessage = null,
        string? correlationId = null,
        bool isTransient = false,
        string[]? suggestedActions = null,
        List<ProviderException>? providerFailures = null,
        TimeSpan? elapsedBeforeFailure = null,
        Exception? innerException = null)
        : base(
            message,
            GenerateErrorCode(stage),
            userMessage ?? GenerateUserMessage(stage, message, completedTasks, totalTasks),
            correlationId,
            suggestedActions ?? GenerateDefaultSuggestedActions(stage, isTransient),
            isTransient,
            innerException)
    {
        Stage = stage;
        CompletedTasks = completedTasks;
        TotalTasks = totalTasks;
        ProviderFailures = providerFailures ?? new List<ProviderException>();
        ElapsedBeforeFailure = elapsedBeforeFailure ?? TimeSpan.Zero;

        WithContext("stage", stage);
        WithContext("completedTasks", completedTasks);
        WithContext("totalTasks", totalTasks);
        WithContext("providerFailureCount", ProviderFailures.Count);
        WithContext("elapsedSeconds", ElapsedBeforeFailure.TotalSeconds);
    }

    private static string GenerateErrorCode(string pipelineStage)
    {
        return pipelineStage switch
        {
            "Script" or "script" or "SCRIPT" => "E101",
            "TTS" or "tts" or "Tts" => "E201",
            "Visual" or "visual" or "VISUAL" => "E401",
            "Composition" or "composition" or "COMPOSITION" => "E501",
            "Render" or "render" or "RENDER" => "E502",
            _ => "E600"
        };
    }

    private static string GenerateUserMessage(string pipelineStage, string message, int completedTasks, int totalTasks)
    {
        if (totalTasks > 0)
        {
            return $"Video generation failed at {pipelineStage} stage ({completedTasks}/{totalTasks} tasks completed): {message}";
        }
        return $"Video generation failed at {pipelineStage} stage: {message}";
    }

    private static string[] GenerateDefaultSuggestedActions(string pipelineStage, bool isTransient)
    {
        if (isTransient)
        {
            return new[]
            {
                "Retry the video generation",
                "Check your internet connection if using cloud providers",
                "Verify all required providers are configured",
                "Try with simpler settings or shorter duration"
            };
        }

        return pipelineStage switch
        {
            "Script" or "script" or "SCRIPT" => new[]
            {
                "Check LLM provider configuration and API keys",
                "Try a different LLM provider",
                "Simplify your creative brief",
                "Check provider service status"
            },
            "TTS" or "tts" or "Tts" => new[]
            {
                "Check TTS provider configuration and API keys",
                "Try a different TTS provider or voice",
                "Verify audio output directory has write permissions",
                "Check TTS provider service status"
            },
            "Visual" or "visual" or "VISUAL" => new[]
            {
                "Check image provider configuration",
                "Try a different image provider",
                "Verify sufficient disk space for images",
                "Check image provider service status"
            },
            "Render" or "render" or "RENDER" or "Composition" or "composition" or "COMPOSITION" => new[]
            {
                "Verify FFmpeg is installed and accessible",
                "Check output directory has write permissions",
                "Verify sufficient disk space (at least 1GB free)",
                "Try with lower resolution or quality settings"
            },
            _ => new[]
            {
                "Check system logs for more details",
                "Verify all required dependencies are installed",
                "Try with different settings",
                "Contact support if the issue persists"
            }
        };
    }

    /// <summary>
    /// Creates a PipelineException for script generation failure
    /// </summary>
    public static PipelineException ScriptGenerationFailed(string message, string? correlationId = null, Exception? innerException = null)
    {
        return new PipelineException(
            "Script",
            message,
            userMessage: $"Failed to generate video script: {message}",
            correlationId: correlationId,
            isTransient: false,
            innerException: innerException);
    }

    /// <summary>
    /// Creates a PipelineException for TTS synthesis failure
    /// </summary>
    public static PipelineException TtsFailed(string message, string? correlationId = null, Exception? innerException = null)
    {
        return new PipelineException(
            "TTS",
            message,
            userMessage: $"Failed to generate audio narration: {message}",
            correlationId: correlationId,
            isTransient: false,
            innerException: innerException);
    }

    /// <summary>
    /// Creates a PipelineException for visual generation failure
    /// </summary>
    public static PipelineException VisualGenerationFailed(string message, string? correlationId = null, Exception? innerException = null)
    {
        return new PipelineException(
            "Visual",
            message,
            userMessage: $"Failed to generate or fetch visuals: {message}",
            correlationId: correlationId,
            isTransient: false,
            innerException: innerException);
    }

    /// <summary>
    /// Creates a PipelineException for render failure
    /// </summary>
    public static PipelineException RenderFailed(string message, string? correlationId = null, Exception? innerException = null)
    {
        return new PipelineException(
            "Render",
            message,
            userMessage: $"Failed to render final video: {message}",
            correlationId: correlationId,
            isTransient: false,
            innerException: innerException);
    }

    public override Dictionary<string, object> ToErrorResponse()
    {
        var response = base.ToErrorResponse();
        response["pipeline"] = new
        {
            stage = Stage,
            completedTasks = CompletedTasks,
            totalTasks = TotalTasks,
            providerFailures = ProviderFailures.Select(pf => new
            {
                providerName = pf.ProviderName,
                providerType = pf.Type.ToString(),
                errorCode = pf.SpecificErrorCode,
                message = pf.UserMessage
            }).ToList(),
            elapsedSeconds = ElapsedBeforeFailure.TotalSeconds
        };
        return response;
    }
}
