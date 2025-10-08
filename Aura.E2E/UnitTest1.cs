using System;
using System.Threading.Tasks;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Core.Rendering;
using Aura.Providers.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.E2E;

/// <summary>
/// End-to-end integration tests for Aura Video Studio
/// These tests validate the complete pipeline from brief to final video
/// </summary>
public class VideoGenerationE2ETests
{
    [Fact]
    public async Task HardwareDetection_Should_DetectSystem()
    {
        // Arrange
        var detector = new HardwareDetector(NullLogger<HardwareDetector>.Instance);

        // Act
        var profile = await detector.DetectSystemAsync();

        // Assert
        Assert.NotNull(profile);
        Assert.True(profile.LogicalCores > 0, "Should detect CPU cores");
        Assert.True(profile.RamGB > 0, "Should detect RAM");
        Assert.True(Enum.IsDefined(typeof(HardwareTier), profile.Tier), "Should assign valid tier");
    }

    [Fact]
    public async Task RuleBasedLlm_Should_GenerateScript()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Introduction to Machine Learning",
            Audience: "Beginners",
            Goal: "Educate",
            Tone: "Educational",
            Language: "English",
            Aspect: Aspect.Widescreen16x9
        );
        var spec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(2),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, spec, default);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains("Machine Learning", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("##", script); // Should have scene headings
    }

    [Fact]
    public void ProviderMixing_Should_SelectCorrectProvider()
    {
        // Arrange
        var config = new ProviderMixingConfig
        {
            ActiveProfile = "Free-Only",
            AutoFallback = true,
            LogProviderSelection = false
        };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new System.Collections.Generic.Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance)
        };

        // Act
        var selection = mixer.SelectLlmProvider(providers, "Free");

        // Assert
        Assert.Equal("RuleBased", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void FFmpegPlanBuilder_Should_BuildValidCommand()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = 75,
            Fps = 30,
            EnableSceneCut = true
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.NotEmpty(command);
        Assert.Contains("-i \"input.mp4\"", command);
        Assert.Contains("-i \"audio.wav\"", command);
        Assert.Contains("-c:v libx264", command);
        Assert.Contains("-c:a aac", command);
        Assert.Contains("-r 30", command);
        Assert.Contains("output.mp4", command);
    }

    [Fact]
    public void RenderPresets_Should_ProvideStandardPresets()
    {
        // Act & Assert
        var youtube1080p = RenderPresets.YouTube1080p;
        Assert.Equal(1920, youtube1080p.Res.Width);
        Assert.Equal(1080, youtube1080p.Res.Height);
        Assert.Equal("mp4", youtube1080p.Container);

        var shorts = RenderPresets.YouTubeShorts;
        Assert.Equal(1080, shorts.Res.Width);
        Assert.Equal(1920, shorts.Res.Height); // Vertical format

        var youtube4k = RenderPresets.YouTube4K;
        Assert.Equal(3840, youtube4k.Res.Width);
        Assert.Equal(2160, youtube4k.Res.Height);
    }

    [Fact]
    public void ProviderProfiles_Should_DefineCorrectStages()
    {
        // Act & Assert
        var freeOnly = ProviderProfile.FreeOnly;
        Assert.Equal("Free", freeOnly.Stages["Script"]);
        Assert.Equal("Windows", freeOnly.Stages["TTS"]);
        Assert.Equal("Stock", freeOnly.Stages["Visuals"]);

        var balancedMix = ProviderProfile.BalancedMix;
        Assert.Equal("ProIfAvailable", balancedMix.Stages["Script"]);
        Assert.Equal("StockOrLocal", balancedMix.Stages["Visuals"]);

        var proMax = ProviderProfile.ProMax;
        Assert.Equal("Pro", proMax.Stages["Script"]);
        Assert.Equal("Pro", proMax.Stages["TTS"]);
        Assert.Equal("Pro", proMax.Stages["Visuals"]);
    }

    [Fact]
    public async Task HardwareProbes_Should_Complete()
    {
        // Arrange
        var detector = new HardwareDetector(NullLogger<HardwareDetector>.Instance);

        // Act & Assert - should not throw
        await detector.RunHardwareProbeAsync();
    }

    /// <summary>
    /// Simulates a minimal free-path video generation workflow
    /// This validates that all components work together
    /// </summary>
    [Fact]
    public async Task FreePath_Should_GenerateScriptAndSelectProviders()
    {
        // Arrange - Hardware detection
        var hardwareDetector = new HardwareDetector(NullLogger<HardwareDetector>.Instance);
        var systemProfile = await hardwareDetector.DetectSystemAsync();
        Assert.NotNull(systemProfile);

        // Arrange - Provider setup
        var llmProvider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var providerConfig = new ProviderMixingConfig
        {
            ActiveProfile = "Free-Only",
            AutoFallback = true
        };
        var providerMixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, providerConfig);

        // Arrange - Brief and spec
        var brief = new Brief(
            Topic: "Getting Started with Video Creation",
            Audience: "Content Creators",
            Goal: "Teach basics",
            Tone: "Informative",
            Language: "English",
            Aspect: Aspect.Widescreen16x9
        );
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(15), // Short test video
            Pacing: Pacing.Fast,
            Density: Density.Sparse,
            Style: "Educational"
        );

        // Act - Generate script
        var script = await llmProvider.DraftScriptAsync(brief, planSpec, default);

        // Assert - Script generation
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains("Video Creation", script, StringComparison.OrdinalIgnoreCase);

        // Act - Provider selection
        var llmProviders = new System.Collections.Generic.Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = llmProvider
        };
        var llmSelection = providerMixer.SelectLlmProvider(llmProviders, "Free");

        // Assert - Provider selection
        Assert.Equal("RuleBased", llmSelection.SelectedProvider);
        Assert.Equal("Script", llmSelection.Stage);

        // Act - Render spec
        var renderSpec = RenderPresets.YouTube1080p;
        var ffmpegBuilder = new FFmpegPlanBuilder();
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = systemProfile.Tier == HardwareTier.D ? 50 : 75,
            Fps = 30
        };

        string renderCommand = ffmpegBuilder.BuildRenderCommand(
            renderSpec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264, // Free path uses software encoder
            "placeholder_video.mp4",
            "placeholder_audio.wav",
            "output.mp4"
        );

        // Assert - Render command
        Assert.NotEmpty(renderCommand);
        Assert.Contains("libx264", renderCommand);
    }
    
    /// <summary>
    /// Tests the TTS happy path with LinuxMock provider
    /// </summary>
    [Fact]
    public async Task TtsHappyPath_Should_GenerateAudioFromScriptLines()
    {
        // Arrange
        var provider = new Aura.Providers.Tts.LinuxMockTtsProvider(
            NullLogger<Aura.Providers.Tts.LinuxMockTtsProvider>.Instance);
        
        var scriptLines = new System.Collections.Generic.List<ScriptLine>
        {
            new ScriptLine(0, "Welcome to our video tutorial.", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
            new ScriptLine(1, "Today we'll learn about video creation.", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)),
            new ScriptLine(2, "Let's get started!", TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(2))
        };
        
        var voiceSpec = new VoiceSpec(
            VoiceName: "Mock Voice (English)",
            Rate: 1.0,
            Pitch: 0,
            Pause: PauseStyle.Natural
        );
        
        // Act
        string audioPath = await provider.SynthesizeAsync(scriptLines, voiceSpec, default);
        
        // Assert
        Assert.NotNull(audioPath);
        Assert.True(System.IO.File.Exists(audioPath), "Audio file should be created");
        
        var fileInfo = new System.IO.FileInfo(audioPath);
        Assert.True(fileInfo.Length > 0, "Audio file should not be empty");
        
        // Cleanup
        if (System.IO.File.Exists(audioPath))
        {
            System.IO.File.Delete(audioPath);
        }
    }
    
    /// <summary>
    /// Tests caption generation and linking into timeline
    /// </summary>
    [Fact]
    public void CaptionsHappyPath_Should_GenerateAndLinkIntoTimeline()
    {
        // Arrange
        var timelineBuilder = new Aura.Core.Timeline.TimelineBuilder();
        var audioProcessor = new Aura.Core.Audio.AudioProcessor(
            NullLogger<Aura.Core.Audio.AudioProcessor>.Instance);
        
        var scenes = new System.Collections.Generic.List<Scene>
        {
            new Scene(0, "Introduction", "Welcome to our tutorial", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
            new Scene(1, "Main Content", "Here's the main content", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5)),
            new Scene(2, "Conclusion", "Thanks for watching", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(2))
        };
        
        // Act - Generate SRT captions
        string srtContent = timelineBuilder.GenerateSubtitles(scenes, "SRT");
        Assert.NotNull(srtContent);
        Assert.Contains("Welcome to our tutorial", srtContent);
        
        // Act - Generate VTT captions
        string vttContent = timelineBuilder.GenerateSubtitles(scenes, "VTT");
        Assert.NotNull(vttContent);
        Assert.StartsWith("WEBVTT", vttContent);
        Assert.Contains("Here's the main content", vttContent);
        
        // Act - Write captions to file
        string tempSrtPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), 
            $"test_captions_{Guid.NewGuid()}.srt");
        System.IO.File.WriteAllText(tempSrtPath, srtContent);
        
        try
        {
            // Act - Build timeline with captions
            var timeline = timelineBuilder.BuildTimeline(
                scenes,
                narrationPath: "/path/to/narration.wav",
                subtitlesPath: tempSrtPath
            );
            
            // Assert - Timeline should have captions linked
            Assert.NotNull(timeline);
            Assert.Equal(tempSrtPath, timeline.SubtitlesPath);
            Assert.Equal(3, timeline.Scenes.Count);
            
            // Assert - Subtitle filter can be built
            string subtitleFilter = audioProcessor.BuildSubtitleFilter(tempSrtPath);
            Assert.Contains("subtitles=", subtitleFilter);
            Assert.Contains(".srt", subtitleFilter);
        }
        finally
        {
            // Cleanup
            if (System.IO.File.Exists(tempSrtPath))
            {
                System.IO.File.Delete(tempSrtPath);
            }
        }
    }
    
    /// <summary>
    /// Tests provider selection with OfflineOnly mode
    /// </summary>
    [Fact]
    public void TtsProviderSelection_Should_RespectOfflineOnly()
    {
        // Arrange
        var config = new ProviderMixingConfig
        {
            ActiveProfile = "Pro",
            AutoFallback = true,
            LogProviderSelection = false
        };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        
        // Simulate offline mode - no Pro providers available
        var providers = new System.Collections.Generic.Dictionary<string, ITtsProvider>
        {
            ["Windows"] = new Aura.Providers.Tts.WindowsTtsProvider(
                NullLogger<Aura.Providers.Tts.WindowsTtsProvider>.Instance)
        };
        
        // Act
        var selection = mixer.SelectTtsProvider(providers, "Pro");
        
        // Assert - Should fallback to Windows TTS
        Assert.Equal("Windows", selection.SelectedProvider);
        Assert.True(selection.IsFallback);
        Assert.Equal("Pro TTS", selection.FallbackFrom);
    }
}
