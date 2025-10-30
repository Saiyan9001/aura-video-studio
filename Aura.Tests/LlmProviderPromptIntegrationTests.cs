using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.AI;
using Aura.Providers.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests to verify that LLM providers correctly use EnhancedPromptTemplates
/// for scene analysis (PR 1 requirement)
/// </summary>
public class LlmProviderPromptIntegrationTests
{
    private const string ValidOpenAiApiKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz1234567890";
    private const string ValidAzureApiKey = "12345678901234567890123456789012";
    private const string ValidAzureEndpoint = "https://myresource.openai.azure.com";

    #region OpenAI Provider Tests

    [Fact]
    public async Task OpenAiProvider_AnalyzeSceneImportanceAsync_Should_UseEnhancedPromptTemplates()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            importance = 85.0,
                            complexity = 70.0,
                            emotionalIntensity = 60.0,
                            informationDensity = "medium",
                            optimalDurationSeconds = 10.0,
                            transitionType = "fade",
                            reasoning = "Test reasoning"
                        })
                    }
                }
            }
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        string? capturedRequestBody = null;

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidOpenAiApiKey);

        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

        try
        {
            // Act
            var result = await provider.AnalyzeSceneImportanceAsync(
                "Test scene text",
                "Previous scene",
                "Educational goal",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestBody);

            // Verify that the request uses enhanced prompt templates
            // by checking for schema-specific keywords
            Assert.Contains("importance", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("complexity", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("optimalDurationSeconds", capturedRequestBody, StringComparison.OrdinalIgnoreCase);

            // Verify strict schema is used when enabled
            if (EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema)
            {
                Assert.Contains("VALID JSON", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            }

            // Verify result is properly parsed
            Assert.NotNull(result);
            Assert.Equal(85.0, result.Importance);
            Assert.Equal(70.0, result.Complexity);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
        }
    }

    [Fact]
    public async Task OpenAiProvider_AnalyzeSceneImportanceAsync_Should_IncludeFewShotExamplesWhenEnabled()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            importance = 75.0,
                            complexity = 50.0,
                            emotionalIntensity = 40.0,
                            informationDensity = "low",
                            optimalDurationSeconds = 8.0,
                            transitionType = "cut",
                            reasoning = "Simple scene"
                        })
                    }
                }
            }
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        string? capturedRequestBody = null;

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidOpenAiApiKey);

        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        var originalExampleCount = EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount;

        EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;
        EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = 2;

        try
        {
            // Act
            await provider.AnalyzeSceneImportanceAsync(
                "Test scene",
                null,
                "Test goal",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestBody);
            Assert.Contains("FEW-SHOT EXAMPLES", capturedRequestBody);
            Assert.Contains("Example 1", capturedRequestBody);
            Assert.Contains("Example 2", capturedRequestBody);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
            EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = originalExampleCount;
        }
    }

    #endregion

    #region Ollama Provider Tests

    [Fact]
    public async Task OllamaProvider_AnalyzeSceneImportanceAsync_Should_UseEnhancedPromptTemplates()
    {
        // Arrange
        var mockResponse = new
        {
            response = JsonSerializer.Serialize(new
            {
                importance = 80.0,
                complexity = 65.0,
                emotionalIntensity = 55.0,
                informationDensity = "medium",
                optimalDurationSeconds = 9.0,
                transitionType = "dissolve",
                reasoning = "Ollama test reasoning"
            })
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        string? capturedRequestBody = null;

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var provider = new OllamaLlmProvider(
            NullLogger<OllamaLlmProvider>.Instance,
            httpClient);

        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

        try
        {
            // Act
            var result = await provider.AnalyzeSceneImportanceAsync(
                "Ollama test scene",
                null,
                "Ollama goal",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestBody);

            // Verify enhanced templates are used
            Assert.Contains("importance", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("optimalDurationSeconds", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("transitionType", capturedRequestBody, StringComparison.OrdinalIgnoreCase);

            // Verify result
            Assert.NotNull(result);
            Assert.Equal(80.0, result.Importance);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
        }
    }

    #endregion

    #region Azure OpenAI Provider Tests

    [Fact]
    public async Task AzureProvider_AnalyzeSceneImportanceAsync_Should_UseEnhancedPromptTemplates()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            importance = 90.0,
                            complexity = 75.0,
                            emotionalIntensity = 65.0,
                            informationDensity = "high",
                            optimalDurationSeconds = 12.0,
                            transitionType = "fade",
                            reasoning = "Azure test reasoning"
                        })
                    }
                }
            }
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        string? capturedRequestBody = null;

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var provider = new AzureOpenAiLlmProvider(
            NullLogger<AzureOpenAiLlmProvider>.Instance,
            httpClient,
            ValidAzureApiKey,
            ValidAzureEndpoint);

        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

        try
        {
            // Act
            var result = await provider.AnalyzeSceneImportanceAsync(
                "Azure test scene",
                "Azure previous scene",
                "Azure goal",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestBody);

            // Verify enhanced templates are used
            Assert.Contains("importance", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("complexity", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("optimalDurationSeconds", capturedRequestBody, StringComparison.OrdinalIgnoreCase);

            // Verify result
            Assert.NotNull(result);
            Assert.Equal(90.0, result.Importance);
            Assert.Equal(75.0, result.Complexity);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
        }
    }

    #endregion

    #region Gemini Provider Tests

    [Fact]
    public async Task GeminiProvider_AnalyzeSceneImportanceAsync_Should_UseEnhancedPromptTemplates()
    {
        // Arrange
        var mockResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    importance = 88.0,
                                    complexity = 72.0,
                                    emotionalIntensity = 62.0,
                                    informationDensity = "high",
                                    optimalDurationSeconds = 11.0,
                                    transitionType = "dissolve",
                                    reasoning = "Gemini test reasoning"
                                })
                            }
                        }
                    }
                }
            }
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        string? capturedRequestBody = null;

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var provider = new GeminiLlmProvider(
            NullLogger<GeminiLlmProvider>.Instance,
            httpClient,
            "AIzaSyABCDEFGH1234567890IJKLMNOPQRSTUVWXYZ"); // Valid format API key

        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

        try
        {
            // Act
            var result = await provider.AnalyzeSceneImportanceAsync(
                "Gemini test scene",
                "Gemini previous scene",
                "Gemini goal",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestBody);

            // Verify enhanced templates are used
            Assert.Contains("importance", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("complexity", capturedRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("optimalDurationSeconds", capturedRequestBody, StringComparison.OrdinalIgnoreCase);

            // Verify result
            Assert.NotNull(result);
            Assert.Equal(88.0, result.Importance);
            Assert.Equal(72.0, result.Complexity);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
        }
    }

    #endregion

    #region Schema Toggle Tests

    [Fact]
    public async Task Providers_Should_RespectStrictSchemaToggle()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            importance = 50.0,
                            complexity = 50.0,
                            emotionalIntensity = 50.0,
                            informationDensity = "medium",
                            optimalDurationSeconds = 10.0,
                            transitionType = "cut",
                            reasoning = "Test"
                        })
                    }
                }
            }
        };

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        string? capturedRequestBodyStrict = null;
        string? capturedRequestBodyCompact = null;
        int callCount = 0;

        mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                var body = request.Content?.ReadAsStringAsync().Result;
                if (callCount == 0)
                {
                    capturedRequestBodyStrict = body;
                }
                else
                {
                    capturedRequestBodyCompact = body;
                }
                callCount++;

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var provider = new OpenAiLlmProvider(
            NullLogger<OpenAiLlmProvider>.Instance,
            httpClient,
            ValidOpenAiApiKey);

        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            // Act - Call with strict schema enabled
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;
            await provider.AnalyzeSceneImportanceAsync("Scene", null, "Goal", CancellationToken.None);

            // Act - Call with strict schema disabled
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = false;
            await provider.AnalyzeSceneImportanceAsync("Scene", null, "Goal", CancellationToken.None);

            // Assert
            Assert.NotNull(capturedRequestBodyStrict);
            Assert.NotNull(capturedRequestBodyCompact);

            // Strict mode should contain detailed schema
            Assert.Contains("VALID JSON", capturedRequestBodyStrict, StringComparison.OrdinalIgnoreCase);

            // Compact mode should not contain detailed schema
            Assert.DoesNotContain("EXACT schema", capturedRequestBodyCompact ?? string.Empty);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
        }
    }

    #endregion
}
