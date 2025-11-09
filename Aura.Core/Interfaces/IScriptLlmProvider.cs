using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Generation;

namespace Aura.Core.Interfaces;

/// <summary>
/// Interface for LLM providers focused on script generation
/// Extends beyond basic text completion to provide structured script output
/// </summary>
public interface IScriptLlmProvider
{
    /// <summary>
    /// Generate a structured script from a brief and plan specification
    /// </summary>
    /// <param name="request">Script generation request with brief and options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated script with scenes and metadata</returns>
    Task<Script> GenerateScriptAsync(ScriptGenerationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Get available models for this provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available model names</returns>
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validate provider configuration (API keys, connectivity, etc.)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with success status and error messages</returns>
    Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get provider metadata for UI display and selection
    /// </summary>
    /// <returns>Provider metadata including name, tier, capabilities</returns>
    ProviderMetadata GetProviderMetadata();

    /// <summary>
    /// Check if provider is currently available (online/offline, service status)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if provider is available for use</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Request for script generation
/// </summary>
public record ScriptGenerationRequest
{
    /// <summary>
    /// Brief containing topic, audience, goal, tone
    /// </summary>
    public Brief Brief { get; init; } = null!;

    /// <summary>
    /// Plan specification with duration, pacing, density
    /// </summary>
    public PlanSpec PlanSpec { get; init; } = null!;

    /// <summary>
    /// Optional model override (uses provider default if not specified)
    /// </summary>
    public string? ModelOverride { get; init; }

    /// <summary>
    /// Optional temperature override for generation randomness (0.0 - 2.0)
    /// </summary>
    public double? TemperatureOverride { get; init; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;
}

/// <summary>
/// Result of provider configuration validation
/// </summary>
public record ProviderValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of error messages if validation failed
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// List of warning messages (non-fatal issues)
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Metadata about a provider
/// </summary>
public record ProviderMetadata
{
    /// <summary>
    /// Provider name (OpenAI, Gemini, Ollama, RuleBased, etc.)
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Provider tier
    /// </summary>
    public ProviderTier Tier { get; init; }

    /// <summary>
    /// Whether provider requires internet connection
    /// </summary>
    public bool RequiresInternet { get; init; }

    /// <summary>
    /// Whether provider requires API key
    /// </summary>
    public bool RequiresApiKey { get; init; }

    /// <summary>
    /// List of capabilities (e.g., "streaming", "function-calling", "json-mode")
    /// </summary>
    public List<string> Capabilities { get; init; } = new();

    /// <summary>
    /// Default model for this provider
    /// </summary>
    public string DefaultModel { get; init; } = string.Empty;

    /// <summary>
    /// Estimated cost per 1K tokens (USD)
    /// </summary>
    public decimal EstimatedCostPer1KTokens { get; init; }
}
