using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace Aura.Tests.Services.Render;

public class QualityAssuranceServiceTests
{
    private readonly Mock<ILogger<QualityAssuranceService>> _loggerMock;
    private readonly QualityAssuranceService _service;

    public QualityAssuranceServiceTests()
    {
        _loggerMock = new Mock<ILogger<QualityAssuranceService>>();
        _service = new QualityAssuranceService(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_InitializesService()
    {
        Assert.NotNull(_service);
    }

    [Fact]
    public void IsFileSizeReasonable_WithMatchingSize_ReturnsTrue()
    {
        var fileSizeBytes = 10_000_000L;
        var durationSeconds = 10.0;
        var targetBitrateKbps = 8000;

        var result = _service.IsFileSizeReasonable(
            fileSizeBytes,
            durationSeconds,
            targetBitrateKbps,
            out var message
        );

        Assert.True(result);
        Assert.Contains("reasonable", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsFileSizeReasonable_WithOversizedFile_ReturnsFalse()
    {
        var fileSizeBytes = 100_000_000L;
        var durationSeconds = 10.0;
        var targetBitrateKbps = 1000;

        var result = _service.IsFileSizeReasonable(
            fileSizeBytes,
            durationSeconds,
            targetBitrateKbps,
            out var message
        );

        Assert.False(result);
        Assert.Contains("larger", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsFileSizeReasonable_WithUndersizedFile_ReturnsFalse()
    {
        var fileSizeBytes = 100_000L;
        var durationSeconds = 100.0;
        var targetBitrateKbps = 8000;

        var result = _service.IsFileSizeReasonable(
            fileSizeBytes,
            durationSeconds,
            targetBitrateKbps,
            out var message
        );

        Assert.False(result);
        Assert.Contains("smaller", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckFileIntegrityAsync_WithNonExistentFile_ReturnsCorrupted()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".mp4");
        
        var result = await _service.CheckFileIntegrityAsync(nonExistentPath);

        Assert.True(result.IsCorrupted);
        Assert.False(result.CanPlay);
        Assert.False(result.HasValidHeader);
        Assert.False(result.HasValidFooter);
        Assert.NotEmpty(result.IntegrityIssues);
        Assert.Contains("does not exist", result.IntegrityIssues[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckFileIntegrityAsync_WithEmptyFile_ReturnsCorrupted()
    {
        var tempFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllBytes(tempFile, Array.Empty<byte>());

            var mockService = new QualityAssuranceService(_loggerMock.Object, "echo", "echo");
            var result = await mockService.CheckFileIntegrityAsync(tempFile);

            Assert.True(result.IsCorrupted);
            Assert.False(result.CanPlay);
            Assert.Contains(result.IntegrityIssues, issue => issue.Contains("empty", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task CheckFileIntegrityAsync_WithSmallFile_ReturnsCorrupted()
    {
        var tempFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllBytes(tempFile, new byte[100]);

            var mockService = new QualityAssuranceService(_loggerMock.Object, "echo", "echo");
            var result = await mockService.CheckFileIntegrityAsync(tempFile);

            Assert.True(result.IsCorrupted);
            Assert.Contains(result.IntegrityIssues, issue => issue.Contains("small", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
