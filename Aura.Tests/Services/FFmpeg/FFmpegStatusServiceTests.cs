using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Services.FFmpeg;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aura.Tests.Services.FFmpeg;

public class FFmpegStatusServiceTests
{
    [Fact]
    public void FFmpegStatusService_Constructor_InitializesSuccessfully()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var resolverLogger = loggerFactory.CreateLogger<FFmpegResolver>();
        var statusLogger = loggerFactory.CreateLogger<FFmpegStatusService>();
        var cacheProvider = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        var resolver = new FFmpegResolver(resolverLogger, cacheProvider);
        var service = new FFmpegStatusService(resolver, loggerFactory, statusLogger);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatusInfo()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var resolverLogger = loggerFactory.CreateLogger<FFmpegResolver>();
        var statusLogger = loggerFactory.CreateLogger<FFmpegStatusService>();
        var cacheProvider = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        var resolver = new FFmpegResolver(resolverLogger, cacheProvider);
        var service = new FFmpegStatusService(resolver, loggerFactory, statusLogger);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.NotNull(status);
        Assert.NotNull(status.Source);
        Assert.NotNull(status.MinimumVersion);
        Assert.NotNull(status.HardwareAcceleration);
        Assert.Equal("4.0", status.MinimumVersion);
    }

    [Fact]
    public async Task GetStatusAsync_HardwareAccelerationInitialized()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var resolverLogger = loggerFactory.CreateLogger<FFmpegResolver>();
        var statusLogger = loggerFactory.CreateLogger<FFmpegStatusService>();
        var cacheProvider = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

        var resolver = new FFmpegResolver(resolverLogger, cacheProvider);
        var service = new FFmpegStatusService(resolver, loggerFactory, statusLogger);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.NotNull(status.HardwareAcceleration);
        Assert.NotNull(status.HardwareAcceleration.AvailableEncoders);
    }
}
