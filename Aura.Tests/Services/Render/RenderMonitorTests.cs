using System;
using System.Threading.Tasks;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Services.Render;

/// <summary>
/// Tests for RenderMonitor progress tracking and error detection
/// </summary>
public class RenderMonitorTests
{
    private readonly Mock<ILogger<RenderMonitor>> _loggerMock;
    private readonly Mock<HardwareEncoder> _hardwareEncoderMock;
    private readonly RenderMonitor _monitor;

    public RenderMonitorTests()
    {
        _loggerMock = new Mock<ILogger<RenderMonitor>>();
        var hardwareLoggerMock = new Mock<ILogger<HardwareEncoder>>();
        _hardwareEncoderMock = new Mock<HardwareEncoder>(hardwareLoggerMock.Object, "ffmpeg");
        _monitor = new RenderMonitor(_loggerMock.Object, _hardwareEncoderMock.Object);
    }

    [Fact]
    public void ParseProgressLine_WithFrameInfo_UpdatesStats()
    {
        var line = "frame=  120 fps= 30 q=-1.0 Lsize=    1024kB time=00:00:04.00 bitrate=2097.2kbits/s speed=1.0x";

        _monitor.ParseProgressLine(line, 300);

        Assert.NotNull(_monitor.CurrentStats);
        Assert.Equal(120, _monitor.CurrentStats!.FramesProcessed);
        Assert.Equal(30, _monitor.CurrentStats.CurrentFps);
        Assert.Equal(300, _monitor.CurrentStats.TotalFrames);
        Assert.Equal(40.0, _monitor.CurrentStats.ProgressPercent);
    }

    [Fact]
    public void ParseProgressLine_WithInvalidData_DoesNotThrow()
    {
        var line = "invalid line with no frame info";

        var exception = Record.Exception(() => 
            _monitor.ParseProgressLine(line, 300));

        Assert.Null(exception);
    }

    [Fact]
    public void ParseErrorLine_WithErrorPattern_AddsError()
    {
        var errorLine = "Error while opening encoder for output stream";

        _monitor.ParseErrorLine(errorLine);

        Assert.Single(_monitor.Errors);
        Assert.Contains("Error while opening encoder", _monitor.Errors[0].Message);
    }

    [Fact]
    public void ParseErrorLine_WithoutErrorPattern_DoesNotAddError()
    {
        var line = "This is a normal log line";

        _monitor.ParseErrorLine(line);

        Assert.Empty(_monitor.Errors);
    }

    [Fact]
    public void HasCriticalErrors_WithNonRecoverableError_ReturnsTrue()
    {
        _monitor.ParseErrorLine("No such file or directory");

        Assert.True(_monitor.HasCriticalErrors());
    }

    [Fact]
    public void HasCriticalErrors_WithRecoverableError_ReturnsFalse()
    {
        _monitor.ParseErrorLine("Error in frame decoding");

        Assert.False(_monitor.HasCriticalErrors());
    }

    [Fact]
    public void GetHealthStatus_WithNoStats_ReturnsUnknown()
    {
        var status = _monitor.GetHealthStatus();

        Assert.Equal("Unknown", status);
    }

    [Fact]
    public void GetHealthStatus_WithCriticalError_ReturnsCritical()
    {
        _monitor.ParseProgressLine("frame=10 fps=30", 100);
        _monitor.ParseErrorLine("No such file or directory");

        var status = _monitor.GetHealthStatus();

        Assert.Equal("Critical", status);
    }

    [Fact]
    public void GetHealthStatus_WithGoodProgress_ReturnsHealthy()
    {
        _monitor.ParseProgressLine("frame=10 fps=30 speed=1.5x", 100);

        var status = _monitor.GetHealthStatus();

        Assert.Equal("Healthy", status);
    }

    [Fact]
    public void RenderStats_Properties_AreCorrect()
    {
        var stats = new RenderStats(
            CurrentFps: 30.0,
            AverageFps: 28.5,
            CurrentBitrate: 5000.0,
            Speed: 1.2,
            Elapsed: TimeSpan.FromSeconds(60),
            Estimated: TimeSpan.FromSeconds(120),
            FramesProcessed: 100,
            TotalFrames: 300,
            ProgressPercent: 33.33,
            CpuUsagePercent: 75.0,
            MemoryUsageMb: 512.0,
            GpuStats: null
        );

        Assert.Equal(30.0, stats.CurrentFps);
        Assert.Equal(100, stats.FramesProcessed);
        Assert.Equal(300, stats.TotalFrames);
        Assert.Equal(33.33, stats.ProgressPercent);
    }

    [Fact]
    public void RenderError_Properties_AreCorrect()
    {
        var timestamp = DateTime.UtcNow;
        var error = new RenderError(
            Timestamp: timestamp,
            Message: "Test error",
            Details: "Detailed error message",
            IsRecoverable: true
        );

        Assert.Equal(timestamp, error.Timestamp);
        Assert.Equal("Test error", error.Message);
        Assert.Equal("Detailed error message", error.Details);
        Assert.True(error.IsRecoverable);
    }

    [Fact]
    public void PreviewFrame_Properties_AreCorrect()
    {
        var timestamp = TimeSpan.FromSeconds(30);
        var preview = new PreviewFrame(
            FilePath: "/tmp/preview.jpg",
            Timestamp: timestamp,
            Width: 640,
            Height: 360
        );

        Assert.Equal("/tmp/preview.jpg", preview.FilePath);
        Assert.Equal(timestamp, preview.Timestamp);
        Assert.Equal(640, preview.Width);
        Assert.Equal(360, preview.Height);
    }

    [Fact]
    public async Task GeneratePreviewFrameAsync_WithInvalidInput_ReturnsNull()
    {
        var preview = await _monitor.GeneratePreviewFrameAsync(
            "/nonexistent/video.mp4",
            TimeSpan.FromSeconds(10),
            "/tmp/preview.jpg"
        );

        Assert.Null(preview);
    }
}
