using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Aura.App.ViewModels;

/// <summary>
/// ViewModel for the Create/Brief wizard view.
/// </summary>
public partial class CreateViewModel : ObservableObject
{
    private readonly ILogger<CreateViewModel> _logger;
    private readonly VideoOrchestrator _orchestrator;

    [ObservableProperty]
    private string _topic = string.Empty;

    [ObservableProperty]
    private string? _audience;

    [ObservableProperty]
    private string? _goal;

    [ObservableProperty]
    private string _tone = "Informative";

    [ObservableProperty]
    private string _language = "en-US";

    [ObservableProperty]
    private Aspect _aspect = Aspect.Widescreen16x9;

    [ObservableProperty]
    private int _durationMinutes = 6;

    [ObservableProperty]
    private Pacing _pacing = Pacing.Conversational;

    [ObservableProperty]
    private Density _density = Density.Balanced;

    [ObservableProperty]
    private string _style = "Educational";

    [ObservableProperty]
    private string _voiceName = "Microsoft David Desktop";

    [ObservableProperty]
    private double _voiceRate = 1.0;

    [ObservableProperty]
    private double _voicePitch = 0.0;

    [ObservableProperty]
    private PauseStyle _pauseStyle = PauseStyle.Natural;

    // Visual Configuration
    [ObservableProperty]
    private string _visualMode = "Stock"; // Stock, StockOrLocal, Pro

    [ObservableProperty]
    private int _pexelsWeight = 33;

    [ObservableProperty]
    private int _pixabayWeight = 33;

    [ObservableProperty]
    private int _unsplashWeight = 34;

    [ObservableProperty]
    private string _sdModel = "Auto"; // Auto, SDXL, SD15

    [ObservableProperty]
    private int _sdSteps = 20;

    [ObservableProperty]
    private double _sdCfgScale = 7.0;

    [ObservableProperty]
    private int _sdSeed = -1;

    [ObservableProperty]
    private string _sdSize = "Auto"; // Auto, 1024x576, 576x1024, 1024x1024

    [ObservableProperty]
    private string _visualStyle = "cinematic"; // cinematic, photographic, artistic, abstract

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = "Ready to generate";

    [ObservableProperty]
    private double _progressPercentage;

    public CreateViewModel(ILogger<CreateViewModel> logger, VideoOrchestrator orchestrator)
    {
        _logger = logger;
        _orchestrator = orchestrator;
    }

    [RelayCommand]
    private async Task GenerateVideoAsync(CancellationToken cancellationToken)
    {
        if (IsGenerating)
            return;

        try
        {
            IsGenerating = true;
            ProgressPercentage = 0;
            StatusMessage = "Starting video generation...";

            var brief = new Brief(
                Topic: Topic,
                Audience: Audience,
                Goal: Goal,
                Tone: Tone,
                Language: Language,
                Aspect: Aspect
            );

            var planSpec = new PlanSpec(
                TargetDuration: TimeSpan.FromMinutes(DurationMinutes),
                Pacing: Pacing,
                Density: Density,
                Style: Style
            );

            var voiceSpec = new VoiceSpec(
                VoiceName: VoiceName,
                Rate: VoiceRate,
                Pitch: VoicePitch,
                Pause: PauseStyle
            );

            var renderSpec = new RenderSpec(
                Res: new Resolution(1920, 1080),
                Container: "mp4",
                VideoBitrateK: 12000,
                AudioBitrateK: 256
            );

            var progress = new Progress<string>(message =>
            {
                StatusMessage = message;
                _logger.LogInformation("Progress: {Message}", message);
            });

            string outputPath = await _orchestrator.GenerateVideoAsync(
                brief,
                planSpec,
                voiceSpec,
                renderSpec,
                progress,
                cancellationToken
            );

            StatusMessage = $"Video generated successfully: {outputPath}";
            ProgressPercentage = 100;

            _logger.LogInformation("Video generation completed: {OutputPath}", outputPath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Video generation cancelled";
            _logger.LogInformation("Video generation was cancelled");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Error generating video");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void ResetForm()
    {
        Topic = string.Empty;
        Audience = null;
        Goal = null;
        Tone = "Informative";
        Language = "en-US";
        Aspect = Aspect.Widescreen16x9;
        DurationMinutes = 6;
        Pacing = Pacing.Conversational;
        Density = Density.Balanced;
        Style = "Educational";
        VoiceName = "Microsoft David Desktop";
        VoiceRate = 1.0;
        VoicePitch = 0.0;
        PauseStyle = PauseStyle.Natural;
        VisualMode = "Stock";
        PexelsWeight = 33;
        PixabayWeight = 33;
        UnsplashWeight = 34;
        SdModel = "Auto";
        SdSteps = 20;
        SdCfgScale = 7.0;
        SdSeed = -1;
        SdSize = "Auto";
        VisualStyle = "cinematic";
        StatusMessage = "Ready to generate";
        ProgressPercentage = 0;
    }
}
