using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Providers.Visuals;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Integration tests for visual provider cascade and fallback behavior
/// </summary>
public class VisualProviderCascadeTests
{
    [Fact]
    public async Task Provider_Cascade_Should_Try_Each_Provider_In_Order()
    {
        var httpClient = new HttpClient();
        var providers = new List<BaseVisualProvider>
        {
            new StabilityAiProvider(
                NullLogger<StabilityAiProvider>.Instance,
                httpClient,
                null),
            new DallE3Provider(
                NullLogger<DallE3Provider>.Instance,
                httpClient,
                null),
            new LocalStableDiffusionProvider(
                NullLogger<LocalStableDiffusionProvider>.Instance,
                httpClient,
                isNvidiaGpu: false),
            new UnsplashVisualProvider(
                NullLogger<UnsplashVisualProvider>.Instance,
                httpClient,
                null),
            new PlaceholderProvider(
                NullLogger<PlaceholderProvider>.Instance)
        };

        var options = new VisualGenerationOptions { Width = 512, Height = 512 };
        string? result = null;
        string? usedProvider = null;

        foreach (var provider in providers)
        {
            var isAvailable = await provider.IsAvailableAsync(CancellationToken.None);
            if (!isAvailable)
            {
                continue;
            }

            result = await provider.GenerateImageAsync("Test cascade", options, CancellationToken.None);
            if (result != null)
            {
                usedProvider = provider.ProviderName;
                break;
            }
        }

        Assert.NotNull(result);
        Assert.Equal("Placeholder", usedProvider);

        if (System.IO.File.Exists(result))
        {
            System.IO.File.Delete(result);
        }
    }

    [Fact]
    public async Task Free_Tier_Cascade_Should_Use_Local_Then_Stock_Then_Placeholder()
    {
        var httpClient = new HttpClient();
        var freeTierProviders = new List<BaseVisualProvider>
        {
            new LocalStableDiffusionProvider(
                NullLogger<LocalStableDiffusionProvider>.Instance,
                httpClient,
                isNvidiaGpu: false),
            new UnsplashVisualProvider(
                NullLogger<UnsplashVisualProvider>.Instance,
                httpClient,
                null),
            new PlaceholderProvider(
                NullLogger<PlaceholderProvider>.Instance)
        };

        var options = new VisualGenerationOptions();
        string? result = null;

        foreach (var provider in freeTierProviders)
        {
            var isAvailable = await provider.IsAvailableAsync(CancellationToken.None);
            if (!isAvailable)
            {
                continue;
            }

            result = await provider.GenerateImageAsync("Free tier test", options, CancellationToken.None);
            if (result != null)
            {
                break;
            }
        }

        Assert.NotNull(result);

        if (System.IO.File.Exists(result))
        {
            System.IO.File.Delete(result);
        }
    }

    [Fact]
    public async Task Pro_Tier_Cascade_Should_Include_All_Providers()
    {
        var httpClient = new HttpClient();
        var proTierProviders = new List<BaseVisualProvider>
        {
            new DallE3Provider(
                NullLogger<DallE3Provider>.Instance,
                httpClient,
                null),
            new StabilityAiProvider(
                NullLogger<StabilityAiProvider>.Instance,
                httpClient,
                null),
            new MidjourneyProvider(
                NullLogger<MidjourneyProvider>.Instance,
                httpClient,
                null),
            new LocalStableDiffusionProvider(
                NullLogger<LocalStableDiffusionProvider>.Instance,
                httpClient,
                isNvidiaGpu: false),
            new UnsplashVisualProvider(
                NullLogger<UnsplashVisualProvider>.Instance,
                httpClient,
                null),
            new PlaceholderProvider(
                NullLogger<PlaceholderProvider>.Instance)
        };

        var options = new VisualGenerationOptions();
        string? result = null;

        foreach (var provider in proTierProviders)
        {
            var isAvailable = await provider.IsAvailableAsync(CancellationToken.None);
            if (!isAvailable)
            {
                continue;
            }

            result = await provider.GenerateImageAsync("Pro tier test", options, CancellationToken.None);
            if (result != null)
            {
                break;
            }
        }

        Assert.NotNull(result);

        if (System.IO.File.Exists(result))
        {
            System.IO.File.Delete(result);
        }
    }

    [Fact]
    public async Task Placeholder_Provider_Should_Always_Succeed_As_Final_Fallback()
    {
        var placeholder = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);

        var isAvailable = await placeholder.IsAvailableAsync();
        Assert.True(isAvailable);

        var options = new VisualGenerationOptions();
        var result = await placeholder.GenerateImageAsync("Guaranteed fallback", options);

        Assert.NotNull(result);
        Assert.True(System.IO.File.Exists(result));

        System.IO.File.Delete(result);
    }

    [Fact]
    public void All_Providers_Should_Report_Consistent_Aspect_Ratios()
    {
        var httpClient = new HttpClient();
        var providers = new List<BaseVisualProvider>
        {
            new StabilityAiProvider(NullLogger<StabilityAiProvider>.Instance, httpClient, null),
            new DallE3Provider(NullLogger<DallE3Provider>.Instance, httpClient, null),
            new LocalStableDiffusionProvider(NullLogger<LocalStableDiffusionProvider>.Instance, httpClient),
            new UnsplashVisualProvider(NullLogger<UnsplashVisualProvider>.Instance, httpClient, null),
            new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance)
        };

        var commonAspectRatios = new[] { "16:9", "9:16", "1:1" };

        foreach (var provider in providers)
        {
            var capabilities = provider.GetProviderCapabilities();

            foreach (var aspectRatio in commonAspectRatios)
            {
                Assert.Contains(aspectRatio, capabilities.SupportedAspectRatios);
            }
        }
    }

    [Fact]
    public void Free_Providers_Should_Have_Zero_Cost()
    {
        var httpClient = new HttpClient();
        var freeProviders = new List<BaseVisualProvider>
        {
            new LocalStableDiffusionProvider(NullLogger<LocalStableDiffusionProvider>.Instance, httpClient),
            new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance)
        };

        foreach (var provider in freeProviders)
        {
            var capabilities = provider.GetProviderCapabilities();
            Assert.True(capabilities.IsFree);
            Assert.Equal(0m, capabilities.CostPerImage);
        }
    }

    [Fact]
    public void Pro_Providers_Should_Have_Positive_Cost()
    {
        var httpClient = new HttpClient();
        var proProviders = new List<BaseVisualProvider>
        {
            new StabilityAiProvider(NullLogger<StabilityAiProvider>.Instance, httpClient, "test-key"),
            new DallE3Provider(NullLogger<DallE3Provider>.Instance, httpClient, "test-key"),
            new MidjourneyProvider(NullLogger<MidjourneyProvider>.Instance, httpClient, "test-key")
        };

        foreach (var provider in proProviders)
        {
            var capabilities = provider.GetProviderCapabilities();
            Assert.False(capabilities.IsFree);
            Assert.True(capabilities.CostPerImage > 0);
            Assert.Equal("Pro", capabilities.Tier);
        }
    }

    [Fact]
    public async Task Batch_Generation_Should_Work_With_Placeholder_Provider()
    {
        var provider = new PlaceholderProvider(NullLogger<PlaceholderProvider>.Instance);
        var prompts = new List<string> { "Image 1", "Image 2", "Image 3" };
        var options = new VisualGenerationOptions { Width = 256, Height = 256 };

        var results = await provider.BatchGenerateAsync(prompts, options);

        Assert.Equal(3, results.Count);

        foreach (var result in results)
        {
            Assert.True(System.IO.File.Exists(result));
            System.IO.File.Delete(result);
        }
    }

    [Fact]
    public void Prompt_Adaptation_Should_Enhance_Prompts()
    {
        var httpClient = new HttpClient();
        var providers = new List<BaseVisualProvider>
        {
            new StabilityAiProvider(NullLogger<StabilityAiProvider>.Instance, httpClient, "test-key"),
            new LocalStableDiffusionProvider(NullLogger<LocalStableDiffusionProvider>.Instance, httpClient)
        };

        var options = new VisualGenerationOptions { Style = "photorealistic" };
        var simplePrompt = "A landscape";

        foreach (var provider in providers)
        {
            var adaptedPrompt = provider.AdaptPrompt(simplePrompt, options);

            Assert.NotEqual(simplePrompt, adaptedPrompt);
            Assert.True(adaptedPrompt.Length >= simplePrompt.Length);
        }
    }
}
