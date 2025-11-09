using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aura.Tests;

public class OpenAIKeyValidationServiceTests : IDisposable
{
    private readonly Mock<ILogger<OpenAIKeyValidationService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly OpenAIKeyValidationService _service;

    public OpenAIKeyValidationServiceTests()
    {
        _mockLogger = new Mock<ILogger<OpenAIKeyValidationService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _service = new OpenAIKeyValidationService(_mockLogger.Object, _httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ValidateKeyFormat_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";

        // Act
        var (isValid, message) = _service.ValidateKeyFormat(validKey);

        // Assert
        Assert.True(isValid);
        Assert.Contains("Format looks correct", message);
    }

    [Fact]
    public void ValidateKeyFormat_WithValidProjKey_ReturnsTrue()
    {
        // Arrange
        var validKey = "sk-proj-1234567890abcdefghijklmnopqrstuvwxyz";

        // Act
        var (isValid, message) = _service.ValidateKeyFormat(validKey);

        // Assert
        Assert.True(isValid);
        Assert.Contains("Format looks correct", message);
    }

    [Fact]
    public void ValidateKeyFormat_WithEmptyKey_ReturnsFalse()
    {
        // Arrange
        var emptyKey = "";

        // Act
        var (isValid, message) = _service.ValidateKeyFormat(emptyKey);

        // Assert
        Assert.False(isValid);
        Assert.Equal("API key is required", message);
    }

    [Fact]
    public void ValidateKeyFormat_WithInvalidPrefix_ReturnsFalse()
    {
        // Arrange
        var invalidKey = "invalid-key-format-1234567890";

        // Act
        var (isValid, message) = _service.ValidateKeyFormat(invalidKey);

        // Assert
        Assert.False(isValid);
        Assert.Contains("Invalid OpenAI API key format", message);
    }

    [Fact]
    public void ValidateKeyFormat_WithTooShortKey_ReturnsFalse()
    {
        // Arrange
        var shortKey = "sk-short";

        // Act
        var (isValid, message) = _service.ValidateKeyFormat(shortKey);

        // Assert
        Assert.False(isValid);
        Assert.Contains("at least 20 characters", message);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithValidKey_ReturnsValid()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/v1/models")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\": []}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(validKey);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Valid", result.Status);
        Assert.Contains("valid and verified", result.Message);
        Assert.True(result.FormatValid);
        Assert.True(result.NetworkCheckPassed);
        Assert.Equal(200, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithUnauthorizedKey_ReturnsUnauthorized()
    {
        // Arrange
        var invalidKey = "sk-invalidkeyinvalidkeyinvalidkey";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\": {\"message\": \"Invalid authentication\"}}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(invalidKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid", result.Status);
        Assert.Contains("Invalid authentication", result.Message);
        Assert.True(result.FormatValid);
        Assert.True(result.NetworkCheckPassed);
        Assert.Equal(401, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithForbiddenKey_ReturnsPermissionDenied()
    {
        // Arrange
        var restrictedKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Forbidden,
                Content = new StringContent("{\"error\": {\"message\": \"Project not found\"}}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(restrictedKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("PermissionDenied", result.Status);
        Assert.Contains("Project not found", result.Message);
        Assert.True(result.FormatValid);
        Assert.True(result.NetworkCheckPassed);
        Assert.Equal(403, result.HttpStatusCode);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithRateLimitedKey_ReturnsRateLimitedButValid()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Content = new StringContent("{\"error\": {\"message\": \"Rate limit exceeded\"}}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(validKey);

        // Assert
        Assert.True(result.IsValid); // Key is valid, just rate limited
        Assert.Equal("RateLimited", result.Status);
        Assert.Contains("Rate limit", result.Message);
        Assert.True(result.FormatValid);
        Assert.True(result.NetworkCheckPassed);
        Assert.Equal(429, result.HttpStatusCode);
        Assert.Equal("RateLimited", result.ErrorType);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithServiceError_ReturnsServiceIssue()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("{\"error\": {\"message\": \"Service temporarily unavailable\"}}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(validKey);

        // Assert
        Assert.False(result.IsValid); // Service issue, can't confirm validity
        Assert.Equal("ServiceIssue", result.Status);
        Assert.Contains("Service temporarily unavailable", result.Message);
        Assert.True(result.FormatValid);
        Assert.True(result.NetworkCheckPassed);
        Assert.Equal(503, result.HttpStatusCode);
        Assert.Equal("ServiceError", result.ErrorType);
    }

    [Fact]
    public void ValidateKeyFormat_WithLiveKey_ReturnsTrue()
    {
        // Arrange
        var validKey = "sk-live-1234567890abcdefghijklmnopqrstuvwxyz";

        // Act
        var (isValid, message) = _service.ValidateKeyFormat(validKey);

        // Assert
        Assert.True(isValid);
        Assert.Contains("Format looks correct", message);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithNetworkError_ReturnsNetworkError()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.ValidateKeyAsync(validKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("NetworkError", result.Status);
        Assert.Contains("Network error while contacting OpenAI", result.Message);
        Assert.True(result.FormatValid);
        Assert.False(result.NetworkCheckPassed);
        Assert.Null(result.HttpStatusCode);
        Assert.Equal("NetworkError", result.ErrorType);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithTimeout_ReturnsTimeout()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        // Act
        var result = await _service.ValidateKeyAsync(validKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Timeout", result.Status);
        Assert.Contains("timed out", result.Message);
        Assert.True(result.FormatValid);
        Assert.False(result.NetworkCheckPassed);
        Assert.Equal("Timeout", result.ErrorType);
    }

    [Fact]
    public async Task ValidateKeyAsync_WithOrgAndProjectHeaders_IncludesHeaders()
    {
        // Arrange
        var validKey = "sk-proj-1234567890abcdefghijklmnopqrstuvwxyz";
        var orgId = "org-123456";
        var projId = "proj-789012";
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\": []}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(validKey, organizationId: orgId, projectId: projId);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("OpenAI-Organization"));
        Assert.True(capturedRequest.Headers.Contains("OpenAI-Project"));
    }

    [Fact]
    public async Task ValidateKeyAsync_WithCustomBaseUrl_UsesCustomUrl()
    {
        // Arrange
        var validKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        var customBaseUrl = "https://custom.openai.com";
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\": []}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(validKey, baseUrl: customBaseUrl);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(capturedRequest);
        Assert.Contains(customBaseUrl, capturedRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task ValidateKeyAsync_WithInvalidFormat_DoesNotCallNetwork()
    {
        // Arrange
        var invalidKey = "invalid-key";
        var httpCallMade = false;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => httpCallMade = true)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\": []}")
            });

        // Act
        var result = await _service.ValidateKeyAsync(invalidKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.FormatValid);
        Assert.False(result.NetworkCheckPassed);
        Assert.False(httpCallMade); // Network call should not be made for invalid format
    }
}
