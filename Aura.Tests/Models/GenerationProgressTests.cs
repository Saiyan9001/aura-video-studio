using Aura.Core.Models;
using Xunit;

namespace Aura.Tests.Models;

/// <summary>
/// Tests for GenerationProgress model and weighted progress calculation
/// </summary>
public class GenerationProgressTests
{
    [Fact]
    public void StageWeights_ShouldSumTo100()
    {
        // Arrange & Act
        var total = StageWeights.Brief + StageWeights.Script + StageWeights.TTS +
                   StageWeights.Images + StageWeights.Rendering + StageWeights.PostProcess;

        // Assert
        Assert.Equal(100.0, total);
    }

    [Theory]
    [InlineData("Brief", 0, 0.0)]
    [InlineData("Brief", 50, 2.5)]
    [InlineData("Brief", 100, 5.0)]
    [InlineData("Script", 0, 5.0)]
    [InlineData("Script", 50, 15.0)]
    [InlineData("Script", 100, 25.0)]
    [InlineData("TTS", 0, 25.0)]
    [InlineData("TTS", 50, 40.0)]
    [InlineData("TTS", 100, 55.0)]
    [InlineData("Images", 0, 55.0)]
    [InlineData("Images", 50, 67.5)]
    [InlineData("Images", 100, 80.0)]
    [InlineData("Rendering", 0, 80.0)]
    [InlineData("Rendering", 50, 87.5)]
    [InlineData("Rendering", 100, 95.0)]
    [InlineData("PostProcess", 100, 100.0)]
    public void CalculateOverallProgress_ShouldReturnCorrectPercentage(string stage, double stagePercent, double expectedOverall)
    {
        // Act
        var actual = StageWeights.CalculateOverallProgress(stage, stagePercent);

        // Assert
        Assert.Equal(expectedOverall, actual, precision: 1);
    }

    [Fact]
    public void ProgressBuilder_CreateBriefProgress_ShouldSetCorrectStage()
    {
        // Arrange
        var message = "Validating system";

        // Act
        var progress = ProgressBuilder.CreateBriefProgress(50, message, "test-correlation");

        // Assert
        Assert.Equal("Brief", progress.Stage);
        Assert.Equal(50, progress.StagePercent);
        Assert.Equal(message, progress.Message);
        Assert.Equal("test-correlation", progress.CorrelationId);
        Assert.InRange(progress.OverallPercent, 0, 5); // Brief is 5% total
    }

    [Fact]
    public void ProgressBuilder_CreateScriptProgress_ShouldCalculateCorrectOverall()
    {
        // Arrange
        var message = "Generating script";

        // Act
        var progress = ProgressBuilder.CreateScriptProgress(50, message);

        // Assert
        Assert.Equal("Script", progress.Stage);
        Assert.Equal(50, progress.StagePercent);
        Assert.Equal(15.0, progress.OverallPercent, precision: 1); // 5 + (50% of 20)
    }

    [Fact]
    public void ProgressBuilder_CreateTtsProgress_ShouldIncludeSubstageDetail()
    {
        // Arrange
        var message = "Synthesizing audio";
        var currentScene = 3;
        var totalScenes = 5;

        // Act
        var progress = ProgressBuilder.CreateTtsProgress(60, message, currentScene, totalScenes);

        // Assert
        Assert.Equal("TTS", progress.Stage);
        Assert.Equal(60, progress.StagePercent);
        Assert.Equal("Synthesizing scene 3 of 5", progress.SubstageDetail);
        Assert.Equal(3, progress.CurrentItem);
        Assert.Equal(5, progress.TotalItems);
    }

    [Fact]
    public void ProgressBuilder_CreateImageProgress_ShouldIncludeItemTracking()
    {
        // Arrange
        var message = "Generating visuals";
        var currentImage = 2;
        var totalImages = 4;

        // Act
        var progress = ProgressBuilder.CreateImageProgress(25, message, currentImage, totalImages);

        // Assert
        Assert.Equal("Images", progress.Stage);
        Assert.Equal(25, progress.StagePercent);
        Assert.Equal("Generating image 2 of 4", progress.SubstageDetail);
        Assert.Equal(2, progress.CurrentItem);
        Assert.Equal(4, progress.TotalItems);
    }

    [Fact]
    public void ProgressBuilder_CreateRenderProgress_ShouldIncludeTimeEstimates()
    {
        // Arrange
        var message = "Encoding video";
        var elapsed = TimeSpan.FromSeconds(30);
        var remaining = TimeSpan.FromSeconds(20);

        // Act
        var progress = ProgressBuilder.CreateRenderProgress(75, message, elapsed, remaining);

        // Assert
        Assert.Equal("Rendering", progress.Stage);
        Assert.Equal(75, progress.StagePercent);
        Assert.Equal(elapsed, progress.ElapsedTime);
        Assert.Equal(remaining, progress.EstimatedTimeRemaining);
    }

    [Fact]
    public void ProgressBuilder_CreateCompleteProgress_Should100Percent()
    {
        // Act
        var progress = ProgressBuilder.CreateCompleteProgress("test-correlation");

        // Assert
        Assert.Equal("Complete", progress.Stage);
        Assert.Equal(100.0, progress.OverallPercent);
        Assert.Equal(100.0, progress.StagePercent);
        Assert.Equal("test-correlation", progress.CorrelationId);
    }

    [Fact]
    public void GenerationProgress_ShouldHaveTimestamp()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var progress = ProgressBuilder.CreateBriefProgress(0, "Starting");
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.InRange(progress.Timestamp, beforeCreate, afterCreate);
    }

    [Theory]
    [InlineData("brief", 50, 2.5)]
    [InlineData("SCRIPT", 50, 15.0)]
    [InlineData("audio", 50, 40.0)]  // TTS alias
    [InlineData("visuals", 50, 67.5)]  // Images alias
    [InlineData("render", 50, 87.5)]  // Rendering alias
    public void CalculateOverallProgress_ShouldBeCaseInsensitive(string stage, double stagePercent, double expectedOverall)
    {
        // Act
        var actual = StageWeights.CalculateOverallProgress(stage, stagePercent);

        // Assert
        Assert.Equal(expectedOverall, actual, precision: 1);
    }
}
