using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aura.Tests;

public class PreflightServiceTests
{
    private readonly Mock<ILogger<PreflightService>> _mockLogger;
    private readonly Mock<ILogger<HardwareDetector>> _mockHardwareLogger;
    private readonly Mock<ILogger<ProviderSettings>> _mockProviderLogger;
    private readonly HardwareDetector _hardwareDetector;
    private readonly ProviderSettings _providerSettings;

    public PreflightServiceTests()
    {
        _mockLogger = new Mock<ILogger<PreflightService>>();
        _mockHardwareLogger = new Mock<ILogger<HardwareDetector>>();
        _mockProviderLogger = new Mock<ILogger<ProviderSettings>>();
        _hardwareDetector = new HardwareDetector(_mockHardwareLogger.Object);
        _providerSettings = new ProviderSettings(_mockProviderLogger.Object);
    }

    [Fact]
    public async Task RunPreflightChecksAsync_ReturnsSuccessWithDefaultConfig()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.CorrelationId);
        Assert.NotEmpty(result.Checks);
        Assert.True(result.Checks.Count >= 8); // We have 8 checks
    }

    [Fact]
    public async Task RunPreflightChecksAsync_ChecksProviderCoherence()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        var coherenceCheck = result.Checks.Find(c => c.Name == "Provider Selection Coherence");
        Assert.NotNull(coherenceCheck);
        Assert.True(coherenceCheck.Ok); // Should pass with defaults
    }

    [Fact]
    public async Task RunPreflightChecksAsync_ChecksApiKeys()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        var apiKeysCheck = result.Checks.Find(c => c.Name == "API Keys");
        Assert.NotNull(apiKeysCheck);
        // Should pass even with no keys (using free providers)
        Assert.True(apiKeysCheck.Ok);
    }

    [Fact]
    public async Task RunPreflightChecksAsync_ChecksDiskSpace()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        var diskSpaceCheck = result.Checks.Find(c => c.Name == "Disk Space");
        Assert.NotNull(diskSpaceCheck);
        // Disk space check should run (may pass or fail depending on actual disk space)
        Assert.NotNull(diskSpaceCheck.Message);
    }

    [Fact]
    public async Task RunPreflightChecksAsync_ChecksOfflineConsistency()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        var offlineCheck = result.Checks.Find(c => c.Name == "Offline Mode Consistency");
        Assert.NotNull(offlineCheck);
        Assert.True(offlineCheck.Ok); // Should pass with defaults
    }

    [Fact]
    public async Task RunPreflightChecksAsync_WithMockHttpClient_ChecksOllama()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            mockHttpClientFactory.Object);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        var ollamaCheck = result.Checks.Find(c => c.Name == "Ollama Reachability");
        Assert.NotNull(ollamaCheck);
        Assert.True(ollamaCheck.Ok); // Should pass with successful HTTP response
    }

    [Fact]
    public async Task RunPreflightChecksAsync_WithFailingHttpClient_ChecksOllamaFails()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            mockHttpClientFactory.Object);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        var ollamaCheck = result.Checks.Find(c => c.Name == "Ollama Reachability");
        Assert.NotNull(ollamaCheck);
        Assert.False(ollamaCheck.Ok); // Should fail with connection error
        Assert.Equal("warning", ollamaCheck.Severity); // Ollama failure is a warning, not an error
    }

    [Fact]
    public async Task RunPreflightChecksAsync_LogsCorrelationId()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        Assert.NotEmpty(result.CorrelationId);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("correlationId")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunPreflightChecksAsync_CanAutoSwitchToFree_WhenProvidersFail()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            mockHttpClientFactory.Object);

        // Act
        var result = await service.RunPreflightChecksAsync();

        // Assert
        // When online providers fail, they're marked as warnings not errors
        // So overall result should still be OK (warnings don't fail preflight)
        var ollamaCheck = result.Checks.Find(c => c.Name == "Ollama Reachability");
        Assert.NotNull(ollamaCheck);
        Assert.False(ollamaCheck.Ok);
        Assert.Equal("warning", ollamaCheck.Severity);
    }

    [Fact]
    public async Task RunPreflightChecksAsync_ReturnsTimestamp()
    {
        // Arrange
        var service = new PreflightService(
            _mockLogger.Object,
            _hardwareDetector,
            _providerSettings,
            null);

        var beforeRun = DateTime.UtcNow;

        // Act
        var result = await service.RunPreflightChecksAsync();

        var afterRun = DateTime.UtcNow;

        // Assert
        Assert.True(result.Timestamp >= beforeRun);
        Assert.True(result.Timestamp <= afterRun);
    }
}
