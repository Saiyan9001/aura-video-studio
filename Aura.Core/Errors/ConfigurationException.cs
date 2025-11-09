using System;
using System.Collections.Generic;

namespace Aura.Core.Errors;

/// <summary>
/// Exception thrown when application configuration is invalid or missing
/// </summary>
public class ConfigurationException : AuraException
{
    /// <summary>
    /// The configuration section or key that is invalid
    /// </summary>
    public string ConfigurationKey { get; }

    /// <summary>
    /// The expected value or format
    /// </summary>
    public string? ExpectedFormat { get; }

    /// <summary>
    /// The actual invalid value (may be null or empty)
    /// </summary>
    public string? ActualValue { get; }

    public ConfigurationException(
        string configurationKey,
        string message,
        string? expectedFormat = null,
        string? actualValue = null,
        string? userMessage = null,
        string? correlationId = null,
        string[]? suggestedActions = null,
        Exception? innerException = null)
        : base(
            message,
            "E003",
            userMessage ?? GenerateUserMessage(configurationKey, message),
            correlationId,
            suggestedActions ?? GenerateDefaultSuggestedActions(configurationKey),
            isTransient: false,
            innerException)
    {
        ConfigurationKey = configurationKey;
        ExpectedFormat = expectedFormat;
        ActualValue = actualValue;

        WithContext("configurationKey", configurationKey);
        if (!string.IsNullOrEmpty(expectedFormat))
        {
            WithContext("expectedFormat", expectedFormat);
        }
        if (!string.IsNullOrEmpty(actualValue))
        {
            WithContext("actualValue", actualValue);
        }
    }

    private static string GenerateUserMessage(string configurationKey, string message)
    {
        return $"Configuration error for '{configurationKey}': {message}";
    }

    private static string[] GenerateDefaultSuggestedActions(string configurationKey)
    {
        return new[]
        {
            $"Check configuration for '{configurationKey}' in settings",
            "Verify configuration file format and syntax",
            "Review application documentation for configuration requirements",
            "Reset to default settings if available"
        };
    }

    /// <summary>
    /// Creates a ConfigurationException for missing required configuration
    /// </summary>
    public static ConfigurationException MissingRequired(string configurationKey, string? correlationId = null)
    {
        return new ConfigurationException(
            configurationKey,
            $"Required configuration '{configurationKey}' is missing or empty",
            userMessage: $"Missing required configuration: {configurationKey}",
            correlationId: correlationId,
            suggestedActions: new[]
            {
                $"Add '{configurationKey}' to your configuration",
                "Check application documentation for required settings",
                "Verify configuration file is not corrupted"
            });
    }

    /// <summary>
    /// Creates a ConfigurationException for invalid format
    /// </summary>
    public static ConfigurationException InvalidFormat(
        string configurationKey,
        string expectedFormat,
        string? actualValue = null,
        string? correlationId = null)
    {
        var message = $"Configuration '{configurationKey}' has invalid format. Expected: {expectedFormat}";
        if (!string.IsNullOrEmpty(actualValue))
        {
            message += $", Actual: {actualValue}";
        }

        return new ConfigurationException(
            configurationKey,
            message,
            expectedFormat,
            actualValue,
            userMessage: $"Invalid configuration format for '{configurationKey}'",
            correlationId: correlationId,
            suggestedActions: new[]
            {
                $"Update '{configurationKey}' to match format: {expectedFormat}",
                "Check configuration documentation",
                "Verify no extra spaces or special characters"
            });
    }

    /// <summary>
    /// Creates a ConfigurationException for invalid API key
    /// </summary>
    public static ConfigurationException InvalidApiKey(
        string providerName,
        string keyName,
        string expectedFormat,
        string? correlationId = null)
    {
        return new ConfigurationException(
            keyName,
            $"API key for {providerName} appears invalid. Expected format: {expectedFormat}",
            expectedFormat,
            userMessage: $"Invalid API key for {providerName}",
            correlationId: correlationId,
            suggestedActions: new[]
            {
                $"Obtain a valid API key from {providerName}",
                "Verify you copied the entire key without truncation",
                "Check that the key hasn't expired",
                "Remove any extra spaces or quotes"
            });
    }

    /// <summary>
    /// Creates a ConfigurationException for invalid file path
    /// </summary>
    public static ConfigurationException InvalidPath(
        string configurationKey,
        string path,
        string reason,
        string? correlationId = null)
    {
        return new ConfigurationException(
            configurationKey,
            $"Invalid path configured for '{configurationKey}': {reason}",
            "Valid file or directory path",
            path,
            userMessage: $"Invalid path for {configurationKey}",
            correlationId: correlationId,
            suggestedActions: new[]
            {
                "Verify the path exists and is accessible",
                "Check file/directory permissions",
                "Use an absolute path instead of relative",
                "Ensure the path doesn't contain invalid characters"
            });
    }

    public override Dictionary<string, object> ToErrorResponse()
    {
        var response = base.ToErrorResponse();
        response["configuration"] = new
        {
            key = ConfigurationKey,
            expectedFormat = ExpectedFormat,
            actualValue = ActualValue
        };
        return response;
    }
}
