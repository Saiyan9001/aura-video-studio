using Xunit;
using Aura.Core.Models;
using Aura.Core.Services;
using Aura.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;

namespace Aura.Tests;

/// <summary>
/// Integration tests for preflight check functionality
/// Tests both PASS and FAIL scenarios with mocks/stubs
/// </summary>
public class PreflightTests
{
    [Fact]
    public async Task PreflightChecks_ShouldCompleteSuccessfully()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.CorrelationId);
        Assert.NotEmpty(result.Checks);
        Assert.True(result.Checks.Count >= 9, "Should have at least 9 checks");
        
        // Verify all checks have required properties
        foreach (var check in result.Checks)
        {
            Assert.NotNull(check.Name);
            Assert.NotNull(check.Message);
        }
    }

    [Fact]
    public async Task PreflightResult_ShouldIncludeCorrelationId()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        Assert.NotNull(result.CorrelationId);
        Assert.Equal(8, result.CorrelationId.Length); // 8-character correlation ID
    }

    [Fact]
    public async Task PreflightChecks_ShouldIncludeProviderSelection()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        var providerCheck = result.Checks.Find(c => c.Name == "Provider Selection");
        Assert.NotNull(providerCheck);
        Assert.True(providerCheck.Ok); // Should pass with default profile
    }

    [Fact]
    public async Task PreflightChecks_ShouldIncludeApiKeys()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        var apiKeysCheck = result.Checks.Find(c => c.Name == "API Keys");
        Assert.NotNull(apiKeysCheck);
        Assert.True(apiKeysCheck.Ok); // Should pass even without keys (free providers)
    }

    [Fact]
    public async Task PreflightChecks_ShouldIncludeFfmpeg()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        var ffmpegCheck = result.Checks.Find(c => c.Name == "FFmpeg");
        Assert.NotNull(ffmpegCheck);
        // May pass or fail depending on environment
        Assert.NotNull(ffmpegCheck.Message);
    }

    [Fact]
    public async Task PreflightChecks_ShouldIncludeDiskSpace()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        var diskSpaceCheck = result.Checks.Find(c => c.Name == "Disk Space");
        Assert.NotNull(diskSpaceCheck);
        Assert.NotNull(diskSpaceCheck.Message);
    }

    [Fact]
    public async Task PreflightChecks_ShouldIncludeOfflineMode()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        var offlineCheck = result.Checks.Find(c => c.Name == "Offline Mode");
        Assert.NotNull(offlineCheck);
        Assert.True(offlineCheck.Ok); // Should pass with default settings
    }

    [Fact]
    public async Task PreflightCheck_FailedChecks_ShouldIncludeFixHints()
    {
        // Arrange
        var providerSettings = new ProviderSettings(NullLogger<ProviderSettings>.Instance);
        var preflightService = new PreflightService(
            NullLogger<PreflightService>.Instance,
            providerSettings
        );

        // Act
        var result = await preflightService.RunPreflightChecksAsync();

        // Assert
        var failedChecks = result.Checks.FindAll(c => !c.Ok);
        foreach (var check in failedChecks)
        {
            // Failed checks should have fix hints
            Assert.True(
                !string.IsNullOrWhiteSpace(check.FixHint),
                $"Failed check '{check.Name}' should have a fix hint"
            );
        }
    }

    [Fact]
    public void PreflightCheck_ModelShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var check = new PreflightCheck(
            Name: "Test Check",
            Ok: true,
            Message: "Test message",
            FixHint: "Fix hint",
            Link: "https://example.com"
        );

        // Assert
        Assert.Equal("Test Check", check.Name);
        Assert.True(check.Ok);
        Assert.Equal("Test message", check.Message);
        Assert.Equal("Fix hint", check.FixHint);
        Assert.Equal("https://example.com", check.Link);
    }

    [Fact]
    public void PreflightResult_ShouldAggregateCheckResults()
    {
        // Arrange & Act
        var checks = new List<PreflightCheck>
        {
            new("Check 1", true, "Passed"),
            new("Check 2", true, "Passed"),
            new("Check 3", false, "Failed", "Fix it", "/link")
        };
        var result = new PreflightResult(
            Ok: false,
            CorrelationId: "test1234",
            Checks: checks
        );

        // Assert
        Assert.False(result.Ok);
        Assert.Equal("test1234", result.CorrelationId);
        Assert.Equal(3, result.Checks.Count);
    }
}
