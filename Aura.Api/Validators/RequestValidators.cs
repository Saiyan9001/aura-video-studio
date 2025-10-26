using Aura.Api.Models.ApiModels.V1;
using FluentValidation;

namespace Aura.Api.Validators;

/// <summary>
/// Validator for ScriptRequest - ensures all required fields are present and valid
/// </summary>
public class ScriptRequestValidator : AbstractValidator<ScriptRequest>
{
    public ScriptRequestValidator()
    {
        RuleFor(x => x.Topic)
            .NotEmpty().WithMessage("Topic is required")
            .MinimumLength(5).WithMessage("Topic must be at least 5 characters")
            .MaximumLength(200).WithMessage("Topic must not exceed 200 characters");

        RuleFor(x => x.Audience)
            .NotEmpty().WithMessage("Audience is required")
            .MaximumLength(100).WithMessage("Audience must not exceed 100 characters");

        RuleFor(x => x.Goal)
            .NotEmpty().WithMessage("Goal is required")
            .MaximumLength(200).WithMessage("Goal must not exceed 200 characters");

        RuleFor(x => x.Tone)
            .NotEmpty().WithMessage("Tone is required")
            .MaximumLength(50).WithMessage("Tone must not exceed 50 characters");

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Language is required")
            .MaximumLength(50).WithMessage("Language must not exceed 50 characters");

        RuleFor(x => x.TargetDurationMinutes)
            .GreaterThan(0).WithMessage("Target duration must be greater than 0")
            .LessThanOrEqualTo(60).WithMessage("Target duration must not exceed 60 minutes");

        RuleFor(x => x.Style)
            .MaximumLength(100).WithMessage("Style must not exceed 100 characters");

        RuleFor(x => x.ProviderTier)
            .Must(tier => tier == null || 
                          tier == "Free" || 
                          tier == "Pro" || 
                          tier == "ProIfAvailable")
            .WithMessage("ProviderTier must be 'Free', 'Pro', or 'ProIfAvailable'");
    }
}

/// <summary>
/// Validator for TtsRequest - ensures valid TTS parameters
/// </summary>
public class TtsRequestValidator : AbstractValidator<TtsRequest>
{
    public TtsRequestValidator()
    {
        RuleFor(x => x.Lines)
            .NotNull().WithMessage("Lines are required")
            .NotEmpty().WithMessage("At least one line is required")
            .Must(lines => lines.Count <= 500).WithMessage("Too many lines (maximum 500)");

        RuleFor(x => x.VoiceName)
            .NotEmpty().WithMessage("Voice name is required")
            .MaximumLength(100).WithMessage("Voice name must not exceed 100 characters");

        RuleFor(x => x.Rate)
            .GreaterThanOrEqualTo(-50).WithMessage("Rate must be >= -50")
            .LessThanOrEqualTo(50).WithMessage("Rate must be <= 50");

        RuleFor(x => x.Pitch)
            .GreaterThanOrEqualTo(-50).WithMessage("Pitch must be >= -50")
            .LessThanOrEqualTo(50).WithMessage("Pitch must be <= 50");

        RuleForEach(x => x.Lines).SetValidator(new LineDtoValidator());
    }
}

/// <summary>
/// Validator for LineDto
/// </summary>
public class LineDtoValidator : AbstractValidator<LineDto>
{
    public LineDtoValidator()
    {
        RuleFor(x => x.SceneIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Scene index must be >= 0");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Line text is required")
            .MaximumLength(1000).WithMessage("Line text must not exceed 1000 characters");

        RuleFor(x => x.StartSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Start seconds must be >= 0");

        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0).WithMessage("Duration must be greater than 0")
            .LessThanOrEqualTo(60).WithMessage("Duration must not exceed 60 seconds per line");
    }
}

/// <summary>
/// Validator for RenderRequest
/// </summary>
public class RenderRequestValidator : AbstractValidator<RenderRequest>
{
    public RenderRequestValidator()
    {
        RuleFor(x => x.TimelineJson)
            .NotEmpty().WithMessage("Timeline JSON is required")
            .Must(BeValidJson).WithMessage("Timeline must be valid JSON");

        RuleFor(x => x.PresetName)
            .NotEmpty().WithMessage("Preset name is required")
            .MaximumLength(50).WithMessage("Preset name must not exceed 50 characters");

        When(x => x.Settings != null, () =>
        {
            RuleFor(x => x.Settings!).SetValidator(new RenderSettingsDtoValidator());
        });
    }

    private static bool BeValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Validator for RenderSettingsDto
/// </summary>
public class RenderSettingsDtoValidator : AbstractValidator<RenderSettingsDto>
{
    public RenderSettingsDtoValidator()
    {
        RuleFor(x => x.Width)
            .GreaterThan(0).WithMessage("Width must be greater than 0")
            .LessThanOrEqualTo(7680).WithMessage("Width must not exceed 7680 (8K)");

        RuleFor(x => x.Height)
            .GreaterThan(0).WithMessage("Height must be greater than 0")
            .LessThanOrEqualTo(4320).WithMessage("Height must not exceed 4320 (8K)");

        RuleFor(x => x.Fps)
            .GreaterThan(0).WithMessage("FPS must be greater than 0")
            .LessThanOrEqualTo(120).WithMessage("FPS must not exceed 120");

        RuleFor(x => x.Codec)
            .NotEmpty().WithMessage("Codec is required")
            .MaximumLength(50).WithMessage("Codec must not exceed 50 characters");

        RuleFor(x => x.Container)
            .NotEmpty().WithMessage("Container is required")
            .MaximumLength(10).WithMessage("Container must not exceed 10 characters");

        RuleFor(x => x.QualityLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Quality level must be >= 0")
            .LessThanOrEqualTo(100).WithMessage("Quality level must be <= 100");

        RuleFor(x => x.VideoBitrateK)
            .GreaterThan(0).WithMessage("Video bitrate must be greater than 0")
            .LessThanOrEqualTo(100000).WithMessage("Video bitrate must not exceed 100,000 kbps");

        RuleFor(x => x.AudioBitrateK)
            .GreaterThan(0).WithMessage("Audio bitrate must be greater than 0")
            .LessThanOrEqualTo(512).WithMessage("Audio bitrate must not exceed 512 kbps");
    }
}

/// <summary>
/// Validator for API Keys Request
/// </summary>
public class ApiKeysRequestValidator : AbstractValidator<ApiKeysRequest>
{
    public ApiKeysRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.OpenAiKey), () =>
        {
            RuleFor(x => x.OpenAiKey)
                .MinimumLength(20).WithMessage("OpenAI key appears to be too short")
                .Must(key => key!.StartsWith("sk-", StringComparison.Ordinal))
                .WithMessage("OpenAI key must start with 'sk-'");
        });

        When(x => !string.IsNullOrEmpty(x.PexelsKey), () =>
        {
            RuleFor(x => x.PexelsKey)
                .MinimumLength(20).WithMessage("Pexels key appears to be too short");
        });

        When(x => !string.IsNullOrEmpty(x.PixabayKey), () =>
        {
            RuleFor(x => x.PixabayKey)
                .MinimumLength(15).WithMessage("Pixabay key appears to be too short");
        });
    }
}
