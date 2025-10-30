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
/// LLM provider that uses Azure OpenAI API for script generation (Pro feature).
/// </summary>
public class AzureOpenAiLlmProvider : ILlmProvider
{
    private readonly ILogger<AzureOpenAiLlmProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _deploymentName;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;

    public AzureOpenAiLlmProvider(
        ILogger<AzureOpenAiLlmProvider> logger,
        HttpClient httpClient,
        string apiKey,
        string endpoint,
        string deploymentName = "gpt-4",
        int maxRetries = 2,
        int timeoutSeconds = 120)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = apiKey;
        _endpoint = endpoint;
        _deploymentName = deploymentName;
        _maxRetries = maxRetries;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);

        ValidateConfiguration();
    }

    /// <summary>
    /// Validate API key and endpoint configuration
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new ArgumentException(
                "Azure OpenAI API key is not configured. Please add your API key in Settings → Providers → Azure OpenAI",
                nameof(_apiKey));
        }

        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            throw new ArgumentException(
                "Azure OpenAI endpoint is not configured. Please add your endpoint URL in Settings → Providers → Azure OpenAI",
                nameof(_endpoint));
        }

        // Validate endpoint format
        if (!_endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Azure OpenAI endpoint must use HTTPS. Expected format: https://<resource>.openai.azure.com/",
                nameof(_endpoint));
        }

        if (!_endpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Azure OpenAI endpoint format appears invalid. Expected format: https://<resource>.openai.azure.com/",
                nameof(_endpoint));
        }

        // Azure API keys are typically 32 characters
        if (_apiKey.Length < 32)
        {
            _logger.LogWarning("Azure OpenAI API key appears to be shorter than expected (expected 32+ characters)");
        }
    }

    public async Task<string> DraftScriptAsync(Brief brief, PlanSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("Generating script with Azure OpenAI (deployment: {Deployment}) for topic: {Topic}", _deploymentName, brief.Topic);

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

                // Call Azure OpenAI API
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.7,
                    max_tokens = 2048
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_timeout);

                var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";
                var response = await _httpClient.PostAsync(url, content, cts.Token);
                
                // Handle specific HTTP error codes
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        throw new InvalidOperationException(
                            "Azure OpenAI authentication failed. Please verify your API key and endpoint in Settings → Providers → Azure OpenAI");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new InvalidOperationException(
                            $"Azure OpenAI deployment '{_deploymentName}' not found. Please verify the deployment name in your Azure portal.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Azure OpenAI rate limit exceeded (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                        if (attempt >= _maxRetries)
                        {
                            throw new InvalidOperationException(
                                "Azure OpenAI rate limit exceeded. Please wait a moment and try again, or increase your quota.");
                        }
                        lastException = new Exception($"Rate limit exceeded: {errorContent}");
                        continue; // Retry
                    }
                    else if ((int)response.StatusCode >= 500)
                    {
                        _logger.LogWarning("Azure OpenAI server error (attempt {Attempt}/{MaxRetries}): {StatusCode}",
                            attempt + 1, _maxRetries + 1, response.StatusCode);
                        if (attempt >= _maxRetries)
                        {
                            throw new InvalidOperationException(
                                $"Azure OpenAI service is experiencing issues (HTTP {response.StatusCode}). Please try again later.");
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
                            throw new InvalidOperationException("Azure OpenAI returned an empty response");
                        }

                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogInformation("Script generated successfully ({Length} characters) in {Duration}s",
                            script.Length, duration.TotalSeconds);
                        return script;
                    }
                }

                _logger.LogWarning("Azure OpenAI response did not contain expected structure");
                throw new InvalidOperationException("Invalid response structure from Azure OpenAI API");
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Azure OpenAI request timed out (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    throw new InvalidOperationException(
                        $"Azure OpenAI request timed out after {_timeout.TotalSeconds}s. Please check your internet connection or try again later.", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to connect to Azure OpenAI (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    throw new InvalidOperationException(
                        $"Cannot connect to Azure OpenAI at {_endpoint}. Please verify your endpoint URL and internet connection.", ex);
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
                _logger.LogWarning(ex, "Error generating script with Azure OpenAI (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating script with Azure OpenAI after all retries");
                throw;
            }
        }

        // Should not reach here, but just in case
        throw new InvalidOperationException(
            $"Failed to generate script with Azure OpenAI after {_maxRetries + 1} attempts. Please try again later.", lastException);
    }

    public async Task<SceneAnalysisResult?> AnalyzeSceneImportanceAsync(
        string sceneText,
        string? previousSceneText,
        string videoGoal,
        CancellationToken ct)
    {
        _logger.LogInformation("Analyzing scene importance with Azure OpenAI");

        try
        {
            // Use centralized scene analysis prompt templates
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();
            var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(sceneText, previousSceneText, videoGoal);

            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 512
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Shorter timeout for analysis

            var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";
            var response = await _httpClient.PostAsync(url, content, cts.Token);
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
                        _logger.LogWarning(ex, "Failed to parse scene analysis JSON from Azure OpenAI: {Response}", analysisText);
                        return null;
                    }
                }
            }

            _logger.LogWarning("Azure OpenAI scene analysis response did not contain expected structure");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI scene analysis request timed out");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Azure OpenAI API for scene analysis");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scene with Azure OpenAI");
            return null;
        }
    }

    // Removed legacy prompt building methods - now using EnhancedPromptTemplates
}
