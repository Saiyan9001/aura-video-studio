using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Aura.Api.Middleware;

/// <summary>
/// Middleware for setting response caching headers based on endpoint
/// </summary>
public class ResponseCachingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseCachingMiddleware> _logger;

    public ResponseCachingMiddleware(RequestDelegate next, ILogger<ResponseCachingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var method = context.Request.Method;

        if (method == "GET")
        {
            if (path.StartsWith("/api/health") || path.StartsWith("/health"))
            {
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
            }
            else if (path.StartsWith("/api/settings") || path.StartsWith("/api/providers"))
            {
                context.Response.Headers["Cache-Control"] = "private, max-age=300";
                context.Response.Headers["Vary"] = "Accept, Accept-Encoding";
            }
            else if (path.StartsWith("/api/jobs") && path.Contains("/status"))
            {
                context.Response.Headers["Cache-Control"] = "private, max-age=5";
            }
            else if (path.StartsWith("/api/assets") || path.StartsWith("/api/stock"))
            {
                context.Response.Headers["Cache-Control"] = "public, max-age=3600";
                context.Response.Headers["Vary"] = "Accept, Accept-Encoding";
            }
            else if (path.StartsWith("/api/"))
            {
                context.Response.Headers["Cache-Control"] = "private, max-age=60";
                context.Response.Headers["Vary"] = "Accept, Accept-Encoding";
            }

            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode == 200)
                {
                    var etag = GenerateETag(context.Request.Path, context.Request.QueryString.ToString());
                    context.Response.Headers["ETag"] = etag;
                }
                return Task.CompletedTask;
            });
        }
        else
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        }

        await _next(context);
    }

    private static string GenerateETag(string path, string query)
    {
        var content = $"{path}{query}";
        var hash = content.GetHashCode();
        return $"\"{hash:X}\"";
    }
}
