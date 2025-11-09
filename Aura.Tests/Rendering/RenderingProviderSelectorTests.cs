using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Providers.Rendering;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Rendering;

public class RenderingProviderSelectorTests
{
    private readonly Mock<ILogger<RenderingProviderSelector>> _mockLogger;

    public RenderingProviderSelectorTests()
    {
        _mockLogger = new Mock<ILogger<RenderingProviderSelector>>();
    }

    [Fact]
    public async Task SelectBestProvider_WithPremiumTier_SelectsHardwareProvider()
    {
        // Arrange
        var mockHardwareProvider = CreateMockProvider("Hardware", 90, true, true);
        var mockSoftwareProvider = CreateMockProvider("Software", 10, false, true);

        var providers = new[] { mockHardwareProvider.Object, mockSoftwareProvider.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act
        var result = await selector.SelectBestProviderAsync(isPremium: true, preferHardware: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hardware", result.Name);
        Assert.Equal(90, result.Priority);
    }

    [Fact]
    public async Task SelectBestProvider_WithFreeTier_SelectsSoftwareProvider()
    {
        // Arrange
        var mockHardwareProvider = CreateMockProvider("Hardware", 90, true, true);
        var mockSoftwareProvider = CreateMockProvider("Software", 10, false, true);

        var providers = new[] { mockHardwareProvider.Object, mockSoftwareProvider.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act
        var result = await selector.SelectBestProviderAsync(isPremium: false, preferHardware: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Software", result.Name);
    }

    [Fact]
    public async Task SelectBestProvider_FallbacksWhenHardwareNotAvailable()
    {
        // Arrange
        var mockHardwareProvider = CreateMockProvider("Hardware", 90, true, false);
        var mockSoftwareProvider = CreateMockProvider("Software", 10, false, true);

        var providers = new[] { mockHardwareProvider.Object, mockSoftwareProvider.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act
        var result = await selector.SelectBestProviderAsync(isPremium: true, preferHardware: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Software", result.Name);
    }

    [Fact]
    public async Task SelectBestProvider_ThrowsWhenNoProvidersRegistered()
    {
        // Arrange
        var providers = Array.Empty<IRenderingProvider>();
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => selector.SelectBestProviderAsync(isPremium: true));
    }

    [Fact]
    public async Task GetAvailableProviders_ReturnsOnlyAvailableProviders()
    {
        // Arrange
        var mockAvailableProvider = CreateMockProvider("Available", 90, true, true);
        var mockUnavailableProvider = CreateMockProvider("Unavailable", 10, false, false);

        var providers = new[] { mockAvailableProvider.Object, mockUnavailableProvider.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act
        var result = await selector.GetAvailableProvidersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Available", result[0].Provider.Name);
    }

    [Fact]
    public async Task RenderWithFallback_SucceedsWithFirstProvider()
    {
        // Arrange
        var mockProvider1 = CreateMockProvider("Provider1", 90, true, true);
        var mockProvider2 = CreateMockProvider("Provider2", 10, false, true);

        var timeline = CreateTestTimeline();
        var spec = CreateTestRenderSpec();
        var progress = new Progress<RenderProgress>();

        mockProvider1
            .Setup(p => p.RenderVideoAsync(It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
                It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/output/video.mp4");

        var providers = new[] { mockProvider1.Object, mockProvider2.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act
        var result = await selector.RenderWithFallbackAsync(timeline, spec, progress, isPremium: true);

        // Assert
        Assert.Equal("/output/video.mp4", result);
        mockProvider1.Verify(p => p.RenderVideoAsync(
            It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
            It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockProvider2.Verify(p => p.RenderVideoAsync(
            It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
            It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenderWithFallback_FallsBackOnFirstProviderFailure()
    {
        // Arrange
        var mockProvider1 = CreateMockProvider("Provider1", 90, true, true);
        var mockProvider2 = CreateMockProvider("Provider2", 10, false, true);

        var timeline = CreateTestTimeline();
        var spec = CreateTestRenderSpec();
        var progress = new Progress<RenderProgress>();

        mockProvider1
            .Setup(p => p.RenderVideoAsync(It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
                It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Hardware failure"));

        mockProvider2
            .Setup(p => p.RenderVideoAsync(It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
                It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/output/video.mp4");

        var providers = new[] { mockProvider1.Object, mockProvider2.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act
        var result = await selector.RenderWithFallbackAsync(timeline, spec, progress, isPremium: true);

        // Assert
        Assert.Equal("/output/video.mp4", result);
        mockProvider1.Verify(p => p.RenderVideoAsync(
            It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
            It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
        mockProvider2.Verify(p => p.RenderVideoAsync(
            It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
            It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenderWithFallback_ThrowsWhenAllProvidersFail()
    {
        // Arrange
        var mockProvider1 = CreateMockProvider("Provider1", 90, true, true);
        var mockProvider2 = CreateMockProvider("Provider2", 10, false, true);

        var timeline = CreateTestTimeline();
        var spec = CreateTestRenderSpec();
        var progress = new Progress<RenderProgress>();

        mockProvider1
            .Setup(p => p.RenderVideoAsync(It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
                It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Hardware failure"));

        mockProvider2
            .Setup(p => p.RenderVideoAsync(It.IsAny<Core.Providers.Timeline>(), It.IsAny<RenderSpec>(), 
                It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Software failure"));

        var providers = new[] { mockProvider1.Object, mockProvider2.Object };
        var selector = new RenderingProviderSelector(_mockLogger.Object, providers);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => selector.RenderWithFallbackAsync(timeline, spec, progress, isPremium: true));
    }

    private Mock<IRenderingProvider> CreateMockProvider(
        string name, 
        int priority, 
        bool isHardware, 
        bool isAvailable)
    {
        var mock = new Mock<IRenderingProvider>();
        
        mock.Setup(p => p.Name).Returns(name);
        mock.Setup(p => p.Priority).Returns(priority);
        mock.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isAvailable);
        
        var capabilities = new RenderingCapabilities(
            ProviderName: name,
            IsHardwareAccelerated: isHardware,
            AccelerationType: isHardware ? "Hardware" : "Software",
            IsAvailable: isAvailable,
            SupportedCodecs: new[] { "h264", "h265" },
            Description: $"{name} provider"
        );
        
        mock.Setup(p => p.GetHardwareCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(capabilities);
        
        return mock;
    }

    private Core.Providers.Timeline CreateTestTimeline()
    {
        return new Core.Providers.Timeline(
            Scenes: new List<Scene>(),
            SceneAssets: new Dictionary<int, IReadOnlyList<Asset>>(),
            NarrationPath: "/test/narration.wav",
            MusicPath: "/test/music.wav",
            SubtitlesPath: null
        );
    }

    private RenderSpec CreateTestRenderSpec()
    {
        return new RenderSpec(
            Res: new Resolution(1920, 1080),
            Fps: 30,
            Codec: "h264",
            Container: "mp4",
            VideoBitrateK: 5000,
            AudioBitrateK: 192,
            QualityLevel: 75
        );
    }
}
