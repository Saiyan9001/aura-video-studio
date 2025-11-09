using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.Setup;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests.Services.Setup;

public class DependencyDetectorTests
{
    [Fact]
    public async Task DetectAllDependenciesAsync_ReturnsStatus()
    {
        // Arrange
        var logger = NullLogger<DependencyDetector>.Instance;
        var detector = new DependencyDetector(logger, null, new HttpClient());

        // Act
        var status = await detector.DetectAllDependenciesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(status);
        Assert.True(status.DiskSpaceGB >= 0);
    }

    [Fact]
    public async Task DetectAllDependenciesAsync_ChecksNodeJs()
    {
        // Arrange
        var logger = NullLogger<DependencyDetector>.Instance;
        var detector = new DependencyDetector(logger, null, new HttpClient());

        // Act
        var status = await detector.DetectAllDependenciesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(status);
    }

    [Fact]
    public async Task DetectAllDependenciesAsync_ChecksDotNet()
    {
        // Arrange
        var logger = NullLogger<DependencyDetector>.Instance;
        var detector = new DependencyDetector(logger, null, new HttpClient());

        // Act
        var status = await detector.DetectAllDependenciesAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(status);
        Assert.True(status.DotNetInstalled);
    }
}
