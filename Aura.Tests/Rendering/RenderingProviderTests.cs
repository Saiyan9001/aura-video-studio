using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Providers.Rendering;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Rendering;

public class RenderingProviderTests
{
    private readonly Mock<ILogger<BasicFFmpegProvider>> _mockBasicLogger;
    private readonly Mock<ILogger<FFmpegProvider>> _mockFFmpegLogger;
    private readonly Mock<ILogger<FFmpegNvidiaProvider>> _mockNvidiaLogger;
    private readonly Mock<ILogger<FFmpegAmdProvider>> _mockAmdLogger;
    private readonly Mock<ILogger<FFmpegIntelProvider>> _mockIntelLogger;
    private readonly Mock<IFfmpegLocator> _mockFfmpegLocator;

    public RenderingProviderTests()
    {
        _mockBasicLogger = new Mock<ILogger<BasicFFmpegProvider>>();
        _mockFFmpegLogger = new Mock<ILogger<FFmpegProvider>>();
        _mockNvidiaLogger = new Mock<ILogger<FFmpegNvidiaProvider>>();
        _mockAmdLogger = new Mock<ILogger<FFmpegAmdProvider>>();
        _mockIntelLogger = new Mock<ILogger<FFmpegIntelProvider>>();
        _mockFfmpegLocator = new Mock<IFfmpegLocator>();
    }

    [Fact]
    public void BasicFFmpegProvider_HasCorrectPriority()
    {
        // Arrange
        var provider = new BasicFFmpegProvider(_mockBasicLogger.Object, _mockFfmpegLocator.Object);

        // Assert
        Assert.Equal("BasicFFmpeg", provider.Name);
        Assert.Equal(10, provider.Priority);
    }

    [Fact]
    public void FFmpegProvider_HasHighestPriority()
    {
        // Arrange
        var provider = new FFmpegProvider(_mockFFmpegLogger.Object, _mockFfmpegLocator.Object);

        // Assert
        Assert.Equal("FFmpeg", provider.Name);
        Assert.Equal(100, provider.Priority);
    }

    [Fact]
    public void FFmpegNvidiaProvider_HasCorrectPriority()
    {
        // Arrange
        var provider = new FFmpegNvidiaProvider(_mockNvidiaLogger.Object, _mockFfmpegLocator.Object);

        // Assert
        Assert.Equal("FFmpegNVENC", provider.Name);
        Assert.Equal(90, provider.Priority);
    }

    [Fact]
    public void FFmpegAmdProvider_HasCorrectPriority()
    {
        // Arrange
        var provider = new FFmpegAmdProvider(_mockAmdLogger.Object, _mockFfmpegLocator.Object);

        // Assert
        Assert.Equal("FFmpegAMF", provider.Name);
        Assert.Equal(80, provider.Priority);
    }

    [Fact]
    public void FFmpegIntelProvider_HasCorrectPriority()
    {
        // Arrange
        var provider = new FFmpegIntelProvider(_mockIntelLogger.Object, _mockFfmpegLocator.Object);

        // Assert
        Assert.Equal("FFmpegQSV", provider.Name);
        Assert.Equal(70, provider.Priority);
    }

    [Fact]
    public async Task BasicFFmpegProvider_GetCapabilities_ReturnsSoftwareCapabilities()
    {
        // Arrange
        _mockFfmpegLocator
            .Setup(x => x.GetEffectiveFfmpegPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ffmpeg");
        
        var provider = new BasicFFmpegProvider(_mockBasicLogger.Object, _mockFfmpegLocator.Object);

        // Act
        var capabilities = await provider.GetHardwareCapabilitiesAsync();

        // Assert
        Assert.NotNull(capabilities);
        Assert.Equal("BasicFFmpeg", capabilities.ProviderName);
        Assert.False(capabilities.IsHardwareAccelerated);
        Assert.Equal("Software", capabilities.AccelerationType);
        Assert.Contains("h264", capabilities.SupportedCodecs);
        Assert.Contains("h265", capabilities.SupportedCodecs);
    }

    [Fact]
    public async Task FFmpegNvidiaProvider_GetCapabilities_ReturnsNVENCCapabilities()
    {
        // Arrange
        _mockFfmpegLocator
            .Setup(x => x.GetEffectiveFfmpegPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ffmpeg");
        
        var provider = new FFmpegNvidiaProvider(_mockNvidiaLogger.Object, _mockFfmpegLocator.Object);

        // Act
        var capabilities = await provider.GetHardwareCapabilitiesAsync();

        // Assert
        Assert.NotNull(capabilities);
        Assert.Equal("FFmpegNVENC", capabilities.ProviderName);
        Assert.True(capabilities.IsHardwareAccelerated);
        Assert.Equal("NVENC", capabilities.AccelerationType);
        Assert.Contains("h264_nvenc", capabilities.SupportedCodecs);
        Assert.Contains("hevc_nvenc", capabilities.SupportedCodecs);
    }

    [Fact]
    public async Task FFmpegAmdProvider_GetCapabilities_ReturnsAMFCapabilities()
    {
        // Arrange
        _mockFfmpegLocator
            .Setup(x => x.GetEffectiveFfmpegPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ffmpeg");
        
        var provider = new FFmpegAmdProvider(_mockAmdLogger.Object, _mockFfmpegLocator.Object);

        // Act
        var capabilities = await provider.GetHardwareCapabilitiesAsync();

        // Assert
        Assert.NotNull(capabilities);
        Assert.Equal("FFmpegAMF", capabilities.ProviderName);
        Assert.True(capabilities.IsHardwareAccelerated);
        Assert.Equal("AMF", capabilities.AccelerationType);
        Assert.Contains("h264_amf", capabilities.SupportedCodecs);
        Assert.Contains("hevc_amf", capabilities.SupportedCodecs);
    }

    [Fact]
    public async Task FFmpegIntelProvider_GetCapabilities_ReturnsQSVCapabilities()
    {
        // Arrange
        _mockFfmpegLocator
            .Setup(x => x.GetEffectiveFfmpegPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ffmpeg");
        
        var provider = new FFmpegIntelProvider(_mockIntelLogger.Object, _mockFfmpegLocator.Object);

        // Act
        var capabilities = await provider.GetHardwareCapabilitiesAsync();

        // Assert
        Assert.NotNull(capabilities);
        Assert.Equal("FFmpegQSV", capabilities.ProviderName);
        Assert.True(capabilities.IsHardwareAccelerated);
        Assert.Equal("QuickSync", capabilities.AccelerationType);
        Assert.Contains("h264_qsv", capabilities.SupportedCodecs);
        Assert.Contains("hevc_qsv", capabilities.SupportedCodecs);
    }

    [Fact]
    public async Task Provider_IsAvailableAsync_ReturnsFalseWhenFFmpegNotFound()
    {
        // Arrange
        _mockFfmpegLocator
            .Setup(x => x.GetEffectiveFfmpegPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FFmpeg not found"));
        
        var provider = new BasicFFmpegProvider(_mockBasicLogger.Object, _mockFfmpegLocator.Object);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }
}
