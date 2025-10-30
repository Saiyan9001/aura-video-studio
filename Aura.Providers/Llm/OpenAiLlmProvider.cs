using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.AI;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Llm;

/// <summary>
/// LLM provider that uses OpenAI API for script generation (Pro feature).
/// Supports optional ML-driven enhancements via callbacks.
/// </summary>
public class OpenAiLlmProvider : ILlmProvider
{
    private readonly ILogger<OpenAiLlmProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Optional callback to enhance prompts before generation
    /// </summary>
    public Func<string, Brief, PlanSpec, Task<string>>? PromptEnhancementCallback { get; set; }

    /// <summary>
    /// Optional callback to track generation performance
    /// </summary>
    public Action<double, TimeSpan, bool>? PerformanceTrackingCallback { get; set; }

    public OpenAiLlmProvider(
        ILogger<OpenAiLlmProvider> logger,
        HttpClient httpClient,
        string apiKey,
        string model = "gpt-4o-mini",
        int maxRetries = 2,
        int timeoutSeconds = 120)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _maxRetries = maxRetries;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);

        ValidateApiKey();
    }

    /// <summary>
    /// Validate the API key format and presence
    /// </summary>
    private void ValidateApiKey()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new ArgumentException(
                "OpenAI API key is not configured. Please add your API key in Settings → Providers → OpenAI",
                nameof(_apiKey));
        }

        // OpenAI API keys should start with "sk-" and be reasonably long
        if (!_apiKey.StartsWith("sk-", StringComparison.Ordinal) || _apiKey.Length < 40)
        {
            throw new ArgumentException(
                "OpenAI API key format appears invalid. Please check your API key in Settings → Providers → OpenAI",
                nameof(_apiKey));
        }
    }

    public async Task<string> DraftScriptAsync(Brief brief, PlanSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("Generating high-quality script with OpenAI (model: {Model}) for topic: {Topic}", _model, brief.Topic);

        var startTime = DateTime.UtcNow;
        Exception? lastException = null;
        
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} after {Delay}s delay", 
                        attempt, _maxRetries, backoffDelay.TotalSeconds);
                    await Task.Delay(backoffDelay, ct);
                }

                // Build enhanced prompts for quality content
                string systemPrompt = EnhancedPromptTemplates.GetSystemPromptForScriptGeneration();
                string userPrompt = EnhancedPromptTemplates.BuildScriptGenerationPrompt(brief, spec);

                // Apply enhancement callback if configured
                if (PromptEnhancementCallback != null)
                {
                    userPrompt = await PromptEnhancementCallback(userPrompt, brief, spec);
                }

                // Call OpenAI API
                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.7,
                    max_tokens = 2048
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_timeout);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, cts.Token);
                
                // Handle specific HTTP error codes
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new InvalidOperationException(
                            "OpenAI API key is invalid or has been revoked. Please check your API key in Settings → Providers → OpenAI");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("OpenAI rate limit exceeded (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                        if (attempt >= _maxRetries)
                        {
                            throw new InvalidOperationException(
                                "OpenAI rate limit exceeded. Please wait a moment and try again, or upgrade your OpenAI plan.");
                        }
                        lastException = new Exception($"Rate limit exceeded: {errorContent}");
                        continue; // Retry
                    }
                    else if ((int)response.StatusCode >= 500)
                    {
                        _logger.LogWarning("OpenAI server error (attempt {Attempt}/{MaxRetries}): {StatusCode}", 
                            attempt + 1, _maxRetries + 1, response.StatusCode);
                        if (attempt >= _maxRetries)
                        {
                            throw new InvalidOperationException(
                                $"OpenAI service is experiencing issues (HTTP {response.StatusCode}). Please try again later.");
                        }
                        lastException = new Exception($"Server error: {errorContent}");
                        continue; // Retry
                    }
                    
                    response.EnsureSuccessStatusCode();
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var responseDoc = JsonDocument.Parse(responseJson);

                if (responseDoc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var contentProp))
                    {
                        string script = contentProp.GetString() ?? string.Empty;
                        
                        if (string.IsNullOrWhiteSpace(script))
                        {
                            throw new InvalidOperationException("OpenAI returned an empty response");
                        }
                        
                        var duration = DateTime.UtcNow - startTime;
                        
                        _logger.LogInformation("Script generated successfully ({Length} characters) in {Duration}s", 
                            script.Length, duration.TotalSeconds);
                        
                        // Track performance if callback configured
                        PerformanceTrackingCallback?.Invoke(80.0, duration, true);
                        
                        return script;
                    }
                }

                _logger.LogWarning("OpenAI response did not contain expected structure");
                var failureDuration = DateTime.UtcNow - startTime;
                PerformanceTrackingCallback?.Invoke(0, failureDuration, false);
                throw new InvalidOperationException("Invalid response structure from OpenAI API");
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "OpenAI request timed out (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    var duration = DateTime.UtcNow - startTime;
                    PerformanceTrackingCallback?.Invoke(0, duration, false);
                    throw new InvalidOperationException(
                        $"OpenAI request timed out after {_timeout.TotalSeconds}s. Please check your internet connection or try again later.", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to connect to OpenAI API (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    var duration = DateTime.UtcNow - startTime;
                    PerformanceTrackingCallback?.Invoke(0, duration, false);
                    throw new InvalidOperationException(
                        "Cannot connect to OpenAI API. Please check your internet connection and try again.", ex);
                }
            }
            catch (InvalidOperationException)
            {
                // Don't retry on validation errors or known issues
                var duration = DateTime.UtcNow - startTime;
                PerformanceTrackingCallback?.Invoke(0, duration, false);
                throw;
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Error generating script with OpenAI (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                PerformanceTrackingCallback?.Invoke(0, duration, false);
                _logger.LogError(ex, "Error generating script with OpenAI after all retries");
                throw;
            }
        }

        // Should not reach here, but just in case
        var finalDuration = DateTime.UtcNow - startTime;
        PerformanceTrackingCallback?.Invoke(0, finalDuration, false);
        throw new InvalidOperationException(
            $"Failed to generate script with OpenAI after {_maxRetries + 1} attempts. Please try again later.", lastException);
    }

    public async Task<SceneAnalysisResult?> AnalyzeSceneImportanceAsync(
        string sceneText,
        string? previousSceneText,
        string videoGoal,
        CancellationToken ct)
    {
        _logger.LogInformation("Analyzing scene importance with OpenAI");

        try
        {
            // Use centralized scene analysis prompt templates
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();
            var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(sceneText, previousSceneText, videoGoal);

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 512,
                response_format = new { type = "json_object" }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Shorter timeout for analysis

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, cts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var responseDoc = JsonDocument.Parse(responseJson);

            if (responseDoc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentProp))
                {
                    var analysisText = contentProp.GetString() ?? string.Empty;
                    
                    try
                    {
                        var analysisDoc = JsonDocument.Parse(analysisText);
                        var root = analysisDoc.RootElement;

                        var result = new SceneAnalysisResult(
                            Importance: root.TryGetProperty("importance", out var imp) ? imp.GetDouble() : 50.0,
                            Complexity: root.TryGetProperty("complexity", out var comp) ? comp.GetDouble() : 50.0,
                            EmotionalIntensity: root.TryGetProperty("emotionalIntensity", out var emo) ? emo.GetDouble() : 50.0,
                            InformationDensity: root.TryGetProperty("informationDensity", out var info) ? info.GetString() ?? "medium" : "medium",
                            OptimalDurationSeconds: root.TryGetProperty("optimalDurationSeconds", out var dur) ? dur.GetDouble() : 10.0,
                            TransitionType: root.TryGetProperty("transitionType", out var trans) ? trans.GetString() ?? "cut" : "cut",
                            Reasoning: root.TryGetProperty("reasoning", out var reas) ? reas.GetString() ?? "" : ""
                        );

                        _logger.LogInformation("Scene analysis complete. Importance: {Importance}, Complexity: {Complexity}", 
                            result.Importance, result.Complexity);
                        
                        return result;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse scene analysis JSON from OpenAI: {Response}", analysisText);
                        return null;
                    }
                }
            }

            _logger.LogWarning("OpenAI scene analysis response did not contain expected structure");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "OpenAI scene analysis request timed out");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to OpenAI API for scene analysis");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scene with OpenAI");
            return null;
        }
    }

    // Removed legacy prompt building methods - now using EnhancedPromptTemplates
}
