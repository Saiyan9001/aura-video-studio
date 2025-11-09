using System;
using System.Collections.Generic;
using Aura.Core.Errors;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests for custom exception types to ensure they provide proper error information
/// </summary>
public class ExceptionTests
{
    [Fact]
    public void PipelineException_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var exception = new PipelineException(
            "Script",
            "Failed to generate script",
            completedTasks: 0,
            totalTasks: 5,
            correlationId: "test-123");

        // Assert
        Assert.Equal("Script", exception.Stage);
        Assert.Equal("Failed to generate script", exception.Message);
        Assert.Equal(0, exception.CompletedTasks);
        Assert.Equal(5, exception.TotalTasks);
        Assert.Equal("test-123", exception.CorrelationId);
        Assert.Equal("E101", exception.ErrorCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public void PipelineException_ScriptGenerationFailed_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = PipelineException.ScriptGenerationFailed(
            "LLM provider timeout",
            correlationId: "corr-456");

        // Assert
        Assert.Equal("Script", exception.Stage);
        Assert.Contains("LLM provider timeout", exception.Message);
        Assert.Equal("corr-456", exception.CorrelationId);
        Assert.Contains("script", exception.UserMessage.ToLower());
    }

    [Fact]
    public void PipelineException_TtsFailed_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = PipelineException.TtsFailed(
            "Voice synthesis failed",
            correlationId: "corr-789");

        // Assert
        Assert.Equal("TTS", exception.Stage);
        Assert.Contains("Voice synthesis failed", exception.Message);
        Assert.Contains("audio narration", exception.UserMessage.ToLower());
    }

    [Fact]
    public void PipelineException_VisualGenerationFailed_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = PipelineException.VisualGenerationFailed(
            "Image API unavailable",
            correlationId: "corr-101");

        // Assert
        Assert.Equal("Visual", exception.Stage);
        Assert.Contains("Image API unavailable", exception.Message);
        Assert.Contains("visuals", exception.UserMessage.ToLower());
    }

    [Fact]
    public void PipelineException_RenderFailed_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = PipelineException.RenderFailed(
            "FFmpeg crashed",
            correlationId: "corr-202");

        // Assert
        Assert.Equal("Render", exception.Stage);
        Assert.Contains("FFmpeg crashed", exception.Message);
        Assert.Contains("render", exception.UserMessage.ToLower());
    }

    [Fact]
    public void PipelineException_ToErrorResponse_IncludesPipelineDetails()
    {
        // Arrange
        var exception = new PipelineException(
            "TTS",
            "Audio generation failed",
            completedTasks: 2,
            totalTasks: 4);

        // Act
        var errorResponse = exception.ToErrorResponse();

        // Assert
        Assert.NotNull(errorResponse);
        Assert.True(errorResponse.ContainsKey("pipeline"));
        var pipeline = errorResponse["pipeline"] as dynamic;
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void PipelineException_WithContext_AddsContextInformation()
    {
        // Arrange
        var exception = new PipelineException("Script", "Test error");

        // Act
        exception.WithContext("detail", "Additional information");

        // Assert
        Assert.True(exception.Context.ContainsKey("detail"));
        Assert.Equal("Additional information", exception.Context["detail"]);
    }

    [Fact]
    public void ConfigurationException_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var exception = new ConfigurationException(
            "OpenAI.ApiKey",
            "API key is required",
            expectedFormat: "sk-...",
            actualValue: "",
            correlationId: "cfg-123");

        // Assert
        Assert.Equal("OpenAI.ApiKey", exception.ConfigurationKey);
        Assert.Equal("API key is required", exception.Message);
        Assert.Equal("sk-...", exception.ExpectedFormat);
        Assert.Equal("", exception.ActualValue);
        Assert.Equal("cfg-123", exception.CorrelationId);
        Assert.Equal("E003", exception.ErrorCode);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public void ConfigurationException_MissingRequired_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ConfigurationException.MissingRequired(
            "FFmpeg.Path",
            correlationId: "cfg-456");

        // Assert
        Assert.Equal("FFmpeg.Path", exception.ConfigurationKey);
        Assert.Contains("missing", exception.Message.ToLower());
        Assert.Contains("missing", exception.UserMessage.ToLower());
        Assert.NotEmpty(exception.SuggestedActions);
    }

    [Fact]
    public void ConfigurationException_InvalidFormat_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ConfigurationException.InvalidFormat(
            "AspectRatio",
            "16:9 or 9:16",
            actualValue: "invalid",
            correlationId: "cfg-789");

        // Assert
        Assert.Equal("AspectRatio", exception.ConfigurationKey);
        Assert.Equal("16:9 or 9:16", exception.ExpectedFormat);
        Assert.Equal("invalid", exception.ActualValue);
        Assert.Contains("invalid format", exception.Message.ToLower());
    }

    [Fact]
    public void ConfigurationException_InvalidApiKey_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ConfigurationException.InvalidApiKey(
            "OpenAI",
            "OpenAI.ApiKey",
            "sk-...",
            correlationId: "cfg-101");

        // Assert
        Assert.Equal("OpenAI.ApiKey", exception.ConfigurationKey);
        Assert.Contains("OpenAI", exception.Message);
        Assert.Contains("api key", exception.UserMessage.ToLower());
        Assert.Contains("obtain", exception.SuggestedActions[0].ToLower());
    }

    [Fact]
    public void ConfigurationException_InvalidPath_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ConfigurationException.InvalidPath(
            "FFmpeg.Path",
            "/nonexistent/ffmpeg",
            "File does not exist",
            correlationId: "cfg-202");

        // Assert
        Assert.Equal("FFmpeg.Path", exception.ConfigurationKey);
        Assert.Equal("/nonexistent/ffmpeg", exception.ActualValue);
        Assert.Contains("File does not exist", exception.Message);
        Assert.Contains("path", exception.UserMessage.ToLower());
    }

    [Fact]
    public void ConfigurationException_ToErrorResponse_IncludesConfigurationDetails()
    {
        // Arrange
        var exception = new ConfigurationException(
            "TestKey",
            "Test error",
            expectedFormat: "format",
            actualValue: "value");

        // Act
        var errorResponse = exception.ToErrorResponse();

        // Assert
        Assert.NotNull(errorResponse);
        Assert.True(errorResponse.ContainsKey("configuration"));
        var configuration = errorResponse["configuration"] as dynamic;
        Assert.NotNull(configuration);
    }

    [Fact]
    public void ProviderException_MissingApiKey_CreatesTransientFalse()
    {
        // Arrange & Act
        var exception = ProviderException.MissingApiKey(
            "OpenAI",
            ProviderType.LLM,
            "OPENAI_API_KEY");

        // Assert
        Assert.False(exception.IsTransient);
        Assert.Contains("add", exception.SuggestedActions[0].ToLower());
    }

    [Fact]
    public void ProviderException_RateLimited_CreatesTransientTrue()
    {
        // Arrange & Act
        var exception = ProviderException.RateLimited(
            "OpenAI",
            ProviderType.LLM,
            retryAfterSeconds: 60);

        // Assert
        Assert.True(exception.IsTransient);
        Assert.Equal(429, exception.HttpStatusCode);
        Assert.Contains("wait", exception.SuggestedActions[0].ToLower());
    }

    [Fact]
    public void ProviderException_NetworkError_CreatesTransientTrue()
    {
        // Arrange & Act
        var exception = ProviderException.NetworkError(
            "OpenAI",
            ProviderType.LLM);

        // Assert
        Assert.True(exception.IsTransient);
        Assert.Contains("connection", exception.UserMessage.ToLower());
    }

    [Fact]
    public void ProviderException_Timeout_CreatesTransientTrue()
    {
        // Arrange & Act
        var exception = ProviderException.Timeout(
            "OpenAI",
            ProviderType.LLM,
            timeoutSeconds: 120);

        // Assert
        Assert.True(exception.IsTransient);
        Assert.Contains("120", exception.Message);
    }

    [Fact]
    public void AllExceptions_InheritFromAuraException()
    {
        // Arrange & Act
        var pipelineEx = new PipelineException("Test", "Test");
        var configEx = new ConfigurationException("Test", "Test");
        var providerEx = new ProviderException("Test", ProviderType.LLM, "Test");

        // Assert
        Assert.IsAssignableFrom<AuraException>(pipelineEx);
        Assert.IsAssignableFrom<AuraException>(configEx);
        Assert.IsAssignableFrom<AuraException>(providerEx);
    }

    [Fact]
    public void AllExceptions_HaveSuggestedActions()
    {
        // Arrange & Act
        var pipelineEx = PipelineException.ScriptGenerationFailed("test");
        var configEx = ConfigurationException.MissingRequired("test");
        var providerEx = ProviderException.MissingApiKey("test", ProviderType.LLM, "test");

        // Assert
        Assert.NotEmpty(pipelineEx.SuggestedActions);
        Assert.NotEmpty(configEx.SuggestedActions);
        Assert.NotEmpty(providerEx.SuggestedActions);
    }

    [Fact]
    public void AllExceptions_ToErrorResponse_ReturnsValidDictionary()
    {
        // Arrange
        var exceptions = new List<AuraException>
        {
            new PipelineException("Test", "Test"),
            new ConfigurationException("Test", "Test"),
            new ProviderException("Test", ProviderType.LLM, "Test")
        };

        // Act & Assert
        foreach (var exception in exceptions)
        {
            var response = exception.ToErrorResponse();
            Assert.NotNull(response);
            Assert.True(response.ContainsKey("errorCode"));
            Assert.True(response.ContainsKey("message"));
            Assert.True(response.ContainsKey("suggestedActions"));
            Assert.True(response.ContainsKey("isTransient"));
        }
    }
}
