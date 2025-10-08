using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Providers.Tts;
using Microsoft.Extensions.Logging;

namespace Aura.Providers;

/// <summary>
/// Factory for creating TTS provider instances based on configuration
/// </summary>
public class TtsProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, string> _apiKeys;
    private readonly SystemProfile _systemProfile;

    public TtsProviderFactory(
        ILoggerFactory loggerFactory,
        Dictionary<string, string> apiKeys,
        SystemProfile systemProfile)
    {
        _loggerFactory = loggerFactory;
        _apiKeys = apiKeys;
        _systemProfile = systemProfile;
    }

    /// <summary>
    /// Gets all available TTS providers based on platform and configuration
    /// </summary>
    public Dictionary<string, ITtsProvider> GetAvailableProviders()
    {
        var providers = new Dictionary<string, ITtsProvider>();

        // Windows TTS (Windows only)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            providers["Windows"] = new WindowsTtsProvider(
                _loggerFactory.CreateLogger<WindowsTtsProvider>());
        }

        // Linux Mock (non-Windows platforms or for testing)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            providers["LinuxMock"] = new LinuxMockTtsProvider(
                _loggerFactory.CreateLogger<LinuxMockTtsProvider>());
        }

        // Pro providers (if API keys are configured and not offline-only)
        if (!_systemProfile.OfflineOnly)
        {
            // ElevenLabs
            if (_apiKeys.TryGetValue("elevenlabs", out var elevenLabsKey) && 
                !string.IsNullOrWhiteSpace(elevenLabsKey))
            {
                providers["ElevenLabs"] = new ElevenLabsTtsProvider(
                    _loggerFactory.CreateLogger<ElevenLabsTtsProvider>(),
                    elevenLabsKey,
                    _systemProfile.OfflineOnly);
            }

            // PlayHT
            if (_apiKeys.TryGetValue("playht_key", out var playhtKey) && 
                _apiKeys.TryGetValue("playht_user", out var playhtUser) &&
                !string.IsNullOrWhiteSpace(playhtKey) &&
                !string.IsNullOrWhiteSpace(playhtUser))
            {
                providers["PlayHT"] = new PlayHTTtsProvider(
                    _loggerFactory.CreateLogger<PlayHTTtsProvider>(),
                    playhtKey,
                    playhtUser,
                    _systemProfile.OfflineOnly);
            }
        }

        return providers;
    }

    /// <summary>
    /// Gets a specific TTS provider by name
    /// </summary>
    public ITtsProvider? GetProvider(string providerName)
    {
        var providers = GetAvailableProviders();
        return providers.TryGetValue(providerName, out var provider) ? provider : null;
    }

    /// <summary>
    /// Gets the default TTS provider based on platform and availability
    /// </summary>
    public ITtsProvider GetDefaultProvider()
    {
        var providers = GetAvailableProviders();

        // Prefer Windows TTS if available
        if (providers.ContainsKey("Windows"))
        {
            return providers["Windows"];
        }

        // Fall back to Linux Mock
        if (providers.ContainsKey("LinuxMock"))
        {
            return providers["LinuxMock"];
        }

        throw new InvalidOperationException("No TTS providers available");
    }
}
