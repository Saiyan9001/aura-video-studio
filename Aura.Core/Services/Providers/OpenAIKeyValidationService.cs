using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Providers;

/// <summary>
/// Service for validating OpenAI API keys with live network verification
/// </summary>
public class OpenAIKeyValidationService
{
    private readonly ILogger<OpenAIKeyValidationService> _logger;
    private readonly HttpClient _httpClient;
    
    private const int TotalTimeoutSeconds = 10;
    private const string DefaultBaseUrl = "https://api.openai.com";

    public OpenAIKeyValidationService(
        ILogger<OpenAIKeyValidationService> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Validate OpenAI API key format (preliminary check)
    /// </summary>
    public (bool IsValid, string Message) ValidateKeyFormat(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "API key is required");
        }

        // OpenAI keys can start with:
        // - "sk-" for regular keys
        // - "sk-proj-" for project-scoped keys
        // - "sk-live-" for live environment keys
        // Must be at least 20 characters long
        var hasValidPrefix = apiKey.StartsWith("sk-", StringComparison.Ordinal) ||
                            apiKey.StartsWith("sk-proj-", StringComparison.Ordinal) ||
                            apiKey.StartsWith("sk-live-", StringComparison.Ordinal);
        
        if (!hasValidPrefix || apiKey.Length < 20)
        {
            return (false, "Invalid OpenAI API key format. Must start with 'sk-', 'sk-proj-', or 'sk-live-' and be at least 20 characters.");
        }

        return (true, "Format looks correct; verifying with OpenAI…");
    }

    /// <summary>
    /// Validate OpenAI API key with live network verification
    /// Uses GET /v1/models endpoint for low-cost validation without token usage
    /// </summary>
    public async Task<OpenAIValidationResult> ValidateKeyAsync(
        string apiKey,
        string? baseUrl = null,
        string? organizationId = null,
        string? projectId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var maskedKey = MaskApiKey(apiKey);
        
        _logger.LogInformation(
            "Validating OpenAI API key (masked: {MaskedKey}), CorrelationId: {CorrelationId}",
            maskedKey,
            correlationId ?? "none");

        // First check format
        var (formatValid, formatMessage) = ValidateKeyFormat(apiKey);
        if (!formatValid)
        {
            return new OpenAIValidationResult
            {
                IsValid = false,
                Status = "Invalid",
                Message = formatMessage,
                FormatValid = false,
                NetworkCheckPassed = false,
                ResponseTimeMs = 0
            };
        }

        // Perform live validation
        try
        {
            var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
            var requestUri = $"{effectiveBaseUrl.TrimEnd('/')}/v1/models";

            _logger.LogDebug(
                "Sending OpenAI validation request to {RequestUri} with key {MaskedKey}",
                requestUri,
                maskedKey);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Add optional headers for sk-proj keys
            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                request.Headers.Add("OpenAI-Organization", organizationId);
                _logger.LogDebug("Added OpenAI-Organization header: {OrgId}", organizationId);
            }
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                request.Headers.Add("OpenAI-Project", projectId);
                _logger.LogDebug("Added OpenAI-Project header: {ProjectId}", projectId);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TotalTimeoutSeconds));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cts.Token);
            }
            catch (TaskCanceledException)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("OpenAI validation cancelled by user, Key: {MaskedKey}", maskedKey);
                    return new OpenAIValidationResult
                    {
                        IsValid = false,
                        Status = "Cancelled",
                        Message = "Validation cancelled",
                        FormatValid = true,
                        NetworkCheckPassed = false,
                        ResponseTimeMs = (long)elapsed
                    };
                }

                _logger.LogWarning(
                    "OpenAI validation timeout after {Timeout}s, Key: {MaskedKey}",
                    TotalTimeoutSeconds,
                    maskedKey);
                
                return new OpenAIValidationResult
                {
                    IsValid = false,
                    Status = "Timeout",
                    Message = $"Request timed out after {TotalTimeoutSeconds} seconds. Please check your internet connection.",
                    FormatValid = true,
                    NetworkCheckPassed = false,
                    HttpStatusCode = null,
                    ErrorType = "Timeout",
                    ResponseTimeMs = (long)elapsed
                };
            }
            catch (HttpRequestException ex)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogWarning(
                    ex,
                    "Network error during OpenAI validation, Key: {MaskedKey}",
                    maskedKey);
                
                return new OpenAIValidationResult
                {
                    IsValid = false,
                    Status = "NetworkError",
                    Message = "Network error while contacting OpenAI. Please check your internet connection and try again.",
                    FormatValid = true,
                    NetworkCheckPassed = false,
                    HttpStatusCode = null,
                    ErrorType = "NetworkError",
                    ResponseTimeMs = (long)elapsed
                };
            }

            var elapsed2 = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var statusCode = (int)response.StatusCode;

            // Success case
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "OpenAI API key validated successfully in {ElapsedMs}ms, Key: {MaskedKey}",
                    elapsed2,
                    maskedKey);
                
                return new OpenAIValidationResult
                {
                    IsValid = true,
                    Status = "Valid",
                    Message = "API key is valid and verified with OpenAI.",
                    FormatValid = true,
                    NetworkCheckPassed = true,
                    HttpStatusCode = statusCode,
                    ResponseTimeMs = (long)elapsed2
                };
            }

            // Read error response for detailed messages
            string errorBody = string.Empty;
            string errorMessage = string.Empty;
            
            try
            {
                errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogDebug(
                    "OpenAI API response body: {ResponseBody}, Status: {StatusCode}, Key: {MaskedKey}",
                    errorBody,
                    response.StatusCode,
                    maskedKey);
                
                using var jsonDoc = JsonDocument.Parse(errorBody);
                if (jsonDoc.RootElement.TryGetProperty("error", out var errorObj))
                {
                    if (errorObj.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse error response from OpenAI, Raw body: {ErrorBody}", errorBody);
            }

            // Handle specific error codes
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var message = !string.IsNullOrEmpty(errorMessage)
                    ? errorMessage
                    : "Invalid API key. Please check the value and try again.";

                _logger.LogWarning(
                    "OpenAI API key validation failed: Unauthorized, Key: {MaskedKey}, Error: {ErrorMessage}",
                    maskedKey,
                    errorMessage);

                return new OpenAIValidationResult
                {
                    IsValid = false,
                    Status = "Invalid",
                    Message = message,
                    FormatValid = true,
                    NetworkCheckPassed = true,
                    HttpStatusCode = statusCode,
                    ErrorType = "Unauthorized",
                    ResponseTimeMs = (long)elapsed2
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var message = !string.IsNullOrEmpty(errorMessage)
                    ? errorMessage
                    : "Access denied. Check organization/project permissions or billing.";

                _logger.LogWarning(
                    "OpenAI API key validation failed: Forbidden, Key: {MaskedKey}, Error: {ErrorMessage}",
                    maskedKey,
                    errorMessage);

                return new OpenAIValidationResult
                {
                    IsValid = false,
                    Status = "PermissionDenied",
                    Message = message,
                    FormatValid = true,
                    NetworkCheckPassed = true,
                    HttpStatusCode = statusCode,
                    ErrorType = "Forbidden",
                    ResponseTimeMs = (long)elapsed2
                };
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var message = !string.IsNullOrEmpty(errorMessage)
                    ? errorMessage
                    : "Rate limited. Your key is valid, but you've hit a limit. Try again later.";

                _logger.LogInformation(
                    "OpenAI API key validation rate limited (key valid): Key: {MaskedKey}",
                    maskedKey);

                return new OpenAIValidationResult
                {
                    IsValid = true,
                    Status = "RateLimited",
                    Message = message,
                    FormatValid = true,
                    NetworkCheckPassed = true,
                    HttpStatusCode = statusCode,
                    ErrorType = "RateLimited",
                    ResponseTimeMs = (long)elapsed2
                };
            }

            if ((int)response.StatusCode >= 500)
            {
                var message = !string.IsNullOrEmpty(errorMessage)
                    ? $"OpenAI service issue: {errorMessage}"
                    : "OpenAI service issue. Your key may be valid; please retry shortly.";

                _logger.LogWarning(
                    "OpenAI service error during validation: {StatusCode}, Key: {MaskedKey}",
                    response.StatusCode,
                    maskedKey);

                return new OpenAIValidationResult
                {
                    IsValid = false,
                    Status = "ServiceIssue",
                    Message = message,
                    FormatValid = true,
                    NetworkCheckPassed = true,
                    HttpStatusCode = statusCode,
                    ErrorType = "ServiceError",
                    ResponseTimeMs = (long)elapsed2
                };
            }

            // Other HTTP errors
            _logger.LogWarning(
                "OpenAI API returned unexpected status: {StatusCode}, Key: {MaskedKey}",
                response.StatusCode,
                maskedKey);

            return new OpenAIValidationResult
            {
                IsValid = false,
                Status = "Error",
                Message = $"OpenAI API returned error: {response.StatusCode}. {errorMessage}",
                FormatValid = true,
                NetworkCheckPassed = true,
                HttpStatusCode = statusCode,
                ErrorType = response.StatusCode.ToString(),
                ResponseTimeMs = (long)elapsed2
            };
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            _logger.LogError(
                ex,
                "Unexpected error during OpenAI key validation, Key: {MaskedKey}",
                maskedKey);

            return new OpenAIValidationResult
            {
                IsValid = false,
                Status = "Error",
                Message = "An unexpected error occurred while validating the API key.",
                FormatValid = true,
                NetworkCheckPassed = false,
                ErrorType = "UnexpectedError",
                ResponseTimeMs = (long)elapsed
            };
        }
    }

    /// <summary>
    /// Get available models for the validated API key
    /// </summary>
    public async Task<OpenAIModelsResult> GetAvailableModelsAsync(
        string apiKey,
        string? baseUrl = null,
        string? organizationId = null,
        string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var maskedKey = MaskApiKey(apiKey);
        
        _logger.LogInformation(
            "Fetching available models for OpenAI API key (masked: {MaskedKey})",
            maskedKey);

        try
        {
            var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
            var requestUri = $"{effectiveBaseUrl.TrimEnd('/')}/v1/models";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                request.Headers.Add("OpenAI-Organization", organizationId);
            }
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                request.Headers.Add("OpenAI-Project", projectId);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TotalTimeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch models: HTTP {StatusCode}, Key: {MaskedKey}",
                    response.StatusCode,
                    maskedKey);
                
                return new OpenAIModelsResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to fetch models: HTTP {response.StatusCode}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            using var jsonDoc = JsonDocument.Parse(responseBody);
            var models = new List<string>();
            
            if (jsonDoc.RootElement.TryGetProperty("data", out var dataArray))
            {
                foreach (var model in dataArray.EnumerateArray())
                {
                    if (model.TryGetProperty("id", out var idProp))
                    {
                        var modelId = idProp.GetString();
                        if (!string.IsNullOrEmpty(modelId))
                        {
                            models.Add(modelId);
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Fetched {Count} models successfully for key {MaskedKey}",
                models.Count,
                maskedKey);

            return new OpenAIModelsResult
            {
                Success = true,
                Models = models
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models for key {MaskedKey}", maskedKey);
            return new OpenAIModelsResult
            {
                Success = false,
                ErrorMessage = $"Error fetching models: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Test script generation with a simple prompt to verify the API key works for completions
    /// </summary>
    public async Task<OpenAITestGenerationResult> TestScriptGenerationAsync(
        string apiKey,
        string model = "gpt-4o-mini",
        string? baseUrl = null,
        string? organizationId = null,
        string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var maskedKey = MaskApiKey(apiKey);
        
        _logger.LogInformation(
            "Testing script generation with model {Model} for key {MaskedKey}",
            model,
            maskedKey);

        var startTime = DateTime.UtcNow;

        try
        {
            var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
            var requestUri = $"{effectiveBaseUrl.TrimEnd('/')}/v1/chat/completions";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = "Say hello" }
                },
                max_tokens = 10
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            if (!string.IsNullOrWhiteSpace(organizationId))
            {
                request.Headers.Add("OpenAI-Organization", organizationId);
            }
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                request.Headers.Add("OpenAI-Project", projectId);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TotalTimeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogWarning(
                    "Script generation test failed: HTTP {StatusCode}, Key: {MaskedKey}",
                    response.StatusCode,
                    maskedKey);

                return new OpenAITestGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Generation test failed: HTTP {response.StatusCode}",
                    ResponseTimeMs = (long)elapsed
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            using var jsonDoc = JsonDocument.Parse(responseBody);
            string? generatedText = null;
            
            if (jsonDoc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentProp))
                {
                    generatedText = contentProp.GetString();
                }
            }

            _logger.LogInformation(
                "Script generation test successful in {ElapsedMs}ms, Key: {MaskedKey}",
                elapsed,
                maskedKey);

            return new OpenAITestGenerationResult
            {
                Success = true,
                GeneratedText = generatedText ?? string.Empty,
                Model = model,
                ResponseTimeMs = (long)elapsed
            };
        }
        catch (TaskCanceledException ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning(ex, "Script generation test timed out for key {MaskedKey}", maskedKey);
            
            return new OpenAITestGenerationResult
            {
                Success = false,
                ErrorMessage = "Request timed out",
                ResponseTimeMs = (long)elapsed
            };
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error testing script generation for key {MaskedKey}", maskedKey);
            
            return new OpenAITestGenerationResult
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}",
                ResponseTimeMs = (long)elapsed
            };
        }
    }

    /// <summary>
    /// Mask API key for logging (show first 6 and last 4 characters)
    /// </summary>
    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "[empty]";
        }

        if (apiKey.Length <= 10)
        {
            return new string('*', apiKey.Length);
        }

        var prefix = apiKey.Substring(0, Math.Min(6, apiKey.Length));
        var suffix = apiKey.Length > 4 ? apiKey.Substring(apiKey.Length - 4) : string.Empty;
        var maskedMiddle = new string('*', Math.Max(0, apiKey.Length - 10));

        return $"{prefix}{maskedMiddle}{suffix}";
    }
}

/// <summary>
/// Result of OpenAI API key validation
/// </summary>
public class OpenAIValidationResult
{
    public bool IsValid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool FormatValid { get; set; }
    public bool NetworkCheckPassed { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorType { get; set; }
    public long ResponseTimeMs { get; set; }
}

/// <summary>
/// Result of fetching available models
/// </summary>
public class OpenAIModelsResult
{
    public bool Success { get; set; }
    public List<string> Models { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of testing script generation
/// </summary>
public class OpenAITestGenerationResult
{
    public bool Success { get; set; }
    public string? GeneratedText { get; set; }
    public string? Model { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}
