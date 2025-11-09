using Aura.Core.Errors;
using Xunit;

namespace Aura.Tests;

public class AuraExceptionTests
{
    [Fact]
    public void ProviderException_MissingApiKey_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ProviderException.MissingApiKey("OpenAI", ProviderType.LLM, "OPENAI_API_KEY", "test-correlation");

        // Assert
        Assert.Equal("OpenAI", exception.ProviderName);
        Assert.Equal(ProviderType.LLM, exception.Type);
        Assert.Equal("test-correlation", exception.CorrelationId);
        Assert.Contains("OPENAI_API_KEY", exception.Message);
        Assert.Contains("required but not configured", exception.Message);
        Assert.NotEmpty(exception.SuggestedActions);
    }

    [Fact]
    public void ProviderException_RateLimited_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ProviderException.RateLimited("OpenAI", ProviderType.LLM, 60, "test-correlation");

        // Assert
        Assert.Equal("OpenAI", exception.ProviderName);
        Assert.Equal(ProviderType.LLM, exception.Type);
        Assert.Equal(429, exception.HttpStatusCode);
        Assert.True(exception.IsTransient);
        Assert.Contains("60", exception.Message);
        Assert.NotEmpty(exception.SuggestedActions);
    }

    [Fact]
    public void ProviderException_NetworkError_CreatesCorrectException()
    {
        // Arrange
        var innerException = new System.Net.Http.HttpRequestException("Connection failed");

        // Act
        var exception = ProviderException.NetworkError("OpenAI", ProviderType.LLM, "test-correlation", innerException);

        // Assert
        Assert.Equal("OpenAI", exception.ProviderName);
        Assert.Equal(ProviderType.LLM, exception.Type);
        Assert.True(exception.IsTransient);
        Assert.Equal(innerException, exception.InnerException);
        Assert.Contains("Network error", exception.Message);
    }

    [Fact]
    public void ProviderException_Timeout_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ProviderException.Timeout("OpenAI", ProviderType.LLM, 30, "test-correlation");

        // Assert
        Assert.Equal("OpenAI", exception.ProviderName);
        Assert.Equal(ProviderType.LLM, exception.Type);
        Assert.True(exception.IsTransient);
        Assert.Contains("30", exception.Message);
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public void ResourceException_InsufficientDiskSpace_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ResourceException.InsufficientDiskSpace("/path/to/output", 1024L * 1024 * 100, 1024L * 1024 * 50, "test-correlation");

        // Assert
        Assert.Equal(ResourceType.DiskSpace, exception.ResourceType);
        Assert.Equal("/path/to/output", exception.ResourcePath);
        Assert.Equal("test-correlation", exception.CorrelationId);
        Assert.Contains("E601", exception.ErrorCode);
        Assert.NotEmpty(exception.SuggestedActions);
    }

    [Fact]
    public void ResourceException_InsufficientMemory_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ResourceException.InsufficientMemory(1024L * 1024 * 500, 1024L * 1024 * 300, "test-correlation");

        // Assert
        Assert.Equal(ResourceType.Memory, exception.ResourceType);
        Assert.Equal("test-correlation", exception.CorrelationId);
        Assert.Contains("E602", exception.ErrorCode);
        Assert.NotEmpty(exception.SuggestedActions);
    }

    [Fact]
    public void ResourceException_FileAccessDenied_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ResourceException.FileAccessDenied("/path/to/file.txt", "test-correlation");

        // Assert
        Assert.Equal(ResourceType.FileAccess, exception.ResourceType);
        Assert.Equal("/path/to/file.txt", exception.ResourcePath);
        Assert.Equal("test-correlation", exception.CorrelationId);
        Assert.Contains("E603", exception.ErrorCode);
    }

    [Fact]
    public void ResourceException_FileNotFound_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = ResourceException.FileNotFound("/path/to/missing.txt", "test-correlation");

        // Assert
        Assert.Equal(ResourceType.FileNotFound, exception.ResourceType);
        Assert.Equal("/path/to/missing.txt", exception.ResourcePath);
        Assert.Contains("E605", exception.ErrorCode);
    }

    [Fact]
    public void RenderException_FromFfmpegException_ConvertsCorrectly()
    {
        // Arrange
        var ffmpegEx = FfmpegException.NotFound("/usr/bin/ffmpeg", "test-correlation");

        // Act
        var renderEx = RenderException.FromFfmpegException(ffmpegEx, "job-123");

        // Assert
        Assert.Equal(RenderErrorCategory.FfmpegNotFound, renderEx.Category);
        Assert.Equal("job-123", renderEx.JobId);
        Assert.Equal("test-correlation", renderEx.CorrelationId);
        Assert.Equal(ffmpegEx, renderEx.InnerException);
    }

    [Fact]
    public void RenderException_HardwareEncoderFailed_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = RenderException.HardwareEncoderFailed("h264_nvenc", "job-123", "test-correlation");

        // Assert
        Assert.Equal(RenderErrorCategory.HardwareEncoderFailed, exception.Category);
        Assert.Equal("job-123", exception.JobId);
        Assert.Equal("test-correlation", exception.CorrelationId);
        Assert.Contains("h264_nvenc", exception.Message);
    }

    [Fact]
    public void RenderException_Cancelled_CreatesCorrectException()
    {
        // Arrange & Act
        var exception = RenderException.Cancelled("job-123", "test-correlation");

        // Assert
        Assert.Equal(RenderErrorCategory.Cancelled, exception.Category);
        Assert.Equal("job-123", exception.JobId);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public void AuraException_WithContext_AddsContext()
    {
        // Arrange
        var exception = ProviderException.MissingApiKey("TestProvider", ProviderType.LLM, "TEST_KEY");

        // Act
        exception.WithContext("userId", "user-123")
                 .WithContext("operation", "generateVideo");

        // Assert
        Assert.Equal(4, exception.Context.Count); // 2 from factory + 2 added
        Assert.Equal("user-123", exception.Context["userId"]);
        Assert.Equal("generateVideo", exception.Context["operation"]);
    }

    [Fact]
    public void AuraException_ToErrorResponse_ReturnsCorrectFormat()
    {
        // Arrange
        var exception = ProviderException.RateLimited("TestProvider", ProviderType.TTS, 60, "test-correlation");
        exception.WithContext("userId", "user-123");

        // Act
        var response = exception.ToErrorResponse();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("errorCode"));
        Assert.True(response.ContainsKey("message"));
        Assert.True(response.ContainsKey("suggestedActions"));
        Assert.True(response.ContainsKey("isTransient"));
        Assert.True(response.ContainsKey("correlationId"));
        Assert.True(response.ContainsKey("context"));
        
        Assert.True((bool)response["isTransient"]);
        Assert.Equal("test-correlation", response["correlationId"]);
    }

    [Fact]
    public void ProviderException_ErrorCode_GeneratedCorrectly()
    {
        // Arrange & Act
        var llmException = new ProviderException("TestLLM", ProviderType.LLM, "Test error");
        var ttsException = new ProviderException("TestTTS", ProviderType.TTS, "Test error");
        var imageException = new ProviderException("TestImage", ProviderType.Visual, "Test error");
        var httpException = new ProviderException("TestHTTP", ProviderType.LLM, "Test error", httpStatusCode: 500);

        // Assert
        Assert.StartsWith("E100", llmException.ErrorCode);
        Assert.StartsWith("E200", ttsException.ErrorCode);
        Assert.StartsWith("E400", imageException.ErrorCode);
        Assert.Contains("500", httpException.ErrorCode);
    }

    [Fact]
    public void ResourceException_ErrorCode_GeneratedCorrectly()
    {
        // Arrange & Act
        var diskEx = ResourceException.InsufficientDiskSpace();
        var memEx = ResourceException.InsufficientMemory();
        var fileAccessEx = ResourceException.FileAccessDenied("/test");
        var dirAccessEx = ResourceException.DirectoryAccessDenied("/test");
        var notFoundEx = ResourceException.FileNotFound("/test");
        var lockedEx = ResourceException.FileLocked("/test");

        // Assert
        Assert.Equal("E601", diskEx.ErrorCode);
        Assert.Equal("E602", memEx.ErrorCode);
        Assert.Equal("E603", fileAccessEx.ErrorCode);
        Assert.Equal("E604", dirAccessEx.ErrorCode);
        Assert.Equal("E605", notFoundEx.ErrorCode);
        Assert.Equal("E606", lockedEx.ErrorCode);
    }

    [Fact]
    public void RenderException_ErrorCode_GeneratedCorrectly()
    {
        // Arrange & Act
        var notFoundEx = new RenderException("Test", RenderErrorCategory.FfmpegNotFound);
        var corruptedEx = new RenderException("Test", RenderErrorCategory.FfmpegCorrupted);
        var processFailedEx = new RenderException("Test", RenderErrorCategory.ProcessFailed, exitCode: 1);
        var encoderEx = new RenderException("Test", RenderErrorCategory.EncoderNotAvailable);

        // Assert
        Assert.Equal("E302", notFoundEx.ErrorCode);
        Assert.Equal("E303", corruptedEx.ErrorCode);
        Assert.Equal("E304-1", processFailedEx.ErrorCode);
        Assert.Equal("E305", encoderEx.ErrorCode);
    }
}
