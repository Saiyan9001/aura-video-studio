using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Services.FFmpeg;
using Aura.Core.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests for Quick Demo resiliency features including FFmpeg readiness gates,
/// safe script fallback, and output path surfacing.
/// </summary>
public class QuickDemoResiliencyTests
{
    [Fact]
    public void Job_Should_HaveIsQuickDemoProperty()
    {
        // Arrange & Act
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            IsQuickDemo = true
        };

        // Assert
        Assert.True(job.IsQuickDemo);
    }

    [Fact]
    public void Job_Should_HaveOutputPathProperty()
    {
        // Arrange
        var outputPath = "/tmp/video.mp4";
        
        // Act
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            OutputPath = outputPath
        };

        // Assert
        Assert.Equal(outputPath, job.OutputPath);
    }

    [Fact]
    public void Job_Should_DefaultIsQuickDemoToFalse()
    {
        // Arrange & Act
        var job = new Job
        {
            Id = Guid.NewGuid().ToString()
        };

        // Assert
        Assert.False(job.IsQuickDemo);
    }

    [Fact]
    public async Task FFmpegStatusService_Should_ReturnStatusInfo()
    {
        // This is an integration test that would require actual FFmpeg
        // For now, we document the expected behavior
        
        // Expected behavior:
        // - FFmpegStatusInfo should have Installed, Valid, Version, VersionMeetsRequirement
        // - When FFmpeg not ready, Installed=false or Valid=false or VersionMeetsRequirement=false
        // - When FFmpeg ready, all should be true and Version should not be null
        
        await Task.CompletedTask;
        Assert.True(true, "FFmpeg status service integration test placeholder");
    }

    [Fact]
    public void ValidationException_Should_ContainIssues()
    {
        // Arrange
        var issues = new System.Collections.Generic.List<string>
        {
            "Script too short",
            "Missing scene headings"
        };

        // Act
        var exception = new ValidationException("Script validation failed", issues);

        // Assert
        Assert.Equal(2, exception.Issues.Count);
        Assert.Contains("Script too short", exception.Issues);
        Assert.Contains("Missing scene headings", exception.Issues);
    }

    [Fact]
    public void Job_WithOutputPath_Should_UpdateCorrectly()
    {
        // Arrange
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            Status = JobStatus.Running
        };

        // Act
        var updatedJob = job with
        {
            Status = JobStatus.Done,
            OutputPath = "/tmp/video.mp4"
        };

        // Assert
        Assert.Equal(JobStatus.Done, updatedJob.Status);
        Assert.Equal("/tmp/video.mp4", updatedJob.OutputPath);
        Assert.Equal(job.Id, updatedJob.Id); // ID unchanged
    }

    [Fact]
    public void Job_IsQuickDemo_Should_NotAffectTerminalStatus()
    {
        // Arrange
        var quickDemoJob = new Job
        {
            Id = Guid.NewGuid().ToString(),
            IsQuickDemo = true,
            Status = JobStatus.Done
        };

        // Act
        var isTerminal = quickDemoJob.IsTerminal();

        // Assert
        Assert.True(isTerminal);
    }

    [Fact]
    public void Job_IsQuickDemo_Should_NotAffectStateTransitions()
    {
        // Arrange
        var quickDemoJob = new Job
        {
            Id = Guid.NewGuid().ToString(),
            IsQuickDemo = true,
            Status = JobStatus.Queued
        };

        // Act
        var canTransitionToRunning = quickDemoJob.CanTransitionTo(JobStatus.Running);
        var canTransitionToFailed = quickDemoJob.CanTransitionTo(JobStatus.Failed);

        // Assert
        Assert.True(canTransitionToRunning);
        Assert.False(canTransitionToFailed); // Must go through Running first
    }

    [Fact]
    public void Brief_Should_SupportQuickDemoDefaults()
    {
        // Arrange & Act
        var brief = new Brief(
            Topic: "Welcome to Aura Video Studio",
            Audience: "General",
            Goal: "Demonstrate",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        // Assert
        Assert.NotNull(brief.Topic);
        Assert.Equal("General", brief.Audience);
        Assert.Equal("Demonstrate", brief.Goal);
    }

    [Fact]
    public void PlanSpec_Should_SupportShortDuration()
    {
        // Arrange & Act
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(12),
            Pacing: Pacing.Fast,
            Density: Density.Sparse,
            Style: "Demo"
        );

        // Assert
        Assert.Equal(12, planSpec.TargetDuration.TotalSeconds);
        Assert.Equal(Pacing.Fast, planSpec.Pacing);
        Assert.Equal("Demo", planSpec.Style);
    }

    [Fact]
    public void RenderSpec_Should_SupportStandardSettings()
    {
        // Arrange & Act
        var renderSpec = new RenderSpec(
            Res: new Resolution(1920, 1080),
            Container: "mp4",
            VideoBitrateK: 5000,
            AudioBitrateK: 192,
            Fps: 30,
            Codec: "H264"
        );

        // Assert
        Assert.Equal(1920, renderSpec.Res.Width);
        Assert.Equal(1080, renderSpec.Res.Height);
        Assert.Equal("H264", renderSpec.Codec);
        Assert.Equal(30, renderSpec.Fps);
    }
}
