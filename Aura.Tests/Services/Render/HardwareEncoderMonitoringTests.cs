using System;
using System.Threading.Tasks;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Services.Render;

/// <summary>
/// Tests for HardwareEncoder GPU monitoring enhancements
/// </summary>
public class HardwareEncoderMonitoringTests
{
    private readonly Mock<ILogger<HardwareEncoder>> _loggerMock;
    private readonly HardwareEncoder _encoder;

    public HardwareEncoderMonitoringTests()
    {
        _loggerMock = new Mock<ILogger<HardwareEncoder>>();
        _encoder = new HardwareEncoder(_loggerMock.Object, "ffmpeg");
    }

    [Fact]
    public async Task GetGpuMemoryInfoAsync_WithoutNvidiaSmi_ReturnsNull()
    {
        var memInfo = await _encoder.GetGpuMemoryInfoAsync();

        Assert.Null(memInfo);
    }

    [Fact]
    public async Task GetGpuUtilizationAsync_WithoutNvidiaSmi_ReturnsNull()
    {
        var utilization = await _encoder.GetGpuUtilizationAsync();

        Assert.Null(utilization);
    }

    [Fact]
    public void EstimateRequiredGpuMemory_WithTypicalValues_ReturnsReasonableEstimate()
    {
        var required = _encoder.EstimateRequiredGpuMemory(1920, 1080, 30, 60.0);

        Assert.True(required > 0);
        Assert.True(required < 1024 * 1024 * 1024);
    }

    [Fact]
    public void EstimateRequiredGpuMemory_With4K_ReturnsHigherEstimate()
    {
        var hd = _encoder.EstimateRequiredGpuMemory(1920, 1080, 30, 60.0);
        var uhd = _encoder.EstimateRequiredGpuMemory(3840, 2160, 30, 60.0);

        Assert.True(uhd > hd);
    }

    [Fact]
    public async Task HasSufficientGpuMemoryAsync_WithoutGpu_ReturnsTrue()
    {
        var sufficient = await _encoder.HasSufficientGpuMemoryAsync(1024 * 1024 * 1024);

        Assert.True(sufficient);
    }

    [Fact]
    public void GpuMemoryInfo_Properties_AreCorrect()
    {
        var memInfo = new GpuMemoryInfo(
            TotalMemoryBytes: 8L * 1024 * 1024 * 1024,
            FreeMemoryBytes: 4L * 1024 * 1024 * 1024,
            UsedMemoryBytes: 4L * 1024 * 1024 * 1024,
            UsagePercentage: 50.0,
            GpuName: "Test GPU"
        );

        Assert.Equal(8L * 1024 * 1024 * 1024, memInfo.TotalMemoryBytes);
        Assert.Equal(4L * 1024 * 1024 * 1024, memInfo.FreeMemoryBytes);
        Assert.Equal(50.0, memInfo.UsagePercentage);
        Assert.Equal("Test GPU", memInfo.GpuName);
    }

    [Fact]
    public void GpuUtilization_Properties_AreCorrect()
    {
        var utilization = new GpuUtilization(
            GpuUsagePercent: 75.0,
            MemoryUsagePercent: 60.0,
            EncoderUsagePercent: 90.0,
            DecoderUsagePercent: 20.0,
            TemperatureCelsius: 65.0
        );

        Assert.Equal(75.0, utilization.GpuUsagePercent);
        Assert.Equal(60.0, utilization.MemoryUsagePercent);
        Assert.Equal(90.0, utilization.EncoderUsagePercent);
        Assert.Equal(20.0, utilization.DecoderUsagePercent);
        Assert.Equal(65.0, utilization.TemperatureCelsius);
    }

    [Fact]
    public async Task DetectHardwareCapabilitiesAsync_IncludesGpuMemoryInfo()
    {
        var capabilities = await _encoder.DetectHardwareCapabilitiesAsync();

        Assert.NotNull(capabilities);
        Assert.NotNull(capabilities.AvailableEncoders);
    }
}
