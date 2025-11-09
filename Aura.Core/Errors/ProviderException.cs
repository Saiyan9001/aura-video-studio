using System;
using System.Collections.Generic;

namespace Aura.Core.Errors;

/// <summary>
/// Types of providers in the system
/// </summary>
public enum ProviderType
{
    LLM,
    TTS,
    Visual,
    Rendering
}

/// <summary>
/// Common error codes for provider failures
/// </summary>
public static class ProviderErrorCode
{
    public const string RateLimit = "RATE_LIMIT";
    public const string AuthFailed = "AUTH_FAILED";
    public const string Timeout = "TIMEOUT";
    public const string NetworkError = "NETWORK_ERROR";
    public const string InvalidInput = "INVALID_INPUT";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string QuotaExceeded = "QUOTA_EXCEEDED";
    public const string Unknown = "UNKNOWN";
}

/// <summary>
/// Exception thrown when a provider (LLM, TTS, Image, etc.) encounters an error
/// </summary>
public class ProviderException : AuraException
{
    /// <summary>
    /// Name of the provider that encountered the error
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Type of provider (LLM, TTS, Visual, Rendering)
    /// </summary>
    public ProviderType Type { get; }

    /// <summary>
    /// Specific error code for categorizing the failure
    /// </summary>
    public string SpecificErrorCode { get; }

    /// <summary>
    /// HTTP status code if the error occurred during an API call
    /// </summary>
    public int? HttpStatusCode { get; }

    public ProviderException(
        string providerName,
        ProviderType type,
        string message,
        string? specificErrorCode = null,
        string? userMessage = null,
        string? correlationId = null,
        int? httpStatusCode = null,
        bool isTransient = false,
        string[]? suggestedActions = null,
        Exception? innerException = null)
        : base(
            message,
            GenerateErrorCode(type, httpStatusCode),
            userMessage ?? GenerateUserMessage(providerName, type, message),
            correlationId,
            suggestedActions ?? GenerateDefaultSuggestedActions(type, isTransient),
            isTransient,
            innerException)
    {
        ProviderName = providerName;
        Type = type;
        SpecificErrorCode = specificErrorCode ?? ProviderErrorCode.Unknown;
        HttpStatusCode = httpStatusCode;

        // Add provider context
        WithContext("providerName", providerName);
        WithContext("providerType", type.ToString());
        WithContext("specificErrorCode", SpecificErrorCode);
        if (httpStatusCode.HasValue)
        {
            WithContext("httpStatusCode", httpStatusCode.Value);
        }
    }

    private static string GenerateErrorCode(ProviderType providerType, int? httpStatusCode)
    {
        var baseCode = providerType switch
        {
            ProviderType.LLM => "E100",
            ProviderType.TTS => "E200",
            ProviderType.Visual => "E400",
            ProviderType.Rendering => "E500",
            _ => "E900"
        };

        return httpStatusCode.HasValue ? $"{baseCode}-{httpStatusCode}" : baseCode;
    }

    private static string GenerateUserMessage(string providerName, ProviderType providerType, string message)
    {
        return $"{providerType} provider '{providerName}' encountered an error: {message}";
    }

    private static string[] GenerateDefaultSuggestedActions(ProviderType providerType, bool isTransient)
    {
        if (isTransient)
        {
            return new[]
            {
                "Retry the operation",
                "Check your internet connection",
                $"Verify {providerType} provider is responding",
                "Try a different provider if available"
            };
        }

        return new[]
        {
            $"Check {providerType} provider configuration",
            "Verify API keys are valid and not expired",
            "Check provider service status",
            "Try a different provider if available"
        };
    }

    /// <summary>
    /// Creates a ProviderException for missing API key
    /// </summary>
    public static ProviderException MissingApiKey(string providerName, ProviderType providerType, string keyName, string? correlationId = null)
    {
        return new ProviderException(
            providerName,
            providerType,
            $"API key '{keyName}' is required but not configured",
            ProviderErrorCode.AuthFailed,
            $"{providerName} requires an API key to function. Please configure '{keyName}' in Settings → Providers.",
            correlationId,
            suggestedActions: new[]
            {
                $"Add {keyName} API key in Settings → Providers",
                "Obtain an API key from the provider's website",
                "Use a different provider that doesn't require an API key"
            });
    }

    /// <summary>
    /// Creates a ProviderException for API rate limiting
    /// </summary>
    public static ProviderException RateLimited(string providerName, ProviderType providerType, int? retryAfterSeconds = null, string? correlationId = null)
    {
        var message = retryAfterSeconds.HasValue
            ? $"Rate limit exceeded. Retry after {retryAfterSeconds} seconds."
            : "Rate limit exceeded.";

        var suggestedActions = retryAfterSeconds.HasValue
            ? new[] { $"Wait {retryAfterSeconds} seconds and retry", "Use a different provider", "Upgrade your API plan for higher limits" }
            : new[] { "Wait a few minutes and retry", "Use a different provider", "Upgrade your API plan for higher limits" };

        return new ProviderException(
            providerName,
            providerType,
            message,
            ProviderErrorCode.RateLimit,
            $"{providerName} rate limit exceeded. Please wait before retrying.",
            correlationId,
            httpStatusCode: 429,
            isTransient: true,
            suggestedActions: suggestedActions);
    }

    /// <summary>
    /// Creates a ProviderException for network/connection errors
    /// </summary>
    public static ProviderException NetworkError(string providerName, ProviderType providerType, string? correlationId = null, Exception? innerException = null)
    {
        return new ProviderException(
            providerName,
            providerType,
            "Network error communicating with provider",
            ProviderErrorCode.NetworkError,
            $"Unable to connect to {providerName}. Please check your internet connection.",
            correlationId,
            isTransient: true,
            suggestedActions: new[]
            {
                "Check your internet connection",
                "Verify firewall is not blocking the connection",
                "Retry the operation",
                "Use a different provider if available"
            },
            innerException: innerException);
    }

    /// <summary>
    /// Creates a ProviderException for timeout errors
    /// </summary>
    public static ProviderException Timeout(string providerName, ProviderType providerType, int timeoutSeconds, string? correlationId = null)
    {
        return new ProviderException(
            providerName,
            providerType,
            $"Provider request timed out after {timeoutSeconds} seconds",
            ProviderErrorCode.Timeout,
            $"{providerName} took too long to respond. The operation timed out.",
            correlationId,
            isTransient: true,
            suggestedActions: new[]
            {
                "Retry the operation",
                "Try with simpler input or smaller batch size",
                "Use a different provider",
                "Check provider service status"
            });
    }

    public override Dictionary<string, object> ToErrorResponse()
    {
        var response = base.ToErrorResponse();
        response["provider"] = new
        {
            name = ProviderName,
            type = Type.ToString(),
            errorCode = SpecificErrorCode
        };
        return response;
    }
}
