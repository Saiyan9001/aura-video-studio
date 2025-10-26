using System.Diagnostics;
using System.Text;

namespace Aura.Api.Middleware;

/// <summary>
/// Middleware that logs all API requests and responses with timing information
/// Logs: method, path, status code, duration, user ID (if authenticated)
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health check and static file requests to reduce noise
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
        var requestId = Guid.NewGuid().ToString("N");
        
        // Log request
        var sw = Stopwatch.StartNew();
        var requestLog = BuildRequestLog(context.Request, correlationId, requestId);
        _logger.LogInformation("API Request: {RequestLog}", requestLog);

        // Capture the original response body stream
        var originalBodyStream = context.Response.Body;
        
        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Execute the request
            await _next(context);

            // Log response
            sw.Stop();
            var responseLog = BuildResponseLog(context.Response, sw.ElapsedMilliseconds, correlationId, requestId);
            
            // Determine log level based on status code
            var logLevel = DetermineLogLevel(context.Response.StatusCode);
            _logger.Log(logLevel, "API Response: {ResponseLog}", responseLog);

            // Copy response body back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "API Request Failed: Method={Method}, Path={Path}, Duration={Duration}ms, CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                sw.ElapsedMilliseconds,
                correlationId);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";
        
        // Skip logging for these paths to reduce noise
        return pathValue.Contains("/healthz") ||
               pathValue.Contains("/health/live") ||
               pathValue.StartsWith("/assets/") ||
               pathValue.StartsWith("/wwwroot/") ||
               pathValue.EndsWith(".js") ||
               pathValue.EndsWith(".css") ||
               pathValue.EndsWith(".map") ||
               pathValue.EndsWith(".svg") ||
               pathValue.EndsWith(".png") ||
               pathValue.EndsWith(".jpg") ||
               pathValue.EndsWith(".ico");
    }

    private static string BuildRequestLog(HttpRequest request, string correlationId, string requestId)
    {
        var sb = new StringBuilder();
        sb.Append($"[{request.Method}] {request.Path}{request.QueryString}");
        sb.Append($" | Client={GetClientIp(request)}");
        sb.Append($" | CorrelationId={correlationId}");
        sb.Append($" | RequestId={requestId}");
        
        // Add content type if present
        if (!string.IsNullOrEmpty(request.ContentType))
        {
            sb.Append($" | ContentType={request.ContentType}");
        }
        
        // Add user agent
        var userAgent = request.Headers["User-Agent"].FirstOrDefault();
        if (!string.IsNullOrEmpty(userAgent))
        {
            // Truncate long user agents
            var truncatedUserAgent = userAgent.Length > 50 ? userAgent.Substring(0, 50) + "..." : userAgent;
            sb.Append($" | UserAgent={truncatedUserAgent}");
        }

        return sb.ToString();
    }

    private static string BuildResponseLog(HttpResponse response, long durationMs, string correlationId, string requestId)
    {
        var sb = new StringBuilder();
        sb.Append($"Status={response.StatusCode}");
        sb.Append($" | Duration={durationMs}ms");
        sb.Append($" | CorrelationId={correlationId}");
        sb.Append($" | RequestId={requestId}");
        
        // Add content type if present
        if (!string.IsNullOrEmpty(response.ContentType))
        {
            sb.Append($" | ContentType={response.ContentType}");
        }

        // Add performance category
        var perfCategory = GetPerformanceCategory(durationMs);
        if (!string.IsNullOrEmpty(perfCategory))
        {
            sb.Append($" | Perf={perfCategory}");
        }

        return sb.ToString();
    }

    private static string GetClientIp(HttpRequest request)
    {
        // Try X-Forwarded-For header first (for proxies/load balancers)
        var forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        // Fall back to remote IP
        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static LogLevel DetermineLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,      // Server errors
            >= 400 => LogLevel.Warning,    // Client errors
            _ => LogLevel.Information       // Success
        };
    }

    private static string? GetPerformanceCategory(long durationMs)
    {
        return durationMs switch
        {
            > 5000 => "VERY_SLOW",
            > 2000 => "SLOW",
            > 1000 => "MODERATE",
            > 500 => "ACCEPTABLE",
            _ => null // Fast, no need to annotate
        };
    }
}

/// <summary>
/// Extension methods for registering the RequestResponseLoggingMiddleware
/// </summary>
public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}
