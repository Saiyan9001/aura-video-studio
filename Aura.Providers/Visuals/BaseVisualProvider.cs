using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Visuals;

/// <summary>
/// Base class for visual generation providers with common functionality
/// </summary>
public abstract class BaseVisualProvider
{
    protected readonly ILogger Logger;

    protected BaseVisualProvider(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Gets the provider name
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Gets whether the provider requires an API key
    /// </summary>
    public abstract bool RequiresApiKey { get; }

    /// <summary>
    /// Generates a single image from a prompt
    /// </summary>
    public abstract Task<string?> GenerateImageAsync(
        string prompt,
        VisualGenerationOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the capabilities of this provider
    /// </summary>
    public abstract VisualProviderCapabilities GetProviderCapabilities();

    /// <summary>
    /// Checks if the provider is available and healthy
    /// </summary>
    public abstract Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Generates multiple images in batch (default implementation calls GenerateImageAsync sequentially)
    /// </summary>
    public virtual async Task<List<string>> BatchGenerateAsync(
        List<string> prompts,
        VisualGenerationOptions options,
        IProgress<BatchGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<string>();
        var totalCount = prompts.Count;

        for (int i = 0; i < prompts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await GenerateImageAsync(prompts[i], options, ct).ConfigureAwait(false);
                if (result != null)
                {
                    results.Add(result);
                }

                progress?.Report(new BatchGenerationProgress
                {
                    CompletedCount = i + 1,
                    TotalCount = totalCount,
                    CurrentPrompt = prompts[i],
                    SuccessCount = results.Count
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to generate image {Index}/{Total} for prompt: {Prompt}",
                    i + 1, totalCount, prompts[i]);
            }
        }

        return results;
    }

    /// <summary>
    /// Adapts a generic prompt to this provider's specific requirements
    /// </summary>
    public virtual string AdaptPrompt(string prompt, VisualGenerationOptions options)
    {
        return prompt;
    }

    /// <summary>
    /// Gets the cost estimate for generating an image (in USD)
    /// </summary>
    public virtual decimal GetCostEstimate(VisualGenerationOptions options)
    {
        return 0m;
    }
}

/// <summary>
/// Options for visual generation
/// </summary>
public class VisualGenerationOptions
{
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public string Style { get; set; } = "photorealistic";
    public string AspectRatio { get; set; } = "16:9";
    public int Quality { get; set; } = 80;
    public string[]? NegativePrompts { get; set; }
    public Dictionary<string, object>? ProviderSpecificOptions { get; set; }
}

/// <summary>
/// Capabilities of a visual provider
/// </summary>
public class VisualProviderCapabilities
{
    public string ProviderName { get; set; } = string.Empty;
    public bool SupportsNegativePrompts { get; set; }
    public bool SupportsBatchGeneration { get; set; }
    public bool SupportsStylePresets { get; set; }
    public List<string> SupportedAspectRatios { get; set; } = new();
    public List<string> SupportedStyles { get; set; } = new();
    public int MaxWidth { get; set; } = 1024;
    public int MaxHeight { get; set; } = 1024;
    public bool IsLocal { get; set; }
    public bool IsFree { get; set; }
    public decimal CostPerImage { get; set; }
    public string Tier { get; set; } = "Free";
}

/// <summary>
/// Progress information for batch generation
/// </summary>
public class BatchGenerationProgress
{
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public string CurrentPrompt { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public double ProgressPercentage => TotalCount > 0 ? (CompletedCount * 100.0 / TotalCount) : 0;
}
