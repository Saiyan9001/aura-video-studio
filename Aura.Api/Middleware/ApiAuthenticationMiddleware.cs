using System;
using System.Linq;
using System.Threading.Tasks;
using Aura.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aura.Api.Middleware;

/// <summary>
/// Middleware for API authentication using API keys or JWT tokens
/// This is a lightweight authentication scheme suitable for local/development scenarios
/// </summary>
public class ApiAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiAuthenticationMiddleware> _logger;
    private readonly ApiAuthenticationOptions _options;

    public ApiAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiAuthenticationMiddleware> logger,
        IOptions<ApiAuthenticationOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var correlationId = context.TraceIdentifier;

        // Check if endpoint is exempt from authentication
        if (IsAnonymousEndpoint(path))
        {
            await _next(context);
            return;
        }

        // If authentication is not required globally, allow through
        if (!_options.RequireAuthentication)
        {
            await _next(context);
            return;
        }

        // Try API key authentication first
        if (_options.EnableApiKeyAuthentication && ValidateApiKey(context))
        {
            _logger.LogDebug("[{CorrelationId}] Request authenticated via API key", correlationId);
            await _next(context);
            return;
        }

        // Try JWT authentication
        if (_options.EnableJwtAuthentication && ValidateJwtToken(context))
        {
            _logger.LogDebug("[{CorrelationId}] Request authenticated via JWT", correlationId);
            await _next(context);
            return;
        }

        // Authentication failed
        _logger.LogWarning(
            "[{CorrelationId}] Unauthorized request to {Path}",
            correlationId, path);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.Append("WWW-Authenticate", "ApiKey");
        
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://docs.aura.studio/errors/E401",
            title = "Unauthorized",
            status = 401,
            detail = "Authentication is required. Provide a valid API key via X-API-Key header.",
            correlationId
        });
    }

    private bool IsAnonymousEndpoint(string path)
    {
        return _options.AnonymousEndpoints.Any(endpoint =>
            path.StartsWith(endpoint.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
    }

    private bool ValidateApiKey(HttpContext context)
    {
        if (_options.ValidApiKeys == null || _options.ValidApiKeys.Length == 0)
        {
            // No API keys configured
            // If authentication is required, this should fail
            if (_options.RequireAuthentication && _options.EnableApiKeyAuthentication)
            {
                _logger.LogWarning("API key authentication is enabled but no valid keys are configured");
                return false;
            }
            return true; // Allow through if authentication not required
        }

        var apiKey = context.Request.Headers[_options.ApiKeyHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            return false;
        }

        // Constant-time comparison to prevent timing attacks
        return _options.ValidApiKeys.Any(validKey => 
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(apiKey),
                System.Text.Encoding.UTF8.GetBytes(validKey)
            ));
    }

    private bool ValidateJwtToken(HttpContext context)
    {
        // Basic JWT validation
        // For production, use Microsoft.AspNetCore.Authentication.JwtBearer
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        
        // For now, just check if token is present
        // Full JWT validation would require JWT library
        return !string.IsNullOrEmpty(token);
    }
}

/// <summary>
/// Extension methods for API authentication middleware
/// </summary>
public static class ApiAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiAuthenticationMiddleware>();
    }
}
