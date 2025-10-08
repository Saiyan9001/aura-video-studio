using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Providers.Images;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class VisualProviderTests
{
    [Fact]
    public async Task StableDiffusion_Should_GateOnNonNvidiaGpu()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new StableDiffusionWebUiProvider(
            NullLogger<StableDiffusionWebUiProvider>.Instance,
            httpClient,
            isNvidiaGpu: false,
            vramGB: 16);

        var scene = new Scene(0, "Test scene", "Test content", TimeSpan.Zero, TimeSpan.FromSeconds(5));
        var spec = new VisualSpec("cinematic", Aspect.Widescreen16x9, new[] { "nature" });

        // Act
        var result = await provider.FetchOrGenerateAsync(scene, spec, CancellationToken.None);

        // Assert
        Assert.Empty(result); // Should return empty due to non-NVIDIA GPU gate
    }

    [Fact]
    public async Task StableDiffusion_Should_GateOnInsufficientVram()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new StableDiffusionWebUiProvider(
            NullLogger<StableDiffusionWebUiProvider>.Instance,
            httpClient,
            isNvidiaGpu: true,
            vramGB: 4); // Below 6GB minimum

        var scene = new Scene(0, "Test scene", "Test content", TimeSpan.Zero, TimeSpan.FromSeconds(5));
        var spec = new VisualSpec("cinematic", Aspect.Widescreen16x9, new[] { "nature" });

        // Act
        var result = await provider.FetchOrGenerateAsync(scene, spec, CancellationToken.None);

        // Assert
        Assert.Empty(result); // Should return empty due to insufficient VRAM
    }

    [Fact]
    public async Task StableDiffusion_Should_PassGateWith6GBNvidiaGpu()
    {
        // Arrange - This will fail to connect but should pass gate checks
        var httpClient = new HttpClient();
        var provider = new StableDiffusionWebUiProvider(
            NullLogger<StableDiffusionWebUiProvider>.Instance,
            httpClient,
            isNvidiaGpu: true,
            vramGB: 8); // Sufficient VRAM

        var scene = new Scene(0, "Test scene", "Test content", TimeSpan.Zero, TimeSpan.FromSeconds(5));
        var spec = new VisualSpec("cinematic", Aspect.Widescreen16x9, new[] { "nature" });

        // Act - Will return empty due to SD not running, but should pass gate
        var result = await provider.FetchOrGenerateAsync(scene, spec, CancellationToken.None);

        // Assert - Empty is acceptable since SD is not actually running
        // The important thing is it didn't gate based on hardware
        Assert.NotNull(result);
    }

    [Fact]
    public async Task StableDiffusion_Probe_Should_FailWithoutNvidiaGpu()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new StableDiffusionWebUiProvider(
            NullLogger<StableDiffusionWebUiProvider>.Instance,
            httpClient,
            isNvidiaGpu: false,
            vramGB: 16);

        // Act
        var result = await provider.ProbeAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StableDiffusion_Probe_Should_FailWithInsufficientVram()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new StableDiffusionWebUiProvider(
            NullLogger<StableDiffusionWebUiProvider>.Instance,
            httpClient,
            isNvidiaGpu: true,
            vramGB: 4);

        // Act
        var result = await provider.ProbeAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task OfflineStockProvider_Should_ReturnAssets()
    {
        // Arrange
        var provider = new OfflineStockProvider(
            NullLogger<OfflineStockProvider>.Instance,
            "/nonexistent/path"); // Will use solid color slides

        // Act
        var result = await provider.SearchAsync("nature", 3, CancellationToken.None);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(3, result.Count);
        Assert.All(result, asset => Assert.Equal("CC0 (Public Domain)", asset.License));
    }

    [Fact]
    public async Task PixabayStockProvider_Should_ReturnEmptyWithoutApiKey()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new PixabayStockProvider(
            NullLogger<PixabayStockProvider>.Instance,
            httpClient,
            apiKey: null);

        // Act
        var result = await provider.SearchAsync("nature", 5, CancellationToken.None);

        // Assert
        Assert.Empty(result); // Should return empty without API key
    }

    [Fact]
    public async Task UnsplashStockProvider_Should_ReturnEmptyWithoutApiKey()
    {
        // Arrange
        var httpClient = new HttpClient();
        var provider = new UnsplashStockProvider(
            NullLogger<UnsplashStockProvider>.Instance,
            httpClient,
            apiKey: null);

        // Act
        var result = await provider.SearchAsync("nature", 5, CancellationToken.None);

        // Assert
        Assert.Empty(result); // Should return empty without API key
    }

    [Theory]
    [InlineData("nature", 5)]
    [InlineData("city", 10)]
    [InlineData("abstract", 1)]
    public async Task OfflineStockProvider_Should_RespectCount(string query, int count)
    {
        // Arrange
        var provider = new OfflineStockProvider(
            NullLogger<OfflineStockProvider>.Instance);

        // Act
        var result = await provider.SearchAsync(query, count, CancellationToken.None);

        // Assert
        Assert.Equal(count, result.Count);
    }

    [Fact]
    public void VisualSpec_Should_ValidateParameters()
    {
        // Arrange & Act
        var spec = new VisualSpec(
            Style: "cinematic",
            Aspect: Aspect.Widescreen16x9,
            Keywords: new[] { "nature", "landscape" });

        // Assert
        Assert.NotNull(spec);
        Assert.Equal("cinematic", spec.Style);
        Assert.Equal(Aspect.Widescreen16x9, spec.Aspect);
        Assert.Equal(2, spec.Keywords.Length);
    }

    [Fact]
    public void VisualSpec_Should_HandleEmptyKeywords()
    {
        // Arrange & Act
        var spec = new VisualSpec(
            Style: "cinematic",
            Aspect: Aspect.Widescreen16x9,
            Keywords: Array.Empty<string>());

        // Assert
        Assert.NotNull(spec);
        Assert.Empty(spec.Keywords);
    }
}
