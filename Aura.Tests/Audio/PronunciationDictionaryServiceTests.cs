using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.Voice;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests.Audio;

public class PronunciationDictionaryServiceTests
{
    private readonly PronunciationDictionaryService _service;

    public PronunciationDictionaryServiceTests()
    {
        _service = new PronunciationDictionaryService(NullLogger<PronunciationDictionaryService>.Instance);
    }

    [Fact]
    public void GetPronunciation_ForDefaultTerm_ShouldReturnPronunciation()
    {
        var pronunciation = _service.GetPronunciation("AI");

        Assert.NotNull(pronunciation);
        Assert.Equal("AI", pronunciation.Term);
        Assert.Equal("A I", pronunciation.Phonetic);
    }

    [Fact]
    public void GetPronunciation_ForNonexistentTerm_ShouldReturnNull()
    {
        var pronunciation = _service.GetPronunciation("NonexistentTerm12345");

        Assert.Null(pronunciation);
    }

    [Fact]
    public async Task AddPronunciationAsync_ShouldAddNewEntry()
    {
        var term = "TestTerm";
        var phonetic = "test-term";

        await _service.AddPronunciationAsync(term, phonetic, null, PronunciationType.Custom, CancellationToken.None);

        var pronunciation = _service.GetPronunciation(term);

        Assert.NotNull(pronunciation);
        Assert.Equal(term, pronunciation.Term);
        Assert.Equal(phonetic, pronunciation.Phonetic);
        Assert.Equal(PronunciationType.Custom, pronunciation.Type);
    }

    [Fact]
    public async Task RemovePronunciationAsync_ShouldRemoveEntry()
    {
        var term = "TempTerm";
        var phonetic = "temp-term";

        await _service.AddPronunciationAsync(term, phonetic, null, PronunciationType.Custom, CancellationToken.None);
        
        var pronunciation = _service.GetPronunciation(term);
        Assert.NotNull(pronunciation);

        await _service.RemovePronunciationAsync(term, CancellationToken.None);

        pronunciation = _service.GetPronunciation(term);
        Assert.Null(pronunciation);
    }

    [Fact]
    public void GetAllPronunciations_ShouldReturnAllEntries()
    {
        var all = _service.GetAllPronunciations();

        Assert.NotNull(all);
        Assert.NotEmpty(all);
        Assert.True(all.Count >= 25);
    }

    [Fact]
    public void SearchPronunciations_WithPattern_ShouldReturnMatchingEntries()
    {
        var results = _service.SearchPronunciations("API");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Term == "API");
    }

    [Fact]
    public void SearchPronunciations_WithPartialPattern_ShouldReturnMultipleMatches()
    {
        var results = _service.SearchPronunciations("SQL");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Term.Contains("SQL"));
    }

    [Fact]
    public void GenerateSSMLWithPhonemes_ShouldIncludePhonemeTagsForKnownTerms()
    {
        var text = "The API uses JSON format";

        var ssml = _service.GenerateSSMLWithPhonemes(text);

        Assert.Contains("<phoneme", ssml);
        Assert.Contains("API", ssml);
        Assert.Contains("JSON", ssml);
    }

    [Fact]
    public void ApplyPronunciations_WithKnownTerms_ShouldProcessText()
    {
        var text = "The API and CPU are important";

        var result = _service.ApplyPronunciations(text);

        Assert.NotNull(result);
        Assert.Contains("API", result);
        Assert.Contains("CPU", result);
    }

    [Fact]
    public void GetPronunciation_IsCaseInsensitive()
    {
        var lowerCase = _service.GetPronunciation("api");
        var upperCase = _service.GetPronunciation("API");
        var mixedCase = _service.GetPronunciation("Api");

        Assert.NotNull(lowerCase);
        Assert.NotNull(upperCase);
        Assert.NotNull(mixedCase);
        Assert.Equal(lowerCase.Term, upperCase.Term);
        Assert.Equal(lowerCase.Term, mixedCase.Term);
    }
}
