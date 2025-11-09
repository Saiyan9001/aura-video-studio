using Aura.Core.Models.Voice;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Voice;

/// <summary>
/// Registry for voice providers and voice validation
/// Stub implementation to satisfy dependencies
/// </summary>
public class VoiceProviderRegistry
{
    private readonly ILogger<VoiceProviderRegistry> _logger;

    public VoiceProviderRegistry(ILogger<VoiceProviderRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate if a voice name is valid for a given provider and language (accepts enum)
    /// </summary>
    public VoiceValidationResult ValidateVoice(VoiceProvider provider, string language, string voiceName)
    {
        return ValidateVoice(provider.ToString(), language, voiceName);
    }

    /// <summary>
    /// Validate if a voice name is valid for a given provider and language (string overload)
    /// </summary>
    public VoiceValidationResult ValidateVoice(string providerName, string language, string voiceName)
    {
        return new VoiceValidationResult
        {
            IsValid = true,
            MatchedVoice = new VoiceDescriptor
            {
                Id = voiceName,
                Name = voiceName,
                Provider = Enum.TryParse<VoiceProvider>(providerName, true, out var provider) 
                    ? provider 
                    : VoiceProvider.Mock,
                Locale = language,
                Gender = VoiceGender.Neutral,
                VoiceType = VoiceType.Neural,
                SupportedFeatures = VoiceFeatures.Basic
            }
        };
    }

    /// <summary>
    /// Get available voices for a provider
    /// </summary>
    public string[] GetAvailableVoices(string providerName)
    {
        return System.Array.Empty<string>();
    }

    /// <summary>
    /// Get voice descriptors for a specific provider
    /// </summary>
    public async Task<List<VoiceDescriptor>> GetVoicesForProviderAsync(VoiceProvider provider, System.Threading.CancellationToken ct)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        return new List<VoiceDescriptor>();
    }

    /// <summary>
    /// Get all available voice descriptors across all providers
    /// </summary>
    public async Task<List<VoiceDescriptor>> GetAllAvailableVoicesAsync(System.Threading.CancellationToken ct)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        return new List<VoiceDescriptor>();
    }
}

/// <summary>
/// Result of voice validation
/// </summary>
public class VoiceValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public VoiceDescriptor? MatchedVoice { get; init; }
    public VoiceDescriptor? FallbackSuggestion { get; init; }
    
    /// <summary>
    /// Legacy property for backward compatibility (maps to MatchedVoice.Name)
    /// </summary>
    public string? VoiceName => MatchedVoice?.Name;
    
    /// <summary>
    /// Legacy property for backward compatibility (maps to MatchedVoice.Gender)
    /// </summary>
    public string? Gender => MatchedVoice?.Gender.ToString();
}
