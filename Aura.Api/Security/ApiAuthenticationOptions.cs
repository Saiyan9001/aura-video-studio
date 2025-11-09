using System;

namespace Aura.Api.Security;

/// <summary>
/// Configuration for API authentication
/// </summary>
public class ApiAuthenticationOptions
{
    /// <summary>
    /// Enable JWT bearer authentication
    /// </summary>
    public bool EnableJwtAuthentication { get; set; } = false;

    /// <summary>
    /// Enable API key authentication
    /// </summary>
    public bool EnableApiKeyAuthentication { get; set; } = true;

    /// <summary>
    /// JWT secret key for token signing (should be set via environment variable)
    /// </summary>
    public string? JwtSecretKey { get; set; }

    /// <summary>
    /// JWT issuer
    /// </summary>
    public string JwtIssuer { get; set; } = "AuraVideoStudio";

    /// <summary>
    /// JWT audience
    /// </summary>
    public string JwtAudience { get; set; } = "AuraApi";

    /// <summary>
    /// JWT token expiration in minutes
    /// </summary>
    public int JwtExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Valid API keys (should be set via environment variable or secure configuration)
    /// </summary>
    public string[]? ValidApiKeys { get; set; }

    /// <summary>
    /// Header name for API key authentication
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Whether to require authentication for all endpoints
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;

    /// <summary>
    /// Endpoints that are exempt from authentication
    /// </summary>
    public string[] AnonymousEndpoints { get; set; } = new[]
    {
        "/health",
        "/healthz",
        "/api/health",
        "/swagger",
        "/api-docs"
    };
}
