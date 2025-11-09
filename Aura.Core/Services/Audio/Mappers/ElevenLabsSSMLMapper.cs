using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Audio;
using Aura.Core.Models.Voice;
using EmphasisLevel = Aura.Core.Models.Voice.EmphasisLevel;

namespace Aura.Core.Services.Audio.Mappers;

/// <summary>
/// SSML mapper for ElevenLabs TTS
/// ElevenLabs has limited SSML support, so this mapper focuses on text preprocessing
/// and timing estimation with prosody approximation through API parameters
/// </summary>
public class ElevenLabsSSMLMapper : ISSMLMapper
{
    public VoiceProvider Provider => VoiceProvider.ElevenLabs;

    public ProviderSSMLConstraints GetConstraints()
    {
        return new ProviderSSMLConstraints
        {
            SupportedTags = new HashSet<string>
            {
                "speak"
            },
            SupportedProsodyAttributes = new HashSet<string>
            {
                "stability", "similarity_boost", "style", "use_speaker_boost"
            },
            RateRange = (0.25, 4.0),
            PitchRange = (0.0, 0.0),
            VolumeRange = (0.0, 1.0),
            MaxPauseDurationMs = 3000,
            SupportsTimingMarkers = false,
            MaxTextLength = 5000
        };
    }

    public string MapToSSML(
        string text,
        ProsodyAdjustments adjustments,
        VoiceSpec voiceSpec)
    {
        var processedText = ApplyPausesAsText(text, adjustments.Pauses);
        
        processedText = ApplyEmphasisAsText(processedText, adjustments.Emphasis);
        
        return processedText;
    }

    public SSMLValidationResult Validate(string ssml)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if (string.IsNullOrWhiteSpace(ssml))
        {
            errors.Add("Text cannot be empty");
        }
        
        if (ssml.Length > GetConstraints().MaxTextLength!.Value)
        {
            warnings.Add($"Text length {ssml.Length} exceeds recommended maximum {GetConstraints().MaxTextLength}");
        }
        
        return new SSMLValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            RepairSuggestions = Array.Empty<SSMLRepairSuggestion>()
        };
    }

    public string AutoRepair(string ssml)
    {
        var text = Regex.Replace(ssml, "<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        
        var maxLength = GetConstraints().MaxTextLength!.Value;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength);
            var lastSpace = text.LastIndexOf(' ');
            if (lastSpace > maxLength - 100)
            {
                text = text.Substring(0, lastSpace);
            }
        }
        
        return text.Trim();
    }

    public async Task<int> EstimateDurationAsync(
        string ssml,
        VoiceSpec voiceSpec,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;
        
        var text = Regex.Replace(ssml, @"\.\.\.", "");
        text = Regex.Replace(text, @"\s+", " ");
        
        var wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        var baseWordsPerMinute = 140.0;
        var adjustedWpm = baseWordsPerMinute * voiceSpec.Rate;
        
        var baseDurationSeconds = (wordCount / adjustedWpm) * 60.0;
        
        var ellipsisCount = Regex.Matches(text, @"\.\.\.").Count;
        var pauseDurationSeconds = ellipsisCount * 0.5;
        
        var totalDurationSeconds = baseDurationSeconds + pauseDurationSeconds;
        
        return (int)Math.Max(100, totalDurationSeconds * 1000);
    }

    private string ApplyPausesAsText(string text, IReadOnlyDictionary<int, int>? pauses)
    {
        if (pauses == null || !pauses.Any())
        {
            return text;
        }
        
        var result = text;
        var sortedPauses = pauses.OrderByDescending(kvp => kvp.Key).ToList();
        
        foreach (var pause in sortedPauses)
        {
            if (pause.Key >= 0 && pause.Key <= result.Length)
            {
                var pauseSymbol = pause.Value switch
                {
                    >= 1000 => "... ",
                    >= 500 => ".. ",
                    _ => ", "
                };
                
                result = result.Insert(pause.Key, pauseSymbol);
            }
        }
        
        return result;
    }

    private string ApplyEmphasisAsText(string text, IReadOnlyList<EmphasisSpan>? emphasis)
    {
        if (emphasis == null || !emphasis.Any())
        {
            return text;
        }
        
        var result = text;
        var sortedEmphasis = emphasis.OrderByDescending(e => e.StartPosition).ToList();
        
        foreach (var emph in sortedEmphasis)
        {
            if (emph.StartPosition >= 0 && 
                emph.StartPosition + emph.Length <= result.Length)
            {
                var before = result.Substring(0, emph.StartPosition);
                var emphText = result.Substring(emph.StartPosition, emph.Length);
                var after = result.Substring(emph.StartPosition + emph.Length);
                
                var marker = emph.Level == EmphasisLevel.Strong ? "!" : "";
                result = $"{before}{emphText.ToUpperInvariant()}{marker}{after}";
            }
        }
        
        return result;
    }
}
