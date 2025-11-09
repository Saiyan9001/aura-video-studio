using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.TTS;

/// <summary>
/// Service for mapping voices across different TTS providers
/// Provides fallback voice selection when requested voice is unavailable
/// </summary>
public class VoiceMapingService
{
    private readonly ILogger<VoiceMapingService> _logger;
    
    private static readonly Dictionary<string, VoiceCharacteristics> VoiceProfiles = new()
    {
        // OpenAI voices
        ["alloy"] = new("alloy", "OpenAI", Gender.Neutral, Tone.Neutral, Age.Adult),
        ["echo"] = new("echo", "OpenAI", Gender.Male, Tone.Warm, Age.Adult),
        ["fable"] = new("fable", "OpenAI", Gender.Female, Tone.Expressive, Age.Young),
        ["onyx"] = new("onyx", "OpenAI", Gender.Male, Tone.Authoritative, Age.Mature),
        ["nova"] = new("nova", "OpenAI", Gender.Female, Tone.Energetic, Age.Young),
        ["shimmer"] = new("shimmer", "OpenAI", Gender.Female, Tone.Soft, Age.Adult),
        
        // ElevenLabs common voices (examples)
        ["rachel"] = new("Rachel", "ElevenLabs", Gender.Female, Tone.Calm, Age.Adult),
        ["drew"] = new("Drew", "ElevenLabs", Gender.Male, Tone.Energetic, Age.Young),
        ["clyde"] = new("Clyde", "ElevenLabs", Gender.Male, Tone.Warm, Age.Mature),
        ["paul"] = new("Paul", "ElevenLabs", Gender.Male, Tone.Authoritative, Age.Mature),
        ["domi"] = new("Domi", "ElevenLabs", Gender.Female, Tone.Confident, Age.Adult),
        ["dave"] = new("Dave", "ElevenLabs", Gender.Male, Tone.Neutral, Age.Adult),
        
        // Azure voices (examples)
        ["en-us-arianeural"] = new("AriaNeural", "Azure", Gender.Female, Tone.Friendly, Age.Adult),
        ["en-us-guyneural"] = new("GuyNeural", "Azure", Gender.Male, Tone.Professional, Age.Adult),
        ["en-us-jennyneural"] = new("JennyNeural", "Azure", Gender.Female, Tone.Warm, Age.Young),
        
        // Windows SAPI voices (examples)
        ["microsoft david desktop"] = new("Microsoft David Desktop", "Windows", Gender.Male, Tone.Neutral, Age.Adult),
        ["microsoft zira desktop"] = new("Microsoft Zira Desktop", "Windows", Gender.Female, Tone.Neutral, Age.Adult),
    };

    public VoiceMapingService(ILogger<VoiceMapingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Finds the best matching voice from available voices based on requested voice characteristics
    /// </summary>
    public string FindBestMatch(
        string requestedVoice,
        string requestedProvider,
        IReadOnlyList<string> availableVoices,
        string targetProvider)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation(
            "[{CorrelationId}] Finding best voice match for '{RequestedVoice}' from {RequestedProvider} in {TargetProvider}",
            correlationId, requestedVoice, requestedProvider, targetProvider);

        if (availableVoices.Count == 0)
        {
            _logger.LogWarning("[{CorrelationId}] No available voices in target provider", correlationId);
            return string.Empty;
        }

        var normalizedRequested = NormalizeVoiceName(requestedVoice);
        
        var exactMatch = availableVoices.FirstOrDefault(v => 
            NormalizeVoiceName(v).Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase));
        
        if (exactMatch != null)
        {
            _logger.LogInformation("[{CorrelationId}] Found exact match: {Voice}", correlationId, exactMatch);
            return exactMatch;
        }

        if (!VoiceProfiles.TryGetValue(normalizedRequested, out var requestedProfile))
        {
            _logger.LogInformation(
                "[{CorrelationId}] No voice profile for '{Voice}', using first available",
                correlationId, requestedVoice);
            return availableVoices[0];
        }

        var scoredVoices = availableVoices
            .Select(v => new
            {
                Voice = v,
                Score = CalculateSimilarityScore(requestedProfile, v, targetProvider)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var bestMatch = scoredVoices.First();
        
        _logger.LogInformation(
            "[{CorrelationId}] Best match for '{Requested}': '{Match}' (score: {Score:F2})",
            correlationId, requestedVoice, bestMatch.Voice, bestMatch.Score);

        return bestMatch.Voice;
    }

    /// <summary>
    /// Gets voice characteristics for a given voice
    /// </summary>
    public VoiceCharacteristics? GetVoiceCharacteristics(string voiceName, string provider)
    {
        var normalized = NormalizeVoiceName(voiceName);
        
        if (VoiceProfiles.TryGetValue(normalized, out var profile))
        {
            return profile;
        }

        return InferCharacteristics(voiceName, provider);
    }

    /// <summary>
    /// Registers a custom voice profile
    /// </summary>
    public void RegisterVoiceProfile(string voiceName, string provider, VoiceCharacteristics characteristics)
    {
        var key = NormalizeVoiceName(voiceName);
        VoiceProfiles[key] = characteristics with { VoiceName = voiceName, Provider = provider };
        
        _logger.LogInformation("Registered voice profile: {Voice} ({Provider})", voiceName, provider);
    }

    /// <summary>
    /// Calculates similarity score between requested voice and candidate
    /// </summary>
    private double CalculateSimilarityScore(VoiceCharacteristics requested, string candidateVoice, string targetProvider)
    {
        var normalized = NormalizeVoiceName(candidateVoice);
        
        if (!VoiceProfiles.TryGetValue(normalized, out var candidate))
        {
            candidate = InferCharacteristics(candidateVoice, targetProvider);
        }

        double score = 0.0;

        if (requested.Gender == candidate.Gender)
        {
            score += 3.0;
        }
        else if (requested.Gender == Gender.Neutral || candidate.Gender == Gender.Neutral)
        {
            score += 1.5;
        }

        if (requested.Tone == candidate.Tone)
        {
            score += 2.0;
        }

        if (requested.Age == candidate.Age)
        {
            score += 1.5;
        }
        else if (Math.Abs((int)requested.Age - (int)candidate.Age) == 1)
        {
            score += 0.75;
        }

        var nameScore = CalculateNameSimilarity(requested.VoiceName, candidateVoice);
        score += nameScore * 1.5;

        return score;
    }

    /// <summary>
    /// Calculates name similarity using simple heuristics
    /// </summary>
    private double CalculateNameSimilarity(string name1, string name2)
    {
        var norm1 = NormalizeVoiceName(name1).ToLowerInvariant();
        var norm2 = NormalizeVoiceName(name2).ToLowerInvariant();

        if (norm1 == norm2)
        {
            return 1.0;
        }

        if (norm1.Contains(norm2) || norm2.Contains(norm1))
        {
            return 0.7;
        }

        var commonLength = 0;
        var minLength = Math.Min(norm1.Length, norm2.Length);
        
        for (int i = 0; i < minLength; i++)
        {
            if (norm1[i] == norm2[i])
            {
                commonLength++;
            }
            else
            {
                break;
            }
        }

        return (double)commonLength / Math.Max(norm1.Length, norm2.Length);
    }

    /// <summary>
    /// Normalizes voice name for comparison
    /// </summary>
    private static string NormalizeVoiceName(string voiceName)
    {
        return voiceName
            .Replace("Neural", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Desktop", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Microsoft", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-", "")
            .Replace("_", "")
            .Replace(" ", "")
            .Trim()
            .ToLowerInvariant();
    }

    /// <summary>
    /// Infers voice characteristics from voice name
    /// </summary>
    private VoiceCharacteristics InferCharacteristics(string voiceName, string provider)
    {
        var lowerName = voiceName.ToLowerInvariant();
        
        var gender = lowerName switch
        {
            var n when n.Contains("female") || n.Contains("woman") || n.Contains("girl") => Gender.Female,
            var n when n.Contains("male") || n.Contains("man") || n.Contains("boy") => Gender.Male,
            _ => Gender.Neutral
        };

        var tone = lowerName switch
        {
            var n when n.Contains("warm") || n.Contains("friendly") => Tone.Warm,
            var n when n.Contains("authoritative") || n.Contains("professional") => Tone.Authoritative,
            var n when n.Contains("energetic") || n.Contains("excited") => Tone.Energetic,
            var n when n.Contains("calm") || n.Contains("soothing") => Tone.Calm,
            var n when n.Contains("confident") || n.Contains("assertive") => Tone.Confident,
            _ => Tone.Neutral
        };

        var age = lowerName switch
        {
            var n when n.Contains("young") || n.Contains("youth") => Age.Young,
            var n when n.Contains("mature") || n.Contains("senior") => Age.Mature,
            _ => Age.Adult
        };

        return new VoiceCharacteristics(voiceName, provider, gender, tone, age);
    }
}

/// <summary>
/// Voice characteristics for matching
/// </summary>
public record VoiceCharacteristics(
    string VoiceName,
    string Provider,
    Gender Gender,
    Tone Tone,
    Age Age
);

public enum Gender
{
    Male,
    Female,
    Neutral
}

public enum Tone
{
    Neutral,
    Warm,
    Authoritative,
    Energetic,
    Calm,
    Confident,
    Professional,
    Friendly,
    Expressive,
    Soft
}

public enum Age
{
    Young,
    Adult,
    Mature
}
