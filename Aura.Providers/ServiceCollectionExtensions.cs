using System;
using System.Net.Http;
using Aura.Core.Configuration;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Core.Models;
using Aura.Providers.Images;
using Aura.Providers.Llm;
using Aura.Providers.Tts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Aura.Providers;

/// <summary>
/// Extension methods for registering all providers with dependency injection
/// Provides clean separation of provider registration from application startup
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all provider services including LLM, TTS, Image, Music, SFX, and Rendering providers
    /// This is the single entry point for provider registration
    /// </summary>
    public static IServiceCollection AddAuraProviders(this IServiceCollection services)
    {
        services.AddLlmProviders();
        services.AddTtsProviders();
        services.AddImageProviders();
        services.AddMusicProviders();
        services.AddSfxProviders();
        services.AddRenderingProviders();
        
        return services;
    }

    /// <summary>
    /// Registers all LLM providers with their dependencies
    /// Only registers providers if their required configuration (API keys, etc.) is available
    /// </summary>
    public static IServiceCollection AddLlmProviders(this IServiceCollection services)
    {
        services.TryAddSingleton<ProviderMixingConfig>(_ => new ProviderMixingConfig
        {
            ActiveProfile = "Free-Only",
            AutoFallback = true,
            LogProviderSelection = true
        });
        services.TryAddSingleton<ProviderMixer>();
        services.TryAddSingleton<LlmProviderFactory>();
        services.TryAddSingleton<IKeyStore, KeyStore>();
        services.TryAddSingleton<CompositeLlmProvider>();
        services.TryAddSingleton<ILlmProvider>(sp => sp.GetRequiredService<CompositeLlmProvider>());

        return services;
    }

    /// <summary>
    /// Registers all TTS providers with their dependencies
    /// Providers are registered as ITtsProvider implementations for factory enumeration
    /// </summary>
    public static IServiceCollection AddTtsProviders(this IServiceCollection services)
    {
        // Always register NullTtsProvider as final fallback
        services.AddSingleton<ITtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<NullTtsProvider>>();
            var silentWavGenerator = sp.GetRequiredService<Aura.Core.Audio.SilentWavGenerator>();
            return new NullTtsProvider(logger, silentWavGenerator);
        });

        // Register Windows TTS provider (platform-specific)
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ITtsProvider>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<WindowsTtsProvider>>();
                return new WindowsTtsProvider(logger);
            });
        }

        // Register ElevenLabs provider (requires API key)
        services.AddSingleton<ITtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ElevenLabsTtsProvider>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var apiKey = settings.GetElevenLabsApiKey();
            
            // Only create if API key is available
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogDebug("ElevenLabs API key not configured, skipping provider registration");
                return null!;
            }
            
            return new ElevenLabsTtsProvider(logger, httpClient, apiKey);
        });

        // Register PlayHT provider (requires API key and user ID)
        services.AddSingleton<ITtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PlayHTTtsProvider>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var apiKey = settings.GetPlayHTApiKey();
            var userId = settings.GetPlayHTUserId();
            
            // Only create if both API key and user ID are available
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(userId))
            {
                logger.LogDebug("PlayHT credentials not configured, skipping provider registration");
                return null!;
            }
            
            return new PlayHTTtsProvider(logger, httpClient, apiKey, userId);
        });

        // Register Azure TTS provider (requires API key and region)
        services.AddSingleton<ITtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AzureTtsProvider>>();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var apiKey = settings.GetAzureSpeechKey();
            var region = settings.GetAzureSpeechRegion();
            var offlineOnly = settings.IsOfflineOnly();
            
            // Only create if API key and region are available
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(region))
            {
                logger.LogDebug("Azure Speech credentials not configured, skipping provider registration");
                return null!;
            }
            
            return new AzureTtsProvider(logger, apiKey, region, offlineOnly);
        });

        // Register Piper provider (requires executable path)
        services.AddSingleton<ITtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PiperTtsProvider>>();
            var silentWavGenerator = sp.GetRequiredService<Aura.Core.Audio.SilentWavGenerator>();
            var wavValidator = sp.GetRequiredService<Aura.Core.Audio.WavValidator>();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var piperPath = settings.PiperExecutablePath;
            var modelPath = settings.PiperVoiceModelPath;
            
            // Only create if paths are configured
            if (string.IsNullOrWhiteSpace(piperPath) || string.IsNullOrWhiteSpace(modelPath))
            {
                logger.LogDebug("Piper TTS paths not configured, skipping provider registration");
                return null!;
            }
            
            return new PiperTtsProvider(logger, silentWavGenerator, wavValidator, piperPath, modelPath);
        });

        // Register Mimic3 provider (requires base URL)
        services.AddSingleton<ITtsProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Mimic3TtsProvider>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var silentWavGenerator = sp.GetRequiredService<Aura.Core.Audio.SilentWavGenerator>();
            var wavValidator = sp.GetRequiredService<Aura.Core.Audio.WavValidator>();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var baseUrl = settings.Mimic3BaseUrl;
            
            // Only create if base URL is configured
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                logger.LogDebug("Mimic3 base URL not configured, skipping provider registration");
                return null!;
            }
            
            return new Mimic3TtsProvider(logger, httpClient, silentWavGenerator, wavValidator, baseUrl);
        });

        return services;
    }

    /// <summary>
    /// Registers all Image providers with their dependencies
    /// Providers are registered as IImageProvider implementations for factory enumeration
    /// </summary>
    public static IServiceCollection AddImageProviders(this IServiceCollection services)
    {
        // Note: Image providers are created dynamically by ImageProviderFactory
        // based on API keys available in ProviderSettings
        // No direct registration needed here - factory handles instantiation
        
        return services;
    }

    /// <summary>
    /// Registers all Music providers with their dependencies
    /// Providers are registered as IMusicProvider implementations for enumeration
    /// </summary>
    public static IServiceCollection AddMusicProviders(this IServiceCollection services)
    {
        // Register Local Stock Music Provider (always available for local libraries)
        services.AddSingleton<Aura.Core.Providers.IMusicProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Music.LocalStockMusicProvider>>();
            var settings = sp.GetRequiredService<ProviderSettings>();
            var musicLibraryPath = System.IO.Path.Combine(settings.GetAuraDataDirectory(), "Music");
            return new Music.LocalStockMusicProvider(logger, musicLibraryPath);
        });

        return services;
    }

    /// <summary>
    /// Registers all SFX (Sound Effects) providers with their dependencies
    /// Providers are registered as ISfxProvider implementations for enumeration
    /// </summary>
    public static IServiceCollection AddSfxProviders(this IServiceCollection services)
    {
        // Register Freesound SFX Provider (requires API key)
        services.AddSingleton<Aura.Core.Providers.ISfxProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Sfx.FreesoundSfxProvider>>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var apiKey = configuration["Audio:FreesoundApiKey"];
            
            // Create provider even without API key (will return IsAvailable = false)
            return new Sfx.FreesoundSfxProvider(logger, httpClient, apiKey);
        });

        return services;
    }

    /// <summary>
    /// Registers provider factories for creating and managing provider instances
    /// </summary>
    public static IServiceCollection AddProviderFactories(this IServiceCollection services)
    {
        services.AddSingleton<Aura.Core.Orchestrator.LlmProviderFactory>();
        services.AddSingleton<TtsProviderFactory>();
        services.AddSingleton<ImageProviderFactory>();
        
        return services;
    }

    /// <summary>
    /// Registers provider health monitoring and recommendation services
    /// </summary>
    public static IServiceCollection AddProviderHealthServices(this IServiceCollection services)
    {
        services.AddSingleton<Aura.Core.Services.Providers.ProviderHealthMonitoringService>();
        services.AddSingleton<Aura.Core.Services.Providers.ProviderCostTrackingService>();
        services.AddSingleton<Aura.Core.Services.Providers.ProviderStatusService>();
        services.AddSingleton<Aura.Core.Services.Providers.ProviderCircuitBreakerService>();
        services.AddSingleton<Aura.Core.Services.Providers.ProviderFallbackService>();
        services.AddSingleton<Aura.Core.Services.Health.ProviderHealthMonitor>();
        services.AddSingleton<Aura.Core.Services.Health.ProviderHealthService>();
        
        // Ollama detection service (for background detection and caching)
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
        
        return services;
    }

    /// <summary>
    /// Registers all rendering providers with their dependencies
    /// Providers are registered with IRenderingProvider interface for selector enumeration
    /// </summary>
    public static IServiceCollection AddRenderingProviders(this IServiceCollection services)
    {
        // Register all rendering providers as IRenderingProvider
        // Priority order: FFmpegProvider (auto-detect) > NVENC > AMF > QSV > BasicFFmpeg
        
        services.AddSingleton<Rendering.IRenderingProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Rendering.FFmpegProvider>>();
            var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
            return new Rendering.FFmpegProvider(logger, ffmpegLocator);
        });

        services.AddSingleton<Rendering.IRenderingProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Rendering.FFmpegNvidiaProvider>>();
            var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
            return new Rendering.FFmpegNvidiaProvider(logger, ffmpegLocator);
        });

        services.AddSingleton<Rendering.IRenderingProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Rendering.FFmpegAmdProvider>>();
            var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
            return new Rendering.FFmpegAmdProvider(logger, ffmpegLocator);
        });

        services.AddSingleton<Rendering.IRenderingProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Rendering.FFmpegIntelProvider>>();
            var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
            return new Rendering.FFmpegIntelProvider(logger, ffmpegLocator);
        });

        services.AddSingleton<Rendering.IRenderingProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Rendering.BasicFFmpegProvider>>();
            var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
            return new Rendering.BasicFFmpegProvider(logger, ffmpegLocator);
        });

        // Register the selector service
        services.AddSingleton<Rendering.RenderingProviderSelector>();

        return services;
    }
}
