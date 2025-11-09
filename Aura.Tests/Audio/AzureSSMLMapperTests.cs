using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Audio;
using Aura.Core.Services.Audio.Mappers;
using Xunit;

namespace Aura.Tests.Audio;

public class AzureSSMLMapperTests
{
    private readonly AzureSSMLMapper _mapper;

    public AzureSSMLMapperTests()
    {
        _mapper = new AzureSSMLMapper();
    }

    [Fact]
    public void GetConstraints_ShouldReturnValidConstraints()
    {
        var constraints = _mapper.GetConstraints();

        Assert.NotNull(constraints);
        Assert.NotEmpty(constraints.SupportedTags);
        Assert.Contains("speak", constraints.SupportedTags);
        Assert.Contains("prosody", constraints.SupportedTags);
        Assert.True(constraints.SupportsTimingMarkers);
    }

    [Fact]
    public void MapToSSML_WithBasicText_ShouldGenerateValidSSML()
    {
        var text = "Hello world";
        var adjustments = new ProsodyAdjustments
        {
            Rate = 1.0,
            Pitch = 0.0,
            Volume = 1.0,
            Pauses = new Dictionary<int, int>(),
            Emphasis = Array.Empty<EmphasisSpan>(),
            Iterations = 0
        };
        var voiceSpec = new VoiceSpec("en-US-JennyNeural", 1.0, 0.0, PauseStyle.Natural);

        var ssml = _mapper.MapToSSML(text, adjustments, voiceSpec);

        Assert.NotNull(ssml);
        Assert.Contains("<speak", ssml);
        Assert.Contains("</speak>", ssml);
        Assert.Contains("en-US-JennyNeural", ssml);
        Assert.Contains("Hello world", ssml);
    }

    [Fact]
    public void MapToSSML_WithProsody_ShouldIncludeProsodyTags()
    {
        var text = "This is important";
        var adjustments = new ProsodyAdjustments
        {
            Rate = 1.2,
            Pitch = 2.0,
            Volume = 1.1,
            Pauses = new Dictionary<int, int>(),
            Emphasis = Array.Empty<EmphasisSpan>(),
            Iterations = 0
        };
        var voiceSpec = new VoiceSpec("en-US-JennyNeural", 1.0, 0.0, PauseStyle.Natural);

        var ssml = _mapper.MapToSSML(text, adjustments, voiceSpec);

        Assert.Contains("<prosody", ssml);
        Assert.Contains("rate=", ssml);
        Assert.Contains("pitch=", ssml);
        Assert.Contains("volume=", ssml);
        Assert.Contains("</prosody>", ssml);
    }

    [Fact]
    public void MapToSSML_WithPauses_ShouldIncludeBreakTags()
    {
        var text = "First sentence. Second sentence.";
        var adjustments = new ProsodyAdjustments
        {
            Rate = 1.0,
            Pitch = 0.0,
            Volume = 1.0,
            Pauses = new Dictionary<int, int> { { 15, 500 } },
            Emphasis = Array.Empty<EmphasisSpan>(),
            Iterations = 0
        };
        var voiceSpec = new VoiceSpec("en-US-JennyNeural", 1.0, 0.0, PauseStyle.Natural);

        var ssml = _mapper.MapToSSML(text, adjustments, voiceSpec);

        Assert.Contains("<break time=\"500ms\"/>", ssml);
    }

    [Fact]
    public void Validate_WithValidSSML_ShouldReturnValid()
    {
        var ssml = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\">" +
                  "<voice name=\"en-US-JennyNeural\">Hello world</voice></speak>";

        var result = _mapper.Validate(ssml);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithInvalidXML_ShouldReturnInvalid()
    {
        var ssml = "<speak>Unclosed tag";

        var result = _mapper.Validate(ssml);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void AutoRepair_WithInvalidSSML_ShouldReturnValidSSML()
    {
        var ssml = "Just plain text without tags";

        var repaired = _mapper.AutoRepair(ssml);

        Assert.Contains("<speak", repaired);
        Assert.Contains("</speak>", repaired);
        Assert.Contains("Just plain text without tags", repaired);
    }

    [Fact]
    public async Task EstimateDurationAsync_WithShortText_ShouldReturnReasonableDuration()
    {
        var ssml = "<speak>This is a short sentence.</speak>";
        var voiceSpec = new VoiceSpec("en-US-JennyNeural", 1.0, 0.0, PauseStyle.Natural);

        var durationMs = await _mapper.EstimateDurationAsync(ssml, voiceSpec, CancellationToken.None);

        Assert.InRange(durationMs, 1000, 5000);
    }

    [Fact]
    public async Task EstimateDurationAsync_WithPauses_ShouldIncludePauseDuration()
    {
        var ssml = "<speak>Text<break time=\"1000ms\"/>more text</speak>";
        var voiceSpec = new VoiceSpec("en-US-JennyNeural", 1.0, 0.0, PauseStyle.Natural);

        var durationMs = await _mapper.EstimateDurationAsync(ssml, voiceSpec, CancellationToken.None);

        Assert.True(durationMs >= 1000);
    }

    [Fact]
    public async Task EstimateDurationAsync_WithFasterRate_ShouldReturnShorterDuration()
    {
        var ssml = "<speak>This is a test sentence with multiple words.</speak>";
        var normalVoiceSpec = new VoiceSpec("en-US-JennyNeural", 1.0, 0.0, PauseStyle.Natural);
        var fasterVoiceSpec = new VoiceSpec("en-US-JennyNeural", 1.5, 0.0, PauseStyle.Natural);

        var normalDuration = await _mapper.EstimateDurationAsync(ssml, normalVoiceSpec, CancellationToken.None);
        var fasterDuration = await _mapper.EstimateDurationAsync(ssml, fasterVoiceSpec, CancellationToken.None);

        Assert.True(fasterDuration < normalDuration);
    }
}
