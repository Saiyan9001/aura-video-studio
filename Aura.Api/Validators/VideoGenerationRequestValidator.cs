using FluentValidation;
using Aura.Api.Models.ApiModels.V1;

namespace Aura.Api.Validators;

/// <summary>
/// Validator for VideoGenerationRequest
/// </summary>
public class VideoGenerationRequestValidator : AbstractValidator<VideoGenerationRequest>
{
    public VideoGenerationRequestValidator()
    {
        RuleFor(x => x.Brief)
            .NotEmpty()
            .WithMessage("Brief is required")
            .MinimumLength(10)
            .WithMessage("Brief must be at least 10 characters")
            .MaximumLength(5000)
            .WithMessage("Brief must not exceed 5000 characters");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0)
            .WithMessage("Duration must be greater than 0")
            .LessThanOrEqualTo(10)
            .WithMessage("Duration must not exceed 10 minutes");

        RuleFor(x => x.Style)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Style))
            .WithMessage("Style must not exceed 100 characters");

        RuleFor(x => x.VoiceId)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.VoiceId))
            .WithMessage("VoiceId must not exceed 200 characters");

        When(x => x.Options != null, () =>
        {
            RuleFor(x => x.Options!.Width)
                .GreaterThanOrEqualTo(320)
                .When(x => x.Options!.Width.HasValue)
                .WithMessage("Width must be at least 320 pixels");

            RuleFor(x => x.Options!.Height)
                .GreaterThanOrEqualTo(240)
                .When(x => x.Options!.Height.HasValue)
                .WithMessage("Height must be at least 240 pixels");

            RuleFor(x => x.Options!.Fps)
                .InclusiveBetween(15, 60)
                .When(x => x.Options!.Fps.HasValue)
                .WithMessage("FPS must be between 15 and 60");

            RuleFor(x => x.Options!.Tone)
                .MaximumLength(50)
                .When(x => !string.IsNullOrEmpty(x.Options!.Tone))
                .WithMessage("Tone must not exceed 50 characters");

            RuleFor(x => x.Options!.Language)
                .MaximumLength(50)
                .When(x => !string.IsNullOrEmpty(x.Options!.Language))
                .WithMessage("Language must not exceed 50 characters");
        });
    }
}
