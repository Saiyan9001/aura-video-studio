using System;

namespace Aura.Providers.Validation;

/// <summary>
/// Result of a provider validation check
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Name of the provider
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the validation succeeded
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// Additional details about the validation result
    /// </summary>
    public string Details { get; init; } = string.Empty;

    /// <summary>
    /// Time taken to complete the validation in milliseconds
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// Error code if validation failed (e.g., E307 for offline mode blocking cloud provider)
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult Success(string name, string details, long elapsedMs)
        => new() { Name = name, Ok = true, Details = details, ElapsedMs = elapsedMs };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    public static ValidationResult Failure(string name, string details, long elapsedMs, string? errorCode = null)
        => new() { Name = name, Ok = false, Details = details, ElapsedMs = elapsedMs, ErrorCode = errorCode };
}
