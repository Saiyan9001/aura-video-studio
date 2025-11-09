using Aura.Core.Configuration;
using Aura.Core.Dependencies;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Core.Services.Providers;
using Aura.Providers.Images;
using Aura.Providers.Llm;
using Aura.Providers.Tts;
using Aura.Providers.Video;

namespace Aura.Api.Startup;

/// <summary>
/// Extension methods for registering LLM, TTS, Image, and Video provider services.
/// </summary>
public static class ProviderServicesExtensions
{
    /// <summary>
    /// Registers all provider services including LLM, TTS, Image, and Video providers.
    /// </summary>
    public static IServiceCollection AddProviderServices(this IServiceCollection services)
    {
        // HTTP client for provider communication
        services.AddHttpClient();

        // OpenAI key validation service
        services.AddSingleton<OpenAIKeyValidationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OpenAIKeyValidationService>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            return new OpenAIKeyValidationService(logger, httpClient);
        });

        // Model catalog for dynamic model discovery
        services.AddSingleton<Aura.Core.AI.Adapters.ModelCatalog>();

        // LLM providers
        services.AddSingleton<LlmProviderFactory>();
        services.AddSingleton<ILlmProvider, CompositeLlmProvider>();

        // Provider mixing configuration
        services.AddSingleton(sp =>
        {
            var config = new ProviderMixingConfig
            {
                ActiveProfile = "Free-Only",
                AutoFallback = true,
                LogProviderSelection = true
            };
            return config;
        });

        services.AddSingleton<ProviderMixer>();

        // Provider recommendation, health monitoring, and cost tracking
        services.AddSingleton<Aura.Core.Services.Providers.ProviderHealthMonitoringService>();
        services.AddSingleton<Aura.Core.Services.Providers.ProviderCircuitBreakerService>();
        services.AddSingleton<Aura.Core.Services.Providers.ProviderCostTrackingService>();
        services.AddSingleton<Aura.Core.Services.CostTracking.EnhancedCostTrackingService>();
        
        // Ollama detection service
        services.AddSingleton<Aura.Core.Services.Providers.OllamaDetectionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Providers.OllamaDetectionService>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var baseUrl = settings.GetOllamaUrl();
            return new Aura.Core.Services.Providers.OllamaDetectionService(logger, httpClient, cache, baseUrl);
        });
        
        // Stable Diffusion detection service
        services.AddSingleton<Aura.Core.Services.Providers.StableDiffusionDetectionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Providers.StableDiffusionDetectionService>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var baseUrl = settings.GetStableDiffusionUrl();
            return new Aura.Core.Services.Providers.StableDiffusionDetectionService(logger, httpClient, baseUrl);
        });
        
        // Image provider fallback service
        services.AddSingleton<Aura.Core.Services.Providers.ImageProviderFallbackService>();
        
        services.AddSingleton<Aura.Core.Services.Providers.LlmProviderRecommendationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Providers.LlmProviderRecommendationService>>();
            var healthMonitor = sp.GetRequiredService<Aura.Core.Services.Providers.ProviderHealthMonitoringService>();
            var costTracker = sp.GetRequiredService<Aura.Core.Services.Providers.ProviderCostTrackingService>();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var factory = sp.GetRequiredService<LlmProviderFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var providers = factory.CreateAvailableProviders(loggerFactory);

            return new Aura.Core.Services.Providers.LlmProviderRecommendationService(
                logger,
                healthMonitor,
                costTracker,
                settings,
                providers);
        });

        // TTS providers with safe DI resolution
        services.AddSingleton<ITtsProvider, NullTtsProvider>();

        // Register WindowsTtsProvider (platform-dependent)
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ITtsProvider, WindowsTtsProvider>();
        }

        services.AddSingleton<TtsProviderFactory>();

        // Azure TTS provider and voice discovery
        services.AddSingleton<AzureTtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AzureTtsProvider>>();
            var providerSettings = sp.GetRequiredService<ProviderSettings>();
            var apiKey = providerSettings.GetAzureSpeechKey();
            var region = providerSettings.GetAzureSpeechRegion();
            var offlineOnly = providerSettings.IsOfflineOnly();

            return new AzureTtsProvider(logger, apiKey, region, offlineOnly);
        });

        services.AddSingleton<AzureVoiceDiscovery>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AzureVoiceDiscovery>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var providerSettings = sp.GetRequiredService<ProviderSettings>();
            var apiKey = providerSettings.GetAzureSpeechKey();
            var region = providerSettings.GetAzureSpeechRegion();

            return new AzureVoiceDiscovery(logger, httpClient, region, apiKey);
        });

        // Image provider factory
        services.AddSingleton<ImageProviderFactory>();

        // Video composer
        services.AddSingleton<IVideoComposer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<FfmpegVideoComposer>>();
            var providerSettings = sp.GetRequiredService<ProviderSettings>();
            var ffmpegLocator = sp.GetRequiredService<IFfmpegLocator>();
            var configuredFfmpegPath = providerSettings.GetFfmpegPath();
            var outputDirectory = providerSettings.GetOutputDirectory();
            return new FfmpegVideoComposer(logger, ffmpegLocator, configuredFfmpegPath, outputDirectory);
        });

        // Provider retry wrapper
        services.AddSingleton<Aura.Core.Services.ProviderRetryWrapper>();

        return services;
    }
}
