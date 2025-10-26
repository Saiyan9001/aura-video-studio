using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace Aura.Api.Middleware;

/// <summary>
/// Rate limiting middleware to prevent abuse
/// - 100 requests/minute for general endpoints
/// - 10 requests/minute for export/processing endpoints
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    // Track request counts per client IP
    private static readonly ConcurrentDictionary<string, ClientRateLimit> _rateLimits = new();
    
    // Cleanup old entries periodically
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get client identifier (IP address)
        var clientId = GetClientIdentifier(context);
        
        // Get rate limit for this endpoint
        var (limit, window) = GetRateLimitForEndpoint(context.Request.Path);
        
        // Get or create rate limit tracker for this client
        var rateLimit = _rateLimits.GetOrAdd(clientId, _ => new ClientRateLimit());
        
        // Check if rate limit exceeded
        if (rateLimit.IsRateLimited(limit, window))
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on path {Path}", clientId, context.Request.Path);
            await ReturnRateLimitError(context, rateLimit.GetRetryAfterSeconds(window));
            return;
        }
        
        // Record this request
        rateLimit.RecordRequest();
        
        // Clean up old entries periodically
        PeriodicCleanup();
        
        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try to get real IP from proxy headers first
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain
            var ip = forwardedFor.Split(',')[0].Trim();
            return ip;
        }
        
        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static (int limit, TimeSpan window) GetRateLimitForEndpoint(string path)
    {
        // Export and processing endpoints have stricter limits
        if (path.Contains("/export", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/render", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/jobs", StringComparison.OrdinalIgnoreCase) && 
            !path.EndsWith("/jobs", StringComparison.OrdinalIgnoreCase)) // Allow listing jobs
        {
            return (10, TimeSpan.FromMinutes(1)); // 10 requests per minute
        }
        
        // General endpoints
        return (100, TimeSpan.FromMinutes(1)); // 100 requests per minute
    }

    private static async Task ReturnRateLimitError(HttpContext context, int retryAfterSeconds)
    {
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N");
        
        var error = new
        {
            errorCode = "E429",
            message = "Rate limit exceeded",
            technicalDetails = "Too many requests. Please slow down and try again later.",
            suggestedActions = new[]
            {
                $"Wait {retryAfterSeconds} seconds before retrying",
                "Reduce the frequency of requests"
            },
            correlationId,
            timestamp = DateTime.UtcNow,
            retryAfter = retryAfterSeconds
        };

        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }

    private static void PeriodicCleanup()
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
            return;

        _lastCleanup = DateTime.UtcNow;

        // Remove entries that haven't been used in the last 10 minutes
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        var keysToRemove = _rateLimits
            .Where(kvp => kvp.Value.LastRequestTime < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _rateLimits.TryRemove(key, out _);
        }
    }

    private class ClientRateLimit
    {
        private readonly object _lock = new();
        private readonly List<DateTime> _requests = new();
        public DateTime LastRequestTime { get; private set; } = DateTime.UtcNow;

        public void RecordRequest()
        {
            lock (_lock)
            {
                _requests.Add(DateTime.UtcNow);
                LastRequestTime = DateTime.UtcNow;
            }
        }

        public bool IsRateLimited(int limit, TimeSpan window)
        {
            lock (_lock)
            {
                // Remove old requests outside the window
                var cutoff = DateTime.UtcNow - window;
                _requests.RemoveAll(r => r < cutoff);
                
                // Check if we've exceeded the limit
                return _requests.Count >= limit;
            }
        }

        public int GetRetryAfterSeconds(TimeSpan window)
        {
            lock (_lock)
            {
                if (_requests.Count == 0)
                    return 0;

                // Calculate when the oldest request will expire
                var oldestRequest = _requests.Min();
                var expiryTime = oldestRequest + window;
                var secondsUntilExpiry = (int)(expiryTime - DateTime.UtcNow).TotalSeconds;
                
                return Math.Max(1, secondsUntilExpiry);
            }
        }
    }
}

/// <summary>
/// Extension methods for registering the RateLimitingMiddleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
