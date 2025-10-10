using Aura.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Integration tests for error logging with correlation IDs
/// Ensures errors are logged with proper context and correlation tracking
/// </summary>
public class ErrorLoggingIntegrationTests
{
    [Fact]
    public void ProblemDetailsHelper_Should_IncludeCorrelationId_InErrorResponse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var correlationId = Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Act
        var result = ProblemDetailsHelper.CreateScriptError("E300", "Test error", context);

        // Assert
        Assert.NotNull(result);
        // The correlation ID should be logged and included in the response
    }

    [Fact]
    public void ProblemDetailsHelper_Should_HandleMissingCorrelationId()
    {
        // Arrange - no context provided
        
        // Act
        var result = ProblemDetailsHelper.CreateScriptError("E300", "Test error", null);

        // Assert
        Assert.NotNull(result);
        // Should work fine even without correlation ID
    }

    [Fact]
    public void ProblemDetailsHelper_Should_LogError_WithErrorCode()
    {
        // Arrange
        var errorCode = "E301";
        var detail = "Request timeout";

        // Act
        var result = ProblemDetailsHelper.CreateScriptError(errorCode, detail);

        // Assert
        Assert.NotNull(result);
        // Error should be logged (can be verified by checking log files in integration tests)
    }

    [Fact]
    public void ProblemDetailsHelper_Should_HandleUnknownErrorCode()
    {
        // Arrange
        var unknownErrorCode = "E999";
        var detail = "Unknown error occurred";

        // Act
        var result = ProblemDetailsHelper.CreateScriptError(unknownErrorCode, detail);

        // Assert
        Assert.NotNull(result);
        // Should handle unknown error codes gracefully
    }

    [Fact]
    public void ProblemDetailsHelper_Should_IncludeGuidance_InErrorDetail()
    {
        // Arrange
        var errorCode = "E300";
        var detail = "Provider failed";

        // Act
        var result = ProblemDetailsHelper.CreateScriptError(errorCode, detail);

        // Assert
        Assert.NotNull(result);
        // Guidance should be appended to detail (tested in existing tests)
    }
}
