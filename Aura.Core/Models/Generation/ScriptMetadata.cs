using System;

namespace Aura.Core.Models.Generation;

/// <summary>
/// Metadata about script generation including provider information and costs
/// </summary>
public record ScriptMetadata
{
    /// <summary>
    /// When the script was generated
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Name of the provider used (OpenAI, Ollama, Gemini, RuleBased, etc.)
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Specific model used within the provider (e.g., gpt-4, llama2, etc.)
    /// </summary>
    public string ModelUsed { get; init; } = string.Empty;

    /// <summary>
    /// Number of tokens consumed during generation
    /// </summary>
    public int TokensUsed { get; init; }

    /// <summary>
    /// Estimated cost in USD for this generation
    /// </summary>
    public decimal EstimatedCost { get; init; }

    /// <summary>
    /// Provider tier used for this generation
    /// </summary>
    public ProviderTier Tier { get; init; }

    /// <summary>
    /// How long the generation took
    /// </summary>
    public TimeSpan GenerationTime { get; init; }
}

/// <summary>
/// Provider tier classification
/// </summary>
public enum ProviderTier
{
    /// <summary>
    /// Free tier providers (RuleBased, Ollama)
    /// </summary>
    Free,

    /// <summary>
    /// Pro tier providers (OpenAI, Anthropic, Gemini)
    /// </summary>
    Pro,

    /// <summary>
    /// Local/offline providers
    /// </summary>
    Local
}
