using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Interfaces;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Llm;

/// <summary>
/// Base class for LLM script providers with common retry logic and error handling
/// </summary>
public abstract class BaseLlmScriptProvider : IScriptLlmProvider
{
    protected readonly ILogger _logger;
    protected readonly int _maxRetries;
    protected readonly TimeSpan _baseRetryDelay;

    protected BaseLlmScriptProvider(ILogger logger, int maxRetries = 3, int baseRetryDelayMs = 1000)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _baseRetryDelay = TimeSpan.FromMilliseconds(baseRetryDelayMs);
    }

    /// <summary>
    /// Generate a script - implements retry logic and error handling
    /// </summary>
    public async Task<Script> GenerateScriptAsync(ScriptGenerationRequest request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Attempting script generation with {Provider} (attempt {Attempt}/{MaxRetries}), CorrelationId: {CorrelationId}",
                    GetProviderMetadata().Name, attempt, _maxRetries, request.CorrelationId);

                var script = await GenerateScriptCoreAsync(request, cancellationToken);

                var generationTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Script generation succeeded with {Provider} in {Duration}s, CorrelationId: {CorrelationId}",
                    GetProviderMetadata().Name, generationTime.TotalSeconds, request.CorrelationId);

                var updatedScript = script with 
                { 
                    Metadata = script.Metadata with { GenerationTime = generationTime }
                };

                return updatedScript;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Script generation cancelled, CorrelationId: {CorrelationId}", request.CorrelationId);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "Script generation attempt {Attempt}/{MaxRetries} failed with {Provider}: {Message}, CorrelationId: {CorrelationId}",
                    attempt, _maxRetries, GetProviderMetadata().Name, ex.Message, request.CorrelationId);

                if (attempt < _maxRetries)
                {
                    var delay = CalculateExponentialBackoff(attempt);
                    _logger.LogDebug("Retrying after {Delay}ms", delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        _logger.LogError(lastException,
            "Script generation failed after {MaxRetries} attempts with {Provider}, CorrelationId: {CorrelationId}",
            _maxRetries, GetProviderMetadata().Name, request.CorrelationId);

        throw new InvalidOperationException(
            $"Script generation failed after {_maxRetries} attempts: {lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// Core script generation logic - implemented by derived classes
    /// </summary>
    protected abstract Task<Script> GenerateScriptCoreAsync(ScriptGenerationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Stream-based script generation for real-time updates (optional override)
    /// </summary>
    public virtual IAsyncEnumerable<ScriptGenerationProgress> StreamGenerateAsync(
        ScriptGenerationRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException($"Streaming not supported by {GetProviderMetadata().Name}");
    }

    /// <summary>
    /// Get available models for this provider
    /// </summary>
    public abstract Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validate provider configuration
    /// </summary>
    public abstract Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get provider metadata
    /// </summary>
    public abstract ProviderMetadata GetProviderMetadata();

    /// <summary>
    /// Check if provider is available
    /// </summary>
    public abstract Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Estimate token count for cost calculation (virtual for override)
    /// Uses simple heuristic: ~4 characters per token
    /// </summary>
    public virtual int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / 4.0);
    }

    /// <summary>
    /// Calculate exponential backoff delay
    /// </summary>
    protected TimeSpan CalculateExponentialBackoff(int attempt)
    {
        var multiplier = Math.Pow(2, attempt - 1);
        var delayMs = _baseRetryDelay.TotalMilliseconds * multiplier;
        var jitter = Random.Shared.Next(0, 100);
        return TimeSpan.FromMilliseconds(delayMs + jitter);
    }

    /// <summary>
    /// Parse raw script text into structured scenes
    /// Common utility for text-based providers
    /// </summary>
    protected List<ScriptScene> ParseScriptIntoScenes(string scriptText, PlanSpec planSpec)
    {
        var scenes = new List<ScriptScene>();
        
        var scenePattern = @"(?:Scene\s+(\d+)|^#{1,3}\s*(.+?)$)(.*?)(?=Scene\s+\d+|^#{1,3}\s|$)";
        var matches = Regex.Matches(scriptText, scenePattern, RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (matches.Count == 0)
        {
            var singleSceneDuration = planSpec.TargetDuration;
            scenes.Add(new ScriptScene
            {
                Number = 1,
                Narration = scriptText.Trim(),
                VisualPrompt = GenerateDefaultVisualPrompt(scriptText),
                Duration = singleSceneDuration,
                Transition = TransitionType.Cut
            });
            return scenes;
        }

        var totalDuration = planSpec.TargetDuration;
        var sceneDuration = TimeSpan.FromSeconds(totalDuration.TotalSeconds / matches.Count);

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var sceneNumber = i + 1;
            var content = match.Groups[3].Value.Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var narration = ExtractNarration(content);
            var visualPrompt = ExtractVisualPrompt(content) ?? GenerateDefaultVisualPrompt(narration);
            var transition = DetermineTransition(i, matches.Count, planSpec.Style);

            scenes.Add(new ScriptScene
            {
                Number = sceneNumber,
                Narration = narration,
                VisualPrompt = visualPrompt,
                Duration = sceneDuration,
                Transition = transition
            });
        }

        if (scenes.Count == 0)
        {
            scenes.Add(new ScriptScene
            {
                Number = 1,
                Narration = scriptText.Trim(),
                VisualPrompt = GenerateDefaultVisualPrompt(scriptText),
                Duration = totalDuration,
                Transition = TransitionType.Cut
            });
        }

        return scenes;
    }

    /// <summary>
    /// Extract narration text from scene content
    /// </summary>
    protected string ExtractNarration(string content)
    {
        var narrationMatch = Regex.Match(content, @"Narration:\s*(.+?)(?=Visual:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (narrationMatch.Success)
        {
            return narrationMatch.Groups[1].Value.Trim();
        }

        return content.Trim();
    }

    /// <summary>
    /// Extract visual prompt from scene content
    /// </summary>
    protected string? ExtractVisualPrompt(string content)
    {
        var visualMatch = Regex.Match(content, @"Visual:\s*(.+?)(?=Narration:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (visualMatch.Success)
        {
            return visualMatch.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Generate a default visual prompt from narration
    /// </summary>
    protected string GenerateDefaultVisualPrompt(string narration)
    {
        var firstSentence = narration.Split(new[] { '.', '!', '?' }, 2)[0].Trim();
        return $"Visual representation of: {firstSentence}";
    }

    /// <summary>
    /// Determine appropriate transition based on position and style
    /// </summary>
    protected TransitionType DetermineTransition(int sceneIndex, int totalScenes, string style)
    {
        if (sceneIndex == totalScenes - 1)
        {
            return TransitionType.Fade;
        }

        return style.ToLowerInvariant() switch
        {
            var s when s.Contains("dynamic") || s.Contains("fast") => TransitionType.Cut,
            var s when s.Contains("smooth") || s.Contains("cinematic") => TransitionType.Dissolve,
            _ => TransitionType.Cut
        };
    }
}

/// <summary>
/// Progress update during streaming script generation
/// </summary>
public record ScriptGenerationProgress
{
    public string Stage { get; init; } = string.Empty;
    public int PercentComplete { get; init; }
    public string? PartialScript { get; init; }
    public string Message { get; init; } = string.Empty;
}
