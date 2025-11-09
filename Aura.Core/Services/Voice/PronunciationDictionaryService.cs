using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Voice;

/// <summary>
/// Service for managing pronunciation dictionaries for technical terms and proper nouns
/// </summary>
public class PronunciationDictionaryService
{
    private readonly ILogger<PronunciationDictionaryService> _logger;
    private readonly Dictionary<string, PronunciationEntry> _dictionary;
    private readonly string _dictionaryPath;

    public PronunciationDictionaryService(ILogger<PronunciationDictionaryService> logger)
    {
        _logger = logger;
        _dictionary = new Dictionary<string, PronunciationEntry>(StringComparer.OrdinalIgnoreCase);
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var auraPath = Path.Combine(appDataPath, "AuraVideoStudio");
        Directory.CreateDirectory(auraPath);
        _dictionaryPath = Path.Combine(auraPath, "pronunciation-dictionary.json");
        
        LoadDefaultDictionary();
        LoadCustomDictionary();
    }

    /// <summary>
    /// Get pronunciation for a term
    /// </summary>
    public PronunciationEntry? GetPronunciation(string term)
    {
        if (_dictionary.TryGetValue(term, out var entry))
        {
            _logger.LogDebug("Found pronunciation for '{Term}': {Pronunciation}", term, entry.Phonetic);
            return entry;
        }

        return null;
    }

    /// <summary>
    /// Add or update pronunciation entry
    /// </summary>
    public async Task AddPronunciationAsync(
        string term,
        string phonetic,
        string? ipa = null,
        PronunciationType type = PronunciationType.Custom,
        CancellationToken ct = default)
    {
        var entry = new PronunciationEntry
        {
            Term = term,
            Phonetic = phonetic,
            IPA = ipa,
            Type = type,
            LastModified = DateTime.UtcNow
        };

        _dictionary[term] = entry;
        
        _logger.LogInformation("Added pronunciation for '{Term}': {Phonetic}", term, phonetic);

        await SaveCustomDictionaryAsync(ct);
    }

    /// <summary>
    /// Remove pronunciation entry
    /// </summary>
    public async Task RemovePronunciationAsync(string term, CancellationToken ct = default)
    {
        if (_dictionary.Remove(term))
        {
            _logger.LogInformation("Removed pronunciation for '{Term}'", term);
            await SaveCustomDictionaryAsync(ct);
        }
    }

    /// <summary>
    /// Get all pronunciations
    /// </summary>
    public IReadOnlyDictionary<string, PronunciationEntry> GetAllPronunciations()
    {
        return _dictionary;
    }

    /// <summary>
    /// Search for pronunciations by term pattern
    /// </summary>
    public List<PronunciationEntry> SearchPronunciations(string pattern)
    {
        return _dictionary.Values
            .Where(e => e.Term.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Term)
            .ToList();
    }

    /// <summary>
    /// Apply pronunciation hints to text
    /// </summary>
    public string ApplyPronunciations(string text)
    {
        var result = text;

        foreach (var entry in _dictionary.Values.OrderByDescending(e => e.Term.Length))
        {
            if (result.Contains(entry.Term, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Applying pronunciation for '{Term}' in text", entry.Term);
            }
        }

        return result;
    }

    /// <summary>
    /// Generate SSML with phoneme tags for terms in dictionary
    /// </summary>
    public string GenerateSSMLWithPhonemes(string text)
    {
        var result = text;

        foreach (var entry in _dictionary.Values.OrderByDescending(e => e.Term.Length))
        {
            if (!string.IsNullOrEmpty(entry.IPA) && 
                result.Contains(entry.Term, StringComparison.OrdinalIgnoreCase))
            {
                var replacement = $"<phoneme alphabet=\"ipa\" ph=\"{entry.IPA}\">{entry.Term}</phoneme>";
                result = System.Text.RegularExpressions.Regex.Replace(
                    result,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(entry.Term)}\b",
                    replacement,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    private void LoadDefaultDictionary()
    {
        var defaultEntries = new[]
        {
            new PronunciationEntry { Term = "AI", Phonetic = "A I", IPA = "eɪ aɪ", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "API", Phonetic = "A P I", IPA = "eɪ piː aɪ", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "GPU", Phonetic = "G P U", IPA = "dʒiː piː juː", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "CPU", Phonetic = "C P U", IPA = "siː piː juː", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "SQL", Phonetic = "S Q L", IPA = "ɛs kjuː ɛl", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "HTML", Phonetic = "H T M L", IPA = "eɪtʃ tiː ɛm ɛl", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "CSS", Phonetic = "C S S", IPA = "siː ɛs ɛs", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "JSON", Phonetic = "jay-son", IPA = "dʒeɪsən", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "RGB", Phonetic = "R G B", IPA = "ɑːr dʒiː biː", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "HTTP", Phonetic = "H T T P", IPA = "eɪtʃ tiː tiː piː", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "HTTPS", Phonetic = "H T T P S", IPA = "eɪtʃ tiː tiː piː ɛs", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "URL", Phonetic = "U R L", IPA = "juː ɑːr ɛl", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "GUI", Phonetic = "goo-ee", IPA = "ˈɡuːi", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "IDE", Phonetic = "I D E", IPA = "aɪ diː iː", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "SDK", Phonetic = "S D K", IPA = "ɛs diː keɪ", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "TTS", Phonetic = "T T S", IPA = "tiː tiː ɛs", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "SSML", Phonetic = "S S M L", IPA = "ɛs ɛs ɛm ɛl", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "OpenAI", Phonetic = "open A I", IPA = "ˈoʊpən eɪ aɪ", Type = PronunciationType.ProperNoun },
            new PronunciationEntry { Term = "ChatGPT", Phonetic = "chat G P T", IPA = "tʃæt dʒiː piː tiː", Type = PronunciationType.ProperNoun },
            new PronunciationEntry { Term = "GitHub", Phonetic = "git-hub", IPA = "ˈɡɪthʌb", Type = PronunciationType.ProperNoun },
            new PronunciationEntry { Term = "Linux", Phonetic = "lin-ucks", IPA = "ˈlɪnəks", Type = PronunciationType.ProperNoun },
            new PronunciationEntry { Term = "AWS", Phonetic = "A W S", IPA = "eɪ dʌbəljuː ɛs", Type = PronunciationType.Acronym },
            new PronunciationEntry { Term = "Azure", Phonetic = "azh-er", IPA = "ˈæʒər", Type = PronunciationType.ProperNoun },
            new PronunciationEntry { Term = "MySQL", Phonetic = "my S Q L", IPA = "maɪ ɛs kjuː ɛl", Type = PronunciationType.ProperNoun },
            new PronunciationEntry { Term = "PostgreSQL", Phonetic = "post-gress Q L", IPA = "ˈpoʊstɡrɛs kjuː ɛl", Type = PronunciationType.ProperNoun }
        };

        foreach (var entry in defaultEntries)
        {
            _dictionary[entry.Term] = entry;
        }

        _logger.LogInformation("Loaded {Count} default pronunciation entries", defaultEntries.Length);
    }

    private void LoadCustomDictionary()
    {
        if (!File.Exists(_dictionaryPath))
        {
            _logger.LogInformation("No custom pronunciation dictionary found");
            return;
        }

        try
        {
            var json = File.ReadAllText(_dictionaryPath);
            var entries = JsonSerializer.Deserialize<List<PronunciationEntry>>(json);

            if (entries != null)
            {
                foreach (var entry in entries.Where(e => e.Type == PronunciationType.Custom))
                {
                    _dictionary[entry.Term] = entry;
                }

                _logger.LogInformation("Loaded {Count} custom pronunciation entries", entries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load custom pronunciation dictionary");
        }
    }

    private async Task SaveCustomDictionaryAsync(CancellationToken ct)
    {
        try
        {
            var customEntries = _dictionary.Values
                .Where(e => e.Type == PronunciationType.Custom)
                .OrderBy(e => e.Term)
                .ToList();

            var json = JsonSerializer.Serialize(customEntries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_dictionaryPath, json, ct);

            _logger.LogInformation("Saved {Count} custom pronunciation entries", customEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save custom pronunciation dictionary");
        }
    }
}

/// <summary>
/// Pronunciation dictionary entry
/// </summary>
public record PronunciationEntry
{
    public required string Term { get; init; }
    public required string Phonetic { get; init; }
    public string? IPA { get; init; }
    public PronunciationType Type { get; init; }
    public DateTime LastModified { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Type of pronunciation entry
/// </summary>
public enum PronunciationType
{
    Default,
    Acronym,
    TechnicalTerm,
    ProperNoun,
    Custom
}
