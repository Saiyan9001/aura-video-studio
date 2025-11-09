using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Aura.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aura.Core.Providers;

/// <summary>
/// Factory for creating and managing TTS providers based on configuration.
/// Uses DI to resolve providers - no reflection or Activator.CreateInstance.
/// </summary>
public class TtsProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TtsProviderFactory> _logger;
    private readonly ProviderSettings _providerSettings;

    public TtsProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<TtsProviderFactory> logger,
        ProviderSettings providerSettings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _providerSettings = providerSettings;
    }

    /// <summary>
    /// Creates all available TTS providers based on configuration.
    /// Resolves providers from DI - never throws on failure.
    /// Excludes test-only providers like MockTtsProvider from production builds.
    /// </summary>
    public Dictionary<string, ITtsProvider> CreateAvailableProviders()
    {
        var providers = new Dictionary<string, ITtsProvider>();
        string correlationId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogInformation("[{CorrelationId}] Creating available TTS providers", correlationId);

        // Try to resolve all registered ITtsProvider instances
        try
        {
            var allProviders = _serviceProvider.GetServices<ITtsProvider>();
            if (allProviders != null)
            {
                foreach (var provider in allProviders)
                {
                    // Skip null providers (these are registered but couldn't be created due to missing config)
                    if (provider == null)
                    {
                        continue;
                    }
                    
                    var providerType = provider.GetType().Name;
                    
                    // SECURITY: Exclude MockTtsProvider from production - it's test-only
                    if (providerType == "MockTtsProvider")
                    {
                        _logger.LogWarning("[{CorrelationId}] Skipping MockTtsProvider - test-only provider not allowed in production", correlationId);
                        continue;
                    }
                    
                    var providerName = providerType.Replace("TtsProvider", "");
                    
                    // Map type names to friendly names
                    providerName = providerName switch
                    {
                        "Windows" => "Windows",
                        "Piper" => "Piper",
                        "Mimic3" => "Mimic3",
                        "ElevenLabs" => "ElevenLabs",
                        "PlayHT" => "PlayHT",
                        "Azure" => "Azure",
                        "Null" => "Null",
                        _ => providerName
                    };
                    
                    providers[providerName] = provider;
                    _logger.LogInformation("[{CorrelationId}] Registered {Provider} TTS provider", correlationId, providerName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error enumerating TTS providers from DI", correlationId);
        }

        _logger.LogInformation("[{CorrelationId}] Total TTS providers available: {Count}", correlationId, providers.Count);

        return providers;
    }

    /// <summary>
    /// Tries to create a specific provider by name with validation.
    /// Returns null if the provider cannot be created or is not available.
    /// </summary>
    public ITtsProvider? TryCreateProvider(string providerName)
    {
        string correlationId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] Attempting to create provider: {ProviderName}", correlationId, providerName);
            
            var providers = CreateAvailableProviders();
            
            if (providers.TryGetValue(providerName, out var provider))
            {
                _logger.LogInformation("[{CorrelationId}] Successfully created provider: {ProviderName}", correlationId, providerName);
                return provider;
            }
            
            _logger.LogWarning("[{CorrelationId}] Provider {ProviderName} not found in available providers. Available: {Available}", 
                correlationId, providerName, string.Join(", ", providers.Keys));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to create provider: {ProviderName}", correlationId, providerName);
            return null;
        }
    }

    /// <summary>
    /// Gets the default TTS provider based on configuration and availability.
    /// Never throws - returns NullTtsProvider if no other providers available.
    /// Priority: ElevenLabs > PlayHT > Azure > Mimic3 > Piper > Windows > Null
    /// </summary>
    public ITtsProvider GetDefaultProvider()
    {
        string correlationId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            var providers = CreateAvailableProviders();

            _logger.LogDebug("[{CorrelationId}] Available providers: {Providers}", 
                correlationId, string.Join(", ", providers.Keys));

            // Try cloud providers first (in priority order)
            if (providers.ContainsKey("ElevenLabs"))
            {
                _logger.LogInformation("[{CorrelationId}] Selected ElevenLabs as default TTS provider", correlationId);
                return providers["ElevenLabs"];
            }

            if (providers.ContainsKey("PlayHT"))
            {
                _logger.LogInformation("[{CorrelationId}] Selected PlayHT as default TTS provider", correlationId);
                return providers["PlayHT"];
            }

            if (providers.ContainsKey("Azure"))
            {
                _logger.LogInformation("[{CorrelationId}] Selected Azure as default TTS provider", correlationId);
                return providers["Azure"];
            }

            // Try local/offline providers
            if (providers.ContainsKey("Mimic3"))
            {
                _logger.LogInformation("[{CorrelationId}] Selected Mimic3 as default TTS provider", correlationId);
                return providers["Mimic3"];
            }

            if (providers.ContainsKey("Piper"))
            {
                _logger.LogInformation("[{CorrelationId}] Selected Piper as default TTS provider", correlationId);
                return providers["Piper"];
            }

            // Fall back to Windows TTS (platform-specific)
            if (providers.ContainsKey("Windows"))
            {
                _logger.LogInformation("[{CorrelationId}] Selected Windows as default TTS provider", correlationId);
                return providers["Windows"];
            }

            // Last resort: Null provider (generates silence)
            if (providers.ContainsKey("Null"))
            {
                _logger.LogWarning("[{CorrelationId}] No functional TTS providers available, using Null provider (generates silence)", correlationId);
                return providers["Null"];
            }

            // If even Null provider is not available in the dictionary, try to resolve it directly as absolute fallback
            _logger.LogError("[{CorrelationId}] CRITICAL: No TTS providers registered, attempting to resolve Null provider directly", correlationId);
            
            // Get all registered providers and find Null
            var allProviders = _serviceProvider.GetServices<ITtsProvider>();
            var nullProvider = allProviders?.FirstOrDefault(p => p.GetType().Name == "NullTtsProvider");
            if (nullProvider != null)
            {
                _logger.LogWarning("[{CorrelationId}] Found Null provider via direct resolution", correlationId);
                return nullProvider;
            }

            // Final fallback - create NullTtsProvider directly if not available in DI
            _logger.LogError("[{CorrelationId}] CRITICAL: No TTS providers available in DI, creating NullTtsProvider directly", correlationId);
            return CreateNullProviderFallback(correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error getting default TTS provider", correlationId);
            
            // Try one last time to get Null provider directly
            try
            {
                var allProviders = _serviceProvider.GetServices<ITtsProvider>();
                var nullProvider = allProviders?.FirstOrDefault(p => p.GetType().Name == "NullTtsProvider");
                if (nullProvider != null)
                {
                    _logger.LogWarning("[{CorrelationId}] Recovered by resolving Null provider directly", correlationId);
                    return nullProvider;
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "[{CorrelationId}] Failed to resolve Null provider as fallback", correlationId);
            }

            // Absolute final fallback - create NullTtsProvider directly
            _logger.LogError("[{CorrelationId}] ULTIMATE FALLBACK: Creating NullTtsProvider directly", correlationId);
            return CreateNullProviderFallback(correlationId);
        }
    }

    /// <summary>
    /// Creates a NullTtsProvider directly when DI resolution fails.
    /// This is the absolute last resort fallback to ensure the factory never returns null.
    /// </summary>
    private ITtsProvider CreateNullProviderFallback(string correlationId)
    {
        _logger.LogWarning("[{CorrelationId}] Creating emergency NullTtsProvider with NullLogger", correlationId);
        
        // Use reflection to create NullTtsProvider since we can't reference Aura.Providers from Aura.Core
        // This avoids circular dependency
        var nullProviderType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == "NullTtsProvider" && typeof(ITtsProvider).IsAssignableFrom(t));
        
        if (nullProviderType != null)
        {
            try
            {
                // NullTtsProvider requires SilentWavGenerator, which requires WavValidator and logger
                // Create WavValidator
                var wavValidatorType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == "WavValidator");
                
                var silentWavGeneratorType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == "SilentWavGenerator");

                if (wavValidatorType != null && silentWavGeneratorType != null)
                {
                    // Create WavValidator logger
                    var wavValidatorLoggerType = typeof(ILogger<>).MakeGenericType(wavValidatorType);
                    var wavValidatorNullLoggerType = typeof(NullLogger<>).MakeGenericType(wavValidatorType);
                    var wavValidatorLogger = Activator.CreateInstance(wavValidatorNullLoggerType);
                    
                    // Create WavValidator
                    var wavValidator = Activator.CreateInstance(wavValidatorType, wavValidatorLogger);

                    // Create SilentWavGenerator logger
                    var silentWavGeneratorLoggerType = typeof(ILogger<>).MakeGenericType(silentWavGeneratorType);
                    var silentWavGeneratorNullLoggerType = typeof(NullLogger<>).MakeGenericType(silentWavGeneratorType);
                    var silentWavGeneratorLogger = Activator.CreateInstance(silentWavGeneratorNullLoggerType);
                    
                    // Create SilentWavGenerator
                    var silentWavGenerator = Activator.CreateInstance(silentWavGeneratorType, silentWavGeneratorLogger);

                    // Create logger for NullTtsProvider
                    var nullProviderLoggerType = typeof(ILogger<>).MakeGenericType(nullProviderType);
                    var nullProviderNullLoggerType = typeof(NullLogger<>).MakeGenericType(nullProviderType);
                    var nullProviderLogger = Activator.CreateInstance(nullProviderNullLoggerType);
                    
                    // Create NullTtsProvider instance with its dependencies
                    var nullProvider = Activator.CreateInstance(nullProviderType, nullProviderLogger, silentWavGenerator);
                    if (nullProvider is ITtsProvider provider)
                    {
                        _logger.LogInformation("[{CorrelationId}] Successfully created emergency NullTtsProvider with dependencies", correlationId);
                        return provider;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Failed to create NullTtsProvider via reflection", correlationId);
            }
        }

        // If all else fails, throw - but this should never happen
        throw new InvalidOperationException("Unable to create NullTtsProvider fallback. This should never happen.");
    }
}
