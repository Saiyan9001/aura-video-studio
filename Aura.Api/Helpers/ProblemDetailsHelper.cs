using Microsoft.AspNetCore.Http;

namespace Aura.Api.Helpers;

/// <summary>
/// Helper class for generating consistent ProblemDetails responses with actionable error messages.
/// Implements RFC 7807 Problem Details for HTTP APIs.
/// 
/// Error Code Ranges:
/// - E300-E311: Script generation errors
/// - E303: Invalid enum/input validation
/// - E304: Invalid plan parameters
/// </summary>
public static class ProblemDetailsHelper
{
    /// <summary>
    /// Error codes for script generation pipeline
    /// 
    /// E300: General script provider failure
    /// E301: Request timeout or cancellation
    /// E302: Provider returned empty/invalid script
    /// E303: Invalid enum value or input validation failure
    /// E304: Invalid plan parameters (duration, etc.)
    /// E305: Provider not available/not registered
    /// E306: Provider authentication failure (API key issues)
    /// E307: Offline mode restriction (Pro providers blocked)
    /// E308: Rate limit exceeded
    /// E309: Invalid script format/structure
    /// E310: Content policy violation
    /// E311: Insufficient system resources
    /// </summary>
    private static readonly Dictionary<string, (string Title, int StatusCode, string Guidance)> ErrorDefinitions = new()
    {
        ["E300"] = ("Script Provider Failed", 500, "The script generation service encountered an error. Please try again or contact support if the issue persists."),
        ["E301"] = ("Request Timeout", 408, "The script generation took too long to complete. Try reducing the target duration or simplifying the request."),
        ["E302"] = ("Empty Script Response", 500, "The provider returned an empty script. This may be a temporary issue - please try again."),
        ["E303"] = ("Invalid Enum Value", 400, "One or more enum values are invalid. Check the error details for valid options and correct the request."),
        ["E304"] = ("Invalid Plan", 400, "Plan parameters are invalid. Ensure target duration is between 0 and 120 minutes and other parameters are within acceptable ranges."),
        ["E305"] = ("Provider Not Available", 500, "The requested LLM provider is not available. Try a different provider tier or check system configuration."),
        ["E306"] = ("Authentication Failed", 401, "Provider authentication failed. Check API keys in settings and ensure they are valid and have sufficient credits."),
        ["E307"] = ("Offline Mode Restriction", 403, "Pro providers require internet connection but system is in Offline-Only mode. Disable Offline mode in settings or use Free providers."),
        ["E308"] = ("Rate Limit Exceeded", 429, "Provider rate limit exceeded. Wait a few minutes before retrying or upgrade your API plan."),
        ["E309"] = ("Invalid Script Format", 422, "Generated script has invalid format or structure. This indicates a provider issue - try a different provider."),
        ["E310"] = ("Content Policy Violation", 400, "The requested content violates provider content policies. Modify the topic or tone to comply with content guidelines."),
        ["E311"] = ("Insufficient Resources", 503, "System resources are insufficient for this operation. Try a shorter duration or reduce complexity.")
    };

    /// <summary>
    /// Creates a ProblemDetails result for script generation errors with correlation ID support
    /// </summary>
    public static IResult CreateScriptError(string errorCode, string detail, HttpContext? context = null)
    {
        // Extract correlation ID from context if available
        var correlationId = context?.Response.Headers["X-Correlation-ID"].FirstOrDefault();

        if (!ErrorDefinitions.TryGetValue(errorCode, out var errorDef))
        {
            // Log unknown error code
            Serilog.Log.Error("Unknown error code {ErrorCode}: {Detail}", errorCode, detail);
            
            // Unknown error code - use generic error
            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Detail = detail,
                Status = 500,
                Title = "Script Generation Failed",
                Type = $"https://docs.aura.studio/errors/{errorCode}"
            };
            
            if (!string.IsNullOrEmpty(correlationId))
            {
                problemDetails.Extensions["correlationId"] = correlationId;
            }
            
            return Results.Problem(problemDetails);
        }

        var (title, statusCode, guidance) = errorDef;
        
        // Append guidance to detail if not already present
        var fullDetail = detail;
        if (!string.IsNullOrEmpty(guidance) && !detail.Contains(guidance))
        {
            fullDetail = $"{detail}\n\nAction: {guidance}";
        }

        // Log error with correlation ID and error code
        Serilog.Log.Error("API Error {ErrorCode} ({Title}): {Detail}", errorCode, title, detail);

        var result = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Detail = fullDetail,
            Status = statusCode,
            Title = title,
            Type = $"https://docs.aura.studio/errors/{errorCode}"
        };

        // Add correlation ID to extensions if available
        if (!string.IsNullOrEmpty(correlationId))
        {
            result.Extensions["correlationId"] = correlationId;
        }

        return Results.Problem(result);
    }

    /// <summary>
    /// Creates a ProblemDetails result for invalid enum values with helpful suggestions
    /// </summary>
    public static IResult CreateEnumError(string enumName, string invalidValue, string[] validValues)
    {
        var validList = string.Join(", ", validValues);
        var detail = $"Invalid {enumName} value: '{invalidValue}'. Valid values are: {validList}";
        
        return CreateScriptError("E303", detail);
    }

    /// <summary>
    /// Creates a ProblemDetails result for invalid Brief parameters
    /// </summary>
    public static IResult CreateInvalidBrief(string detail)
    {
        return Results.Problem(
            detail: detail,
            statusCode: 400,
            title: "Invalid Brief",
            type: "https://docs.aura.studio/errors/E303"
        );
    }

    /// <summary>
    /// Creates a ProblemDetails result for invalid Plan parameters
    /// </summary>
    public static IResult CreateInvalidPlan(string detail)
    {
        var fullDetail = $"{detail}\n\nAction: Ensure target duration is between 0 and 120 minutes and all enum values are valid.";
        
        return Results.Problem(
            detail: fullDetail,
            statusCode: 400,
            title: "Invalid Plan",
            type: "https://docs.aura.studio/errors/E304"
        );
    }

    /// <summary>
    /// Gets the status code for an error code
    /// </summary>
    public static int GetStatusCode(string errorCode)
    {
        if (ErrorDefinitions.TryGetValue(errorCode, out var errorDef))
        {
            return errorDef.StatusCode;
        }
        return 500; // Default to internal server error
    }

    /// <summary>
    /// Gets the user-friendly guidance for an error code
    /// </summary>
    public static string GetGuidance(string errorCode)
    {
        if (ErrorDefinitions.TryGetValue(errorCode, out var errorDef))
        {
            return errorDef.Guidance;
        }
        return "Please try again or contact support if the issue persists.";
    }
}
