using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Providers.Visuals;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests for visual provider implementations
/// </summary>
public class VisualProviderTests
{
    #region PlaceholderProvider Tests (Guaranteed Fallback)

    [Fact]
    public async Task PlaceholderProvider_Should_Always_Succeed()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var options = new VisualGenerationOptions { Width = 1024, Height = 1024 };

        var result = await provider.GenerateImageAsync("Test prompt", options);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));

        if (File.Exists(result))
        {
            File.Delete(result);
        }
    }

    [Fact]
    public async Task PlaceholderProvider_Should_Be_Always_Available()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);

        var isAvailable = await provider.IsAvailableAsync();

        Assert.True(isAvailable);
    }

    [Fact]
    public void PlaceholderProvider_Should_Not_Require_ApiKey()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);

        Assert.False(provider.RequiresApiKey);
    }

    [Fact]
    public void PlaceholderProvider_Should_Report_Free_Tier()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var capabilities = provider.GetProviderCapabilities();

        Assert.Equal("Placeholder", capabilities.ProviderName);
        Assert.True(capabilities.IsFree);
        Assert.True(capabilities.IsLocal);
        Assert.Equal(0m, capabilities.CostPerImage);
        Assert.Equal("Free", capabilities.Tier);
    }

    [Fact]
    public async Task PlaceholderProvider_Should_Generate_Different_Colors_For_Different_Prompts()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var options = new VisualGenerationOptions { Width = 512, Height = 512 };

        var result1 = await provider.GenerateImageAsync("First prompt", options);
        var result2 = await provider.GenerateImageAsync("Second prompt", options);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);

        if (File.Exists(result1)) File.Delete(result1);
        if (File.Exists(result2)) File.Delete(result2);
    }

    [Fact]
    public async Task PlaceholderProvider_Should_Handle_Various_Aspect_Ratios()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);

        var wideOptions = new VisualGenerationOptions { Width = 1920, Height = 1080, AspectRatio = "16:9" };
        var portraitOptions = new VisualGenerationOptions { Width = 1080, Height = 1920, AspectRatio = "9:16" };
        var squareOptions = new VisualGenerationOptions { Width = 1024, Height = 1024, AspectRatio = "1:1" };

        var wideResult = await provider.GenerateImageAsync("Wide image", wideOptions);
        var portraitResult = await provider.GenerateImageAsync("Portrait image", portraitOptions);
        var squareResult = await provider.GenerateImageAsync("Square image", squareOptions);

        Assert.NotNull(wideResult);
        Assert.NotNull(portraitResult);
        Assert.NotNull(squareResult);

        if (File.Exists(wideResult)) File.Delete(wideResult);
        if (File.Exists(portraitResult)) File.Delete(portraitResult);
        if (File.Exists(squareResult)) File.Delete(squareResult);
    }

    #endregion

    #region StabilityAiProvider Tests

    [Fact]
    public async Task StabilityAiProvider_Without_ApiKey_Should_Not_Generate()
    {
        var provider = new StabilityAiProvider(
            NullLogger<StabilityAiProvider>.Instance,
            new System.Net.Http.HttpClient(),
            null);
        var options = new VisualGenerationOptions();

        var result = await provider.GenerateImageAsync("Test prompt", options);

        Assert.Null(result);
    }

    [Fact]
    public async Task StabilityAiProvider_Without_ApiKey_Should_Not_Be_Available()
    {
        var provider = new StabilityAiProvider(
            NullLogger<StabilityAiProvider>.Instance,
            new System.Net.Http.HttpClient(),
            null);

        var isAvailable = await provider.IsAvailableAsync();

        Assert.False(isAvailable);
    }

    [Fact]
    public void StabilityAiProvider_Should_Require_ApiKey()
    {
        var provider = new StabilityAiProvider(
            NullLogger<StabilityAiProvider>.Instance,
            new System.Net.Http.HttpClient(),
            null);

        Assert.True(provider.RequiresApiKey);
    }

    [Fact]
    public void StabilityAiProvider_Should_Report_Pro_Tier()
    {
        var provider = new StabilityAiProvider(
            NullLogger<StabilityAiProvider>.Instance,
            new System.Net.Http.HttpClient(),
            "test-key");
        var capabilities = provider.GetProviderCapabilities();

        Assert.Equal("StabilityAI", capabilities.ProviderName);
        Assert.False(capabilities.IsFree);
        Assert.False(capabilities.IsLocal);
        Assert.True(capabilities.CostPerImage > 0);
        Assert.Equal("Pro", capabilities.Tier);
    }

    [Fact]
    public void StabilityAiProvider_Should_Support_Negative_Prompts()
    {
        var provider = new StabilityAiProvider(
            NullLogger<StabilityAiProvider>.Instance,
            new System.Net.Http.HttpClient(),
            "test-key");
        var capabilities = provider.GetProviderCapabilities();

        Assert.True(capabilities.SupportsNegativePrompts);
    }

    [Fact]
    public void StabilityAiProvider_Should_Adapt_Prompt_With_Quality_Tags()
    {
        var provider = new StabilityAiProvider(
            NullLogger<StabilityAiProvider>.Instance,
            new System.Net.Http.HttpClient(),
            "test-key");
        var options = new VisualGenerationOptions { Style = "photorealistic" };

        var adaptedPrompt = provider.AdaptPrompt("A landscape", options);

        Assert.Contains("quality", adaptedPrompt.ToLowerInvariant());
    }

    #endregion

    #region DallE3Provider Tests

    [Fact]
    public async Task DallE3Provider_Without_ApiKey_Should_Not_Generate()
    {
        var provider = new DallE3Provider(
            NullLogger<DallE3Provider>.Instance,
            new System.Net.Http.HttpClient(),
            null);
        var options = new VisualGenerationOptions();

        var result = await provider.GenerateImageAsync("Test prompt", options);

        Assert.Null(result);
    }

    [Fact]
    public void DallE3Provider_Should_Report_Pro_Tier()
    {
        var provider = new DallE3Provider(
            NullLogger<DallE3Provider>.Instance,
            new System.Net.Http.HttpClient(),
            "test-key");
        var capabilities = provider.GetProviderCapabilities();

        Assert.Equal("DALL-E 3", capabilities.ProviderName);
        Assert.False(capabilities.IsFree);
        Assert.False(capabilities.IsLocal);
        Assert.True(capabilities.CostPerImage > 0);
        Assert.Equal("Pro", capabilities.Tier);
    }

    [Fact]
    public void DallE3Provider_Should_Support_Style_Presets()
    {
        var provider = new DallE3Provider(
            NullLogger<DallE3Provider>.Instance,
            new System.Net.Http.HttpClient(),
            "test-key");
        var capabilities = provider.GetProviderCapabilities();

        Assert.True(capabilities.SupportsStylePresets);
        Assert.Contains("natural", capabilities.SupportedStyles);
        Assert.Contains("vivid", capabilities.SupportedStyles);
    }

    [Fact]
    public void DallE3Provider_Should_Truncate_Long_Prompts()
    {
        var provider = new DallE3Provider(
            NullLogger<DallE3Provider>.Instance,
            new System.Net.Http.HttpClient(),
            "test-key");
        var options = new VisualGenerationOptions();

        var longPrompt = new string('a', 1500);
        var adaptedPrompt = provider.AdaptPrompt(longPrompt, options);

        Assert.True(adaptedPrompt.Length <= 1000);
    }

    #endregion

    #region LocalStableDiffusionProvider Tests

    [Fact]
    public async Task LocalSD_Without_NvidiaGpu_Should_Not_Generate()
    {
        var provider = new LocalStableDiffusionProvider(
            NullLogger<LocalStableDiffusionProvider>.Instance,
            new System.Net.Http.HttpClient(),
            isNvidiaGpu: false);
        var options = new VisualGenerationOptions();

        var result = await provider.GenerateImageAsync("Test prompt", options);

        Assert.Null(result);
    }

    [Fact]
    public async Task LocalSD_With_Insufficient_VRAM_Should_Not_Generate()
    {
        var provider = new LocalStableDiffusionProvider(
            NullLogger<LocalStableDiffusionProvider>.Instance,
            new System.Net.Http.HttpClient(),
            isNvidiaGpu: true,
            vramGB: 4);
        var options = new VisualGenerationOptions();

        var result = await provider.GenerateImageAsync("Test prompt", options);

        Assert.Null(result);
    }

    [Fact]
    public async Task LocalSD_Should_Not_Be_Available_Without_NvidiaGpu()
    {
        var provider = new LocalStableDiffusionProvider(
            NullLogger<LocalStableDiffusionProvider>.Instance,
            new System.Net.Http.HttpClient(),
            isNvidiaGpu: false);

        var isAvailable = await provider.IsAvailableAsync();

        Assert.False(isAvailable);
    }

    [Fact]
    public void LocalSD_Should_Report_Free_Tier()
    {
        var provider = new LocalStableDiffusionProvider(
            NullLogger<LocalStableDiffusionProvider>.Instance,
            new System.Net.Http.HttpClient(),
            isNvidiaGpu: true,
            vramGB: 12);
        var capabilities = provider.GetProviderCapabilities();

        Assert.Equal("LocalSD", capabilities.ProviderName);
        Assert.True(capabilities.IsFree);
        Assert.True(capabilities.IsLocal);
        Assert.Equal(0m, capabilities.CostPerImage);
        Assert.Equal("Free", capabilities.Tier);
    }

    [Fact]
    public void LocalSD_Should_Support_Negative_Prompts()
    {
        var provider = new LocalStableDiffusionProvider(
            NullLogger<LocalStableDiffusionProvider>.Instance,
            new System.Net.Http.HttpClient(),
            isNvidiaGpu: true,
            vramGB: 12);
        var capabilities = provider.GetProviderCapabilities();

        Assert.True(capabilities.SupportsNegativePrompts);
    }

    [Fact]
    public void LocalSD_Should_Adapt_Prompt_With_Quality_Tags()
    {
        var provider = new LocalStableDiffusionProvider(
            NullLogger<LocalStableDiffusionProvider>.Instance,
            new System.Net.Http.HttpClient(),
            isNvidiaGpu: true,
            vramGB: 12);
        var options = new VisualGenerationOptions { Style = "photorealistic" };

        var adaptedPrompt = provider.AdaptPrompt("A portrait", options);

        Assert.Contains("masterpiece", adaptedPrompt.ToLowerInvariant());
    }

    #endregion

    #region BaseVisualProvider Tests

    [Fact]
    public void BaseProvider_GetCostEstimate_Should_Return_Zero_By_Default()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var options = new VisualGenerationOptions();

        var cost = provider.GetCostEstimate(options);

        Assert.Equal(0m, cost);
    }

    [Fact]
    public async Task BaseProvider_BatchGenerate_Should_Handle_Multiple_Prompts()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var prompts = new System.Collections.Generic.List<string>
        {
            "First image",
            "Second image",
            "Third image"
        };
        var options = new VisualGenerationOptions { Width = 512, Height = 512 };

        var results = await provider.BatchGenerateAsync(prompts, options);

        Assert.Equal(3, results.Count);
        foreach (var result in results)
        {
            Assert.True(File.Exists(result));
            File.Delete(result);
        }
    }

    [Fact]
    public async Task BaseProvider_BatchGenerate_Should_Report_Progress()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var prompts = new System.Collections.Generic.List<string> { "Image 1", "Image 2" };
        var options = new VisualGenerationOptions { Width = 256, Height = 256 };
        var progressReports = new System.Collections.Generic.List<BatchGenerationProgress>();

        var progress = new System.Progress<BatchGenerationProgress>(p => progressReports.Add(p));

        var results = await provider.BatchGenerateAsync(prompts, options, progress);

        Assert.Equal(2, progressReports.Count);
        Assert.Equal(1, progressReports[0].CompletedCount);
        Assert.Equal(2, progressReports[1].CompletedCount);

        foreach (var result in results)
        {
            if (File.Exists(result)) File.Delete(result);
        }
    }

    #endregion
}
