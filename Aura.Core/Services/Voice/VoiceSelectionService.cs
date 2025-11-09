using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.Voice;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Voice;

/// <summary>
/// Service for intelligent voice selection based on content type and requirements
/// </summary>
public class VoiceSelectionService
{
    private readonly ILogger<VoiceSelectionService> _logger;
    private readonly VoiceProviderRegistry _providerRegistry;

    public VoiceSelectionService(
        ILogger<VoiceSelectionService> logger,
        VoiceProviderRegistry providerRegistry)
    {
        _logger = logger;
        _providerRegistry = providerRegistry;
    }

    /// <summary>
    /// Select optimal voice based on content requirements
    /// </summary>
    public async Task<VoiceSelectionResult> SelectVoiceAsync(
        VoiceSelectionCriteria criteria,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Selecting voice for content type: {ContentType}, locale: {Locale}",
            criteria.ContentType, criteria.PreferredLocale);

        var availableVoices = await GetAvailableVoicesAsync(criteria, ct);

        if (!availableVoices.Any())
        {
            _logger.LogWarning("No voices available matching criteria");
            return new VoiceSelectionResult
            {
                IsSuccess = false,
                ErrorMessage = "No voices available matching the specified criteria",
                AvailableAlternatives = Array.Empty<VoiceDescriptor>()
            };
        }

        var scoredVoices = ScoreVoices(availableVoices, criteria);
        var topVoice = scoredVoices.First();

        _logger.LogInformation(
            "Selected voice: {VoiceName} (Provider: {Provider}, Score: {Score:F2})",
            topVoice.Voice.Name, topVoice.Voice.Provider, topVoice.Score);

        return new VoiceSelectionResult
        {
            IsSuccess = true,
            SelectedVoice = topVoice.Voice,
            SelectionScore = topVoice.Score,
            SelectionReasoning = topVoice.Reasoning,
            AvailableAlternatives = scoredVoices.Skip(1).Take(5).Select(s => s.Voice).ToArray()
        };
    }

    /// <summary>
    /// Get voice recommendations for multiple content types (e.g., dialogue)
    /// </summary>
    public async Task<MultiVoiceRecommendation> RecommendVoicesForDialogueAsync(
        DialogueVoiceRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Recommending voices for dialogue with {CharacterCount} characters",
            request.CharacterCount);

        var voiceAssignments = new List<CharacterVoiceAssignment>();

        var genderPreferences = new[] { VoiceGender.Male, VoiceGender.Female, VoiceGender.Neutral };
        var usedVoices = new HashSet<string>();

        for (int i = 0; i < request.CharacterCount; i++)
        {
            var preferredGender = request.CharacterGenderPreferences != null && 
                                 i < request.CharacterGenderPreferences.Length
                ? request.CharacterGenderPreferences[i]
                : genderPreferences[i % genderPreferences.Length];

            var criteria = new VoiceSelectionCriteria
            {
                ContentType = "dialogue",
                PreferredLocale = request.Locale,
                PreferredGender = preferredGender,
                PreferredProvider = request.PreferredProvider,
                RequiredFeatures = VoiceFeatures.Basic,
                ExcludeVoiceIds = usedVoices.ToList()
            };

            var result = await SelectVoiceAsync(criteria, ct);

            if (result.IsSuccess && result.SelectedVoice != null)
            {
                voiceAssignments.Add(new CharacterVoiceAssignment
                {
                    CharacterIndex = i,
                    CharacterName = request.CharacterNames != null && i < request.CharacterNames.Length
                        ? request.CharacterNames[i]
                        : $"Character {i + 1}",
                    AssignedVoice = result.SelectedVoice,
                    Reasoning = $"Selected for contrast and natural dialogue flow"
                });

                usedVoices.Add(result.SelectedVoice.Id);
            }
        }

        return new MultiVoiceRecommendation
        {
            VoiceAssignments = voiceAssignments,
            IsComplete = voiceAssignments.Count == request.CharacterCount,
            OverallQualityScore = voiceAssignments.Count >= request.CharacterCount ? 0.95 : 0.6
        };
    }

    private async Task<List<VoiceDescriptor>> GetAvailableVoicesAsync(
        VoiceSelectionCriteria criteria,
        CancellationToken ct)
    {
        var voices = new List<VoiceDescriptor>();

        if (criteria.PreferredProvider.HasValue)
        {
            var providerVoices = await _providerRegistry.GetVoicesForProviderAsync(
                criteria.PreferredProvider.Value, ct);
            voices.AddRange(providerVoices);
        }
        else
        {
            voices.AddRange(await _providerRegistry.GetAllAvailableVoicesAsync(ct));
        }

        if (!string.IsNullOrEmpty(criteria.PreferredLocale))
        {
            voices = voices.Where(v => v.Locale.StartsWith(criteria.PreferredLocale.Split('-')[0])).ToList();
        }

        if (criteria.PreferredGender.HasValue)
        {
            voices = voices.Where(v => v.Gender == criteria.PreferredGender.Value).ToList();
        }

        if (criteria.RequiredFeatures != VoiceFeatures.None)
        {
            voices = voices.Where(v => (v.SupportedFeatures & criteria.RequiredFeatures) == criteria.RequiredFeatures).ToList();
        }

        if (criteria.ExcludeVoiceIds != null && criteria.ExcludeVoiceIds.Any())
        {
            voices = voices.Where(v => !criteria.ExcludeVoiceIds.Contains(v.Id)).ToList();
        }

        return voices;
    }

    private List<ScoredVoice> ScoreVoices(
        List<VoiceDescriptor> voices,
        VoiceSelectionCriteria criteria)
    {
        var scoredVoices = voices.Select(voice => new ScoredVoice
        {
            Voice = voice,
            Score = CalculateVoiceScore(voice, criteria),
            Reasoning = GenerateReasoning(voice, criteria)
        }).OrderByDescending(sv => sv.Score).ToList();

        return scoredVoices;
    }

    private double CalculateVoiceScore(VoiceDescriptor voice, VoiceSelectionCriteria criteria)
    {
        double score = 50.0;

        if (voice.VoiceType == VoiceType.Neural)
        {
            score += 20.0;
        }

        if (criteria.PreferredGender.HasValue && voice.Gender == criteria.PreferredGender.Value)
        {
            score += 15.0;
        }

        if (!string.IsNullOrEmpty(criteria.PreferredLocale))
        {
            var requestedLanguage = criteria.PreferredLocale.Split('-')[0];
            var voiceLanguage = voice.Locale.Split('-')[0];
            
            if (voice.Locale == criteria.PreferredLocale)
            {
                score += 15.0;
            }
            else if (voiceLanguage == requestedLanguage)
            {
                score += 10.0;
            }
        }

        if ((voice.SupportedFeatures & VoiceFeatures.Prosody) != 0)
        {
            score += 10.0;
        }

        if ((voice.SupportedFeatures & VoiceFeatures.Styles) != 0 && voice.AvailableStyles.Length > 0)
        {
            score += 8.0;
        }

        score += criteria.ContentType?.ToLowerInvariant() switch
        {
            "educational" => voice.Gender == VoiceGender.Neutral ? 5.0 : 0.0,
            "narrative" => voice.VoiceType == VoiceType.Neural ? 10.0 : 0.0,
            "commercial" => (voice.SupportedFeatures & VoiceFeatures.Emphasis) != 0 ? 8.0 : 0.0,
            "podcast" => voice.Gender == VoiceGender.Male ? 3.0 : voice.Gender == VoiceGender.Female ? 3.0 : 0.0,
            _ => 0.0
        };

        if (criteria.PreferredProvider.HasValue && voice.Provider == criteria.PreferredProvider.Value)
        {
            score += 5.0;
        }

        return Math.Min(score, 100.0);
    }

    private string GenerateReasoning(VoiceDescriptor voice, VoiceSelectionCriteria criteria)
    {
        var reasons = new List<string>();

        if (voice.VoiceType == VoiceType.Neural)
        {
            reasons.Add("Neural voice for high quality");
        }

        if (criteria.PreferredGender.HasValue && voice.Gender == criteria.PreferredGender.Value)
        {
            reasons.Add($"Matches preferred gender ({voice.Gender})");
        }

        if (!string.IsNullOrEmpty(criteria.PreferredLocale) && voice.Locale.StartsWith(criteria.PreferredLocale))
        {
            reasons.Add($"Native {voice.Locale} speaker");
        }

        if ((voice.SupportedFeatures & VoiceFeatures.Prosody) != 0)
        {
            reasons.Add("Supports advanced prosody control");
        }

        if (voice.AvailableStyles.Length > 0)
        {
            reasons.Add($"Offers {voice.AvailableStyles.Length} speaking styles");
        }

        return reasons.Any() ? string.Join("; ", reasons) : "Standard voice selection";
    }
}

/// <summary>
/// Criteria for voice selection
/// </summary>
public record VoiceSelectionCriteria
{
    public string? ContentType { get; init; }
    public string? PreferredLocale { get; init; } = "en-US";
    public VoiceGender? PreferredGender { get; init; }
    public VoiceProvider? PreferredProvider { get; init; }
    public VoiceFeatures RequiredFeatures { get; init; } = VoiceFeatures.Basic;
    public List<string>? ExcludeVoiceIds { get; init; }
}

/// <summary>
/// Result of voice selection
/// </summary>
public record VoiceSelectionResult
{
    public bool IsSuccess { get; init; }
    public VoiceDescriptor? SelectedVoice { get; init; }
    public double SelectionScore { get; init; }
    public string? SelectionReasoning { get; init; }
    public string? ErrorMessage { get; init; }
    public VoiceDescriptor[] AvailableAlternatives { get; init; } = Array.Empty<VoiceDescriptor>();
}

/// <summary>
/// Request for dialogue voice recommendations
/// </summary>
public record DialogueVoiceRequest
{
    public int CharacterCount { get; init; }
    public string[] CharacterNames { get; init; } = Array.Empty<string>();
    public VoiceGender[] CharacterGenderPreferences { get; init; } = Array.Empty<VoiceGender>();
    public string Locale { get; init; } = "en-US";
    public VoiceProvider? PreferredProvider { get; init; }
}

/// <summary>
/// Multi-voice recommendation for dialogue
/// </summary>
public record MultiVoiceRecommendation
{
    public List<CharacterVoiceAssignment> VoiceAssignments { get; init; } = new();
    public bool IsComplete { get; init; }
    public double OverallQualityScore { get; init; }
}

/// <summary>
/// Character voice assignment
/// </summary>
public record CharacterVoiceAssignment
{
    public int CharacterIndex { get; init; }
    public string CharacterName { get; init; } = string.Empty;
    public VoiceDescriptor AssignedVoice { get; init; } = null!;
    public string Reasoning { get; init; } = string.Empty;
}

/// <summary>
/// Scored voice for ranking
/// </summary>
internal record ScoredVoice
{
    public VoiceDescriptor Voice { get; init; } = null!;
    public double Score { get; init; }
    public string Reasoning { get; init; } = string.Empty;
}
