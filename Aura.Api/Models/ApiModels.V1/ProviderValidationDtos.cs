namespace Aura.Api.Models.ApiModels.V1;

/// <summary>
/// Request model for validating OpenAI API key with live network check
/// </summary>
public record ValidateOpenAIKeyRequest(
    string ApiKey,
    string? BaseUrl = null,
    string? OrganizationId = null,
    string? ProjectId = null);

/// <summary>
/// Request model for testing OpenAI script generation
/// </summary>
public record TestOpenAIGenerationRequest(
    string ApiKey,
    string Model = "gpt-4o-mini",
    string? BaseUrl = null,
    string? OrganizationId = null,
    string? ProjectId = null);

/// <summary>
/// Request model for validating ElevenLabs API key
/// </summary>
public record ValidateElevenLabsKeyRequest(string ApiKey);

/// <summary>
/// Request model for validating PlayHT API key
/// </summary>
public record ValidatePlayHTKeyRequest(string ApiKey);

/// <summary>
/// Generic provider API key validation request
/// </summary>
public record ValidateProviderKeyRequest(
    string Provider,
    string ApiKey);

/// <summary>
/// Response model for provider validation with detailed status
/// </summary>
public record ProviderValidationResponse(
    bool IsValid,
    string Status,
    string? Message = null,
    string? CorrelationId = null,
    ValidationDetails? Details = null);

/// <summary>
/// Detailed validation information
/// </summary>
public record ValidationDetails(
    string Provider,
    string KeyFormat,
    bool FormatValid,
    bool? NetworkCheckPassed = null,
    int? HttpStatusCode = null,
    string? ErrorType = null,
    long? ResponseTimeMs = null);

/// <summary>
/// Provider status information
/// </summary>
public record ProviderStatusDto(
    string Name,
    bool IsConfigured,
    bool IsAvailable,
    string Status,
    string? LastValidated = null,
    string? ErrorMessage = null);

/// <summary>
/// Validation status enum
/// </summary>
public enum ValidationStatus
{
    Valid,
    Invalid,
    NetworkError,
    Unauthorized,
    Forbidden,
    Timeout,
    UnknownError
}
