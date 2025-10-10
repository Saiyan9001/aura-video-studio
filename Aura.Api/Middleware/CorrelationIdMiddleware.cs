using System.Diagnostics;
using Serilog.Context;

namespace Aura.Api.Middleware;

/// <summary>
/// Middleware that ensures every request has a correlation ID for distributed tracing.
/// The correlation ID is added to:
/// - Response headers (X-Correlation-ID)
/// - Serilog logging context
/// - Activity tags for distributed tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from request header or generate new one
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
                          ?? Guid.NewGuid().ToString();

        // Add to response headers
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        // Add to Serilog logging context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Add to Activity tags for distributed tracing
            Activity.Current?.SetTag("correlation_id", correlationId);

            await _next(context);
        }
    }
}
