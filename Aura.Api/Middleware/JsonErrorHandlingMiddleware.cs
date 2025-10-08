using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aura.Api.Middleware;

/// <summary>
/// Middleware to catch JSON deserialization errors and return E303 ProblemDetails responses
/// </summary>
public class JsonErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public JsonErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.InnerException is JsonException jsonEx)
        {
            Log.Warning(jsonEx, "JSON deserialization error: {Message}", jsonEx.Message);
            
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = 400,
                Title = "Invalid Enum Value",
                Detail = jsonEx.Message,
                Type = "https://docs.aura.studio/errors/E303"
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "JSON deserialization error: {Message}", ex.Message);
            
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = 400,
                Title = "Invalid Enum Value",
                Detail = ex.Message,
                Type = "https://docs.aura.studio/errors/E303"
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
