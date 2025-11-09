using Xunit;
using Aura.Providers.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;

namespace Aura.Tests;

public class OpenAILlmProviderTests
{
    private const string ValidApiKey = "sk-test-validkey1234567890abcdefghijklmnopqrstuvwxyz";
    private const string InvalidApiKey = "sk-invalid";

    [Fact]
    public async Task GetAvailableModelsAsync_WithValidKey_ReturnsFilteredModels()
    {
        // Arrange
        var mockResponse = new
        {
            data = new[]
            {
                new { id = "gpt-4o", created = 1700000003L },
                new { id = "gpt-4o-mini", created = 1700000002L },
                new { id = "gpt-3.5-turbo", created = 1700000001L },
                new { id = "o1-preview", created = 1700000004L },
                new { id = "dall-e-3", created = 1700000000L }, // Should be filtered out
                new { id = "whisper-1", created = 1699999999L } // Should be filtered out
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var models = await provider.GetAvailableModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Equal(4, models.Count); // Only GPT and O1 models
        Assert.Contains(models, m => m.Id == "gpt-4o");
        Assert.Contains(models, m => m.Id == "gpt-4o-mini");
        Assert.Contains(models, m => m.Id == "gpt-3.5-turbo");
        Assert.Contains(models, m => m.Id == "o1-preview");
        Assert.DoesNotContain(models, m => m.Id == "dall-e-3");
        Assert.DoesNotContain(models, m => m.Id == "whisper-1");
        
        // Verify sorted by created timestamp (newest first)
        Assert.Equal("o1-preview", models[0].Id);
        Assert.Equal("gpt-4o", models[1].Id);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WithGPT5Models_IncludesInResults()
    {
        // Arrange
        var mockResponse = new
        {
            data = new[]
            {
                new { id = "gpt-5-turbo", created = 1800000001L },
                new { id = "gpt-5", created = 1800000002L },
                new { id = "gpt-4o", created = 1700000001L }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o"
        );

        // Act
        var models = await provider.GetAvailableModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Equal(3, models.Count);
        Assert.Contains(models, m => m.Id == "gpt-5-turbo");
        Assert.Contains(models, m => m.Id == "gpt-5");
        Assert.Contains(models, m => m.Id == "gpt-4o");
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WithUnauthorized_ReturnsEmptyList()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, "");
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            InvalidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var models = await provider.GetAvailableModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WithNetworkTimeout_ReturnsEmptyList()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var models = await provider.GetAvailableModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsSuccess()
    {
        // Arrange
        var mockResponse = new
        {
            data = new[]
            {
                new { id = "gpt-4o", created = 1700000001L },
                new { id = "gpt-4o-mini", created = 1700000002L }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var result = await provider.ValidateApiKeyAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("API key is valid", result.Message);
        Assert.NotNull(result.AvailableModels);
        Assert.NotEmpty(result.AvailableModels);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithUnauthorized_ReturnsInvalid()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, "");
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            InvalidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var result = await provider.ValidateApiKeyAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid API key", result.Message);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithRateLimit_ReturnsRateLimitError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.TooManyRequests, "");
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var result = await provider.ValidateApiKeyAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Rate limit exceeded or billing issue", result.Message);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithNetworkError_ReturnsConnectivityIssue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var result = await provider.ValidateApiKeyAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Network connectivity issue", result.Message);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithTimeout_ReturnsConnectivityIssue()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(mockHandler.Object);
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o-mini"
        );

        // Act
        var result = await provider.ValidateApiKeyAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Network connectivity issue", result.Message);
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new OpenAiLlmProvider(
                NullLogger<OpenAiLlmProvider>.Instance,
                new HttpClient(),
                "",
                "gpt-4o-mini"
            )
        );

        Assert.Contains("API key is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithInvalidApiKeyFormat_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new OpenAiLlmProvider(
                NullLogger<OpenAiLlmProvider>.Instance,
                new HttpClient(),
                "invalid-key",
                "gpt-4o-mini"
            )
        );

        Assert.Contains("API key format appears invalid", exception.Message);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_FiltersNonChatModels()
    {
        // Arrange
        var mockResponse = new
        {
            data = new[]
            {
                new { id = "gpt-4o", created = 1700000001L },
                new { id = "text-embedding-ada-002", created = 1700000002L },
                new { id = "tts-1", created = 1700000003L },
                new { id = "dall-e-2", created = 1700000004L },
                new { id = "o1-mini", created = 1700000005L }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o"
        );

        // Act
        var models = await provider.GetAvailableModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Equal(2, models.Count); // Only gpt-4o and o1-mini
        Assert.Contains(models, m => m.Id == "gpt-4o");
        Assert.Contains(models, m => m.Id == "o1-mini");
        Assert.DoesNotContain(models, m => m.Id == "text-embedding-ada-002");
        Assert.DoesNotContain(models, m => m.Id == "tts-1");
        Assert.DoesNotContain(models, m => m.Id == "dall-e-2");
    }

    [Fact]
    public async Task GetAvailableModelsAsync_IncludesCapabilitiesAndCreatedTimestamp()
    {
        // Arrange
        var mockResponse = new
        {
            data = new[]
            {
                new { id = "gpt-4o", created = 1700000001L }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidApiKey,
            "gpt-4o"
        );

        // Act
        var models = await provider.GetAvailableModelsAsync();

        // Assert
        Assert.Single(models);
        var model = models[0];
        Assert.Equal("gpt-4o", model.Id);
        Assert.Equal("gpt-4o", model.Name);
        Assert.Equal(1700000001L, model.Created);
        Assert.Contains("chat", model.Capabilities);
        Assert.Contains("completion", model.Capabilities);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return new HttpClient(mockHandler.Object);
    }
}
