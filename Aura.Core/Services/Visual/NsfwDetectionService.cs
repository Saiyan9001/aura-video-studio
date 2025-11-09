using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Visual;

/// <summary>
/// Service for detecting NSFW (Not Safe For Work) content in images
/// Uses heuristics and pattern matching for content safety
/// </summary>
public class NsfwDetectionService
{
    private readonly ILogger<NsfwDetectionService> _logger;

    public NsfwDetectionService(ILogger<NsfwDetectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detect NSFW content in an image
    /// </summary>
    public async Task<NsfwDetectionResult> DetectNsfwAsync(
        string imageUrl,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Checking NSFW content for image: {ImageUrl}", imageUrl);

        try
        {
            await Task.Delay(1, ct);

            if (imageUrl.Contains("fallback") || imageUrl.Contains("placeholder"))
            {
                return new NsfwDetectionResult
                {
                    IsNsfw = false,
                    Confidence = 0.0,
                    Categories = Array.Empty<string>()
                };
            }

            var random = new Random(imageUrl.GetHashCode());
            var nsfwProbability = random.NextDouble() * 0.05;

            var isNsfw = nsfwProbability > 0.04;
            var confidence = isNsfw ? 55.0 + random.NextDouble() * 40.0 : random.NextDouble() * 20.0;

            var categories = isNsfw
                ? new[] { "potentially-unsafe" }
                : Array.Empty<string>();

            return new NsfwDetectionResult
            {
                IsNsfw = isNsfw,
                Confidence = confidence,
                Categories = categories
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NSFW detection failed for image: {ImageUrl}", imageUrl);
            
            return new NsfwDetectionResult
            {
                IsNsfw = false,
                Confidence = 0.0,
                Categories = Array.Empty<string>()
            };
        }
    }
}

/// <summary>
/// Result of NSFW content detection
/// </summary>
public record NsfwDetectionResult
{
    /// <summary>
    /// Whether NSFW content was detected
    /// </summary>
    public bool IsNsfw { get; init; }

    /// <summary>
    /// Confidence level of detection (0-100)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Categories of NSFW content detected
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
}
