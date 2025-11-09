using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.FFmpeg;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Services.FFmpeg;

/// <summary>
/// Tests for FFmpegService progress parsing and execution
/// </summary>
public class FFmpegServiceProgressTests
{
    [Fact]
    public void ParseProgress_WithValidProgressLine_ReturnsProgress()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "frame=  123 fps= 45 q=28.0 size=    1024kB time=00:00:05.12 bitrate=1638.4kbits/s speed=1.5x";
        
        // Act
        var progress = InvokeParseProgress(service, line, null);
        
        // Assert
        Assert.NotNull(progress);
        Assert.Equal(123, progress.Frame);
        Assert.Equal(45, progress.Fps);
        Assert.Equal(TimeSpan.FromSeconds(5.12), progress.ProcessedDuration);
        Assert.Equal(1.5, progress.Speed);
    }
    
    [Fact]
    public void ParseProgress_WithTotalDuration_CalculatesPercentage()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "frame=  123 fps= 45 q=28.0 size=    1024kB time=00:00:05.00 bitrate=1638.4kbits/s speed=1.5x";
        var totalDuration = TimeSpan.FromSeconds(10); // 5 seconds out of 10 = 50%
        
        // Act
        var progress = InvokeParseProgress(service, line, totalDuration);
        
        // Assert
        Assert.NotNull(progress);
        Assert.Equal(50.0, progress.PercentComplete, precision: 1);
    }
    
    [Fact]
    public void ParseProgress_AtCompletion_Returns100Percent()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "frame=  300 fps= 45 q=28.0 size=    2048kB time=00:00:10.00 bitrate=1638.4kbits/s speed=1.5x";
        var totalDuration = TimeSpan.FromSeconds(10);
        
        // Act
        var progress = InvokeParseProgress(service, line, totalDuration);
        
        // Assert
        Assert.NotNull(progress);
        Assert.Equal(100.0, progress.PercentComplete);
    }
    
    [Fact]
    public void ParseProgress_WithoutTimeOrFrame_ReturnsNull()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "Stream #0:0: Video: h264";
        
        // Act
        var progress = InvokeParseProgress(service, line, null);
        
        // Assert
        Assert.Null(progress);
    }
    
    [Fact]
    public void ParseDuration_WithValidDurationLine_ReturnsDuration()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "  Duration: 00:01:23.45, start: 0.000000, bitrate: 1234 kb/s";
        
        // Act
        var duration = InvokeParseDuration(service, line);
        
        // Assert
        Assert.NotNull(duration);
        Assert.Equal(TimeSpan.FromHours(0) + TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(23.45), duration.Value);
    }
    
    [Fact]
    public void ParseDuration_WithInvalidLine_ReturnsNull()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "Stream #0:0: Video: h264";
        
        // Act
        var duration = InvokeParseDuration(service, line);
        
        // Assert
        Assert.Null(duration);
    }
    
    [Fact]
    public void ParseProgress_WithBitrateAndSize_ParsesCorrectly()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "frame=  150 fps= 30 q=25.0 size=    2048kB time=00:00:05.00 bitrate=3276.8kbits/s speed=1.0x";
        
        // Act
        var progress = InvokeParseProgress(service, line, null);
        
        // Assert
        Assert.NotNull(progress);
        Assert.Equal(2048, progress.Size);
        Assert.Equal(3276.8, progress.Bitrate);
    }
    
    [Fact]
    public void ParseProgress_WithVaryingWhitespace_ParsesCorrectly()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "frame=123 fps=45.6 time=00:01:30.50 speed=2.5x";
        
        // Act
        var progress = InvokeParseProgress(service, line, null);
        
        // Assert
        Assert.NotNull(progress);
        Assert.Equal(123, progress.Frame);
        Assert.Equal(45.6, progress.Fps);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30.5), progress.ProcessedDuration);
        Assert.Equal(2.5, progress.Speed);
    }
    
    [Fact]
    public void ParseProgress_PercentageNeverExceeds100()
    {
        // Arrange
        var service = CreateFFmpegService();
        // Simulate FFmpeg reporting time beyond total duration (can happen due to timing)
        var line = "frame=  400 fps= 45 time=00:00:12.00 speed=1.5x";
        var totalDuration = TimeSpan.FromSeconds(10);
        
        // Act
        var progress = InvokeParseProgress(service, line, totalDuration);
        
        // Assert
        Assert.NotNull(progress);
        Assert.Equal(100.0, progress.PercentComplete); // Clamped to 100
    }
    
    [Fact]
    public void ParseProgress_WithMalformedData_ReturnsNullOrDefault()
    {
        // Arrange
        var service = CreateFFmpegService();
        var line = "frame=abc fps=xyz time=invalid";
        
        // Act
        var progress = InvokeParseProgress(service, line, null);
        
        // Assert
        // The implementation returns null if no valid data can be parsed
        // If it returns a default object with zeros, that's also acceptable
        if (progress != null)
        {
            Assert.Equal(0, progress.Frame);
            Assert.Equal(TimeSpan.Zero, progress.ProcessedDuration);
        }
    }
    
    private FFmpegService CreateFFmpegService()
    {
        var mockLocator = new Mock<Aura.Core.Dependencies.IFfmpegLocator>();
        mockLocator.Setup(x => x.GetEffectiveFfmpegPathAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ffmpeg");
        
        var mockLogger = new Mock<ILogger<FFmpegService>>();
        
        return new FFmpegService(mockLocator.Object, mockLogger.Object);
    }
    
    // Helper methods to test internal methods - no reflection needed with InternalsVisibleTo
    private FFmpegProgress? InvokeParseProgress(FFmpegService service, string line, TimeSpan? totalDuration)
    {
        return service.ParseProgress(line, totalDuration);
    }
    
    // Helper method to invoke internal ParseDuration method
    private TimeSpan? InvokeParseDuration(FFmpegService service, string line)
    {
        return service.ParseDuration(line);
    }
}
