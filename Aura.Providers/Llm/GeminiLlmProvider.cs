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
/// LLM provider that uses Google Gemini API for script generation (Pro feature).
/// </summary>
public class GeminiLlmProvider : ILlmProvider
{
    private readonly ILogger<GeminiLlmProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;

    public GeminiLlmProvider(
        ILogger<GeminiLlmProvider> logger,
        HttpClient httpClient,
        string apiKey,
        string model = "gemini-pro",
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
                "Gemini API key is not configured. Please add your API key in Settings → Providers → Gemini",
                nameof(_apiKey));
        }

        // Gemini API keys should be at least 30 characters
        if (_apiKey.Length < 30)
        {
            throw new ArgumentException(
                "Gemini API key format appears invalid. Please check your API key in Settings → Providers → Gemini",
                nameof(_apiKey));
        }
    }

    public async Task<string> DraftScriptAsync(Brief brief, PlanSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("Generating script with Gemini (model: {Model}) for topic: {Topic}", _model, brief.Topic);

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

                // Build enhanced prompt for quality content
                string systemPrompt = EnhancedPromptTemplates.GetSystemPromptForScriptGeneration();
                string userPrompt = EnhancedPromptTemplates.BuildScriptGenerationPrompt(brief, spec);
                string prompt = $"{systemPrompt}\n\n{userPrompt}";

                // Call Gemini API
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 2048,
                        topP = 0.9
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_timeout);

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content, cts.Token);
                
                // Handle specific HTTP error codes
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        throw new InvalidOperationException(
                            "Gemini API key is invalid or access is forbidden. Please verify your API key in Settings → Providers → Gemini");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Gemini quota exceeded or rate limit hit (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                        if (attempt >= _maxRetries)
                        {
                            throw new InvalidOperationException(
                                "Gemini API quota exceeded or rate limit reached. Please check your quota at https://makersuite.google.com/app/apikey or wait before retrying.");
                        }
                        lastException = new Exception($"Quota/rate limit exceeded: {errorContent}");
                        continue; // Retry
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        // Parse error for more details
                        throw new InvalidOperationException(
                            $"Gemini API request was invalid. This may indicate an issue with the model name '{_model}' or request format. Error: {errorContent}");
                    }
                    else if ((int)response.StatusCode >= 500)
                    {
                        _logger.LogWarning("Gemini server error (attempt {Attempt}/{MaxRetries}): {StatusCode}",
                            attempt + 1, _maxRetries + 1, response.StatusCode);
                        if (attempt >= _maxRetries)
                        {
                            throw new InvalidOperationException(
                                $"Gemini service is experiencing issues (HTTP {response.StatusCode}). Please try again later.");
                        }
                        lastException = new Exception($"Server error: {errorContent}");
                        continue; // Retry
                    }

                    response.EnsureSuccessStatusCode();
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var responseDoc = JsonDocument.Parse(responseJson);

                if (responseDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                        contentObj.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out var textProp))
                        {
                            string script = textProp.GetString() ?? string.Empty;
                            
                            if (string.IsNullOrWhiteSpace(script))
                            {
                                throw new InvalidOperationException("Gemini returned an empty response");
                            }

                            var duration = DateTime.UtcNow - startTime;
                            _logger.LogInformation("Script generated successfully ({Length} characters) in {Duration}s",
                                script.Length, duration.TotalSeconds);
                            return script;
                        }
                    }
                }

                _logger.LogWarning("Gemini response did not contain expected structure");
                throw new InvalidOperationException("Invalid response structure from Gemini API");
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Gemini request timed out (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    throw new InvalidOperationException(
                        $"Gemini request timed out after {_timeout.TotalSeconds}s. Please check your internet connection or try again later.", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to connect to Gemini API (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    throw new InvalidOperationException(
                        "Cannot connect to Gemini API. Please check your internet connection and try again.", ex);
                }
            }
            catch (InvalidOperationException)
            {
                // Don't retry on validation errors
                throw;
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Error generating script with Gemini (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating script with Gemini after all retries");
                throw;
            }
        }

        // Should not reach here, but just in case
        throw new InvalidOperationException(
            $"Failed to generate script with Gemini after {_maxRetries + 1} attempts. Please try again later.", lastException);
    }

    public async Task<SceneAnalysisResult?> AnalyzeSceneImportanceAsync(
        string sceneText,
        string? previousSceneText,
        string videoGoal,
        CancellationToken ct)
    {
        _logger.LogInformation("Analyzing scene importance with Gemini");

        try
        {
            // Use centralized scene analysis prompt templates
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();
            var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(sceneText, previousSceneText, videoGoal);
            
            var prompt = $"{systemPrompt}\n\n{userPrompt}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 512,
                    topP = 0.9
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Shorter timeout for analysis

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content, cts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var responseDoc = JsonDocument.Parse(responseJson);

            if (responseDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textProp))
                    {
                        var analysisText = textProp.GetString() ?? string.Empty;
                        
                        try
                        {
                            // Clean up potential markdown code blocks
                            analysisText = analysisText.Trim();
                            if (analysisText.StartsWith("```json"))
                            {
                                analysisText = analysisText.Substring(7);
                            }
                            if (analysisText.StartsWith("```"))
                            {
                                analysisText = analysisText.Substring(3);
                            }
                            if (analysisText.EndsWith("```"))
                            {
                                analysisText = analysisText.Substring(0, analysisText.Length - 3);
                            }
                            analysisText = analysisText.Trim();

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
                            _logger.LogWarning(ex, "Failed to parse scene analysis JSON from Gemini: {Response}", analysisText);
                            return null;
                        }
                    }
                }
            }

            _logger.LogWarning("Gemini scene analysis response did not contain expected structure");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Gemini scene analysis request timed out");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Gemini API for scene analysis");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scene with Gemini");
            return null;
        }
    }

    // Removed legacy prompt building method - now using EnhancedPromptTemplates
}
