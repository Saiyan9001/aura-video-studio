using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Aura.Core.Models;
using Xunit;

namespace Aura.Tests;

public class RecommendationApiIntegrationTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public RecommendationApiIntegrationTests(ApiTestFixture fixture)
    {
        _client = fixture.Client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task GetRecommendations_WithValidInput_ShouldReturnSuccessfully()
    {
        // Arrange
        var request = new
        {
            topic = "Introduction to Machine Learning",
            audience = "Beginners",
            goal = "Educational",
            tone = "Informative",
            language = "en-US",
            aspect = "Widescreen16x9",
            targetDurationMinutes = 5.0,
            pacing = "Conversational",
            density = "Balanced",
            style = "Educational"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/planner/recommendations", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RecommendationResponse>(responseBody, _jsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Recommendations);
        Assert.InRange(result.Recommendations.SceneCount, 3, 20);
        Assert.InRange(result.Recommendations.ShotsPerScene, 1, 8);
        Assert.InRange(result.Recommendations.BRollPercentage, 0.0, 100.0);
        Assert.NotEmpty(result.Recommendations.Outline);
    }

    [Fact]
    public async Task GetRecommendations_WithMissingTopic_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            topic = "",
            targetDurationMinutes = 5.0
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/planner/recommendations", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRecommendations_WithInvalidDuration_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            topic = "Test Topic",
            targetDurationMinutes = 0.0
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/planner/recommendations", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRecommendations_WithPersona_ShouldIncludePersonaConsiderations()
    {
        // Arrange
        var request = new
        {
            topic = "Advanced Robotics",
            audience = "Experts",
            goal = "Educational",
            tone = "Professional",
            language = "en-US",
            aspect = "Widescreen16x9",
            targetDurationMinutes = 10.0,
            pacing = "Conversational",
            density = "Dense",
            style = "Educational",
            personaName = "Dr. Robotics Expert",
            personaDemographics = "PhD researchers",
            personaInterests = "Robotics and AI",
            personaExpertiseLevel = "Expert"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/planner/recommendations", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RecommendationResponse>(responseBody, _jsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Recommendations);
        Assert.Contains("Advanced", result.Recommendations.ReadingLevel);
    }

    [Fact]
    public async Task GetRecommendations_WithConstraints_ShouldRespectConstraints()
    {
        // Arrange
        var request = new
        {
            topic = "Quick Tutorial",
            audience = "Beginners",
            goal = "Tutorial",
            tone = "Friendly",
            language = "en-US",
            aspect = "Vertical9x16",
            targetDurationMinutes = 3.0,
            pacing = "Fast",
            density = "Balanced",
            style = "Tutorial",
            maxDurationMinutes = 5.0,
            minDurationMinutes = 2.0,
            mustBeOffline = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/planner/recommendations", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RecommendationResponse>(responseBody, _jsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Recommendations);
    }

    [Fact]
    public async Task GetRecommendations_WithVerticalAspect_ShouldAdaptCaptionStyle()
    {
        // Arrange
        var request = new
        {
            topic = "TikTok Tutorial",
            audience = "Young Adults",
            goal = "Entertain",
            tone = "Energetic",
            language = "en-US",
            aspect = "Vertical9x16",
            targetDurationMinutes = 1.0,
            pacing = "Fast",
            density = "Balanced",
            style = "Tutorial"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/planner/recommendations", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RecommendationResponse>(responseBody, _jsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Recommendations);
        Assert.NotNull(result.Recommendations.CaptionStyle);
        // Vertical format should have different caption style
        Assert.Contains("Center", result.Recommendations.CaptionStyle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRecommendations_WithDifferentPacings_ShouldVaryRecommendations()
    {
        // Arrange - Chill pacing
        var chillRequest = new
        {
            topic = "Meditation Guide",
            audience = "Adults",
            goal = "Wellness",
            tone = "Calm",
            language = "en-US",
            aspect = "Widescreen16x9",
            targetDurationMinutes = 5.0,
            pacing = "Chill",
            density = "Sparse",
            style = "Wellness"
        };

        // Arrange - Fast pacing
        var fastRequest = new
        {
            topic = "Quick News Update",
            audience = "General",
            goal = "Inform",
            tone = "Energetic",
            language = "en-US",
            aspect = "Widescreen16x9",
            targetDurationMinutes = 5.0,
            pacing = "Fast",
            density = "Dense",
            style = "News"
        };

        var chillContent = new StringContent(
            JsonSerializer.Serialize(chillRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var fastContent = new StringContent(
            JsonSerializer.Serialize(fastRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var chillResponse = await _client.PostAsync("/api/planner/recommendations", chillContent);
        var fastResponse = await _client.PostAsync("/api/planner/recommendations", fastContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, chillResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fastResponse.StatusCode);

        var chillBody = await chillResponse.Content.ReadAsStringAsync();
        var fastBody = await fastResponse.Content.ReadAsStringAsync();

        var chillResult = JsonSerializer.Deserialize<RecommendationResponse>(chillBody, _jsonOptions);
        var fastResult = JsonSerializer.Deserialize<RecommendationResponse>(fastBody, _jsonOptions);

        Assert.NotNull(chillResult?.Recommendations);
        Assert.NotNull(fastResult?.Recommendations);

        // Fast pacing should have higher voice rate
        Assert.True(fastResult.Recommendations.VoiceRate > chillResult.Recommendations.VoiceRate);
    }

    // Helper classes for deserialization
    private class RecommendationResponse
    {
        public bool Success { get; set; }
        public PlanRecommendationsDto Recommendations { get; set; } = new();
    }

    private class PlanRecommendationsDto
    {
        public string Outline { get; set; } = "";
        public int SceneCount { get; set; }
        public int ShotsPerScene { get; set; }
        public double BRollPercentage { get; set; }
        public double OverlayDensity { get; set; }
        public string ReadingLevel { get; set; } = "";
        public double VoiceRate { get; set; }
        public double VoicePitch { get; set; }
        public string MusicTempoCurve { get; set; } = "";
        public string MusicIntensityCurve { get; set; } = "";
        public string CaptionStyle { get; set; } = "";
        public string ThumbnailPrompt { get; set; } = "";
        public string SeoTitle { get; set; } = "";
        public string SeoDescription { get; set; } = "";
        public string[] SeoTags { get; set; } = Array.Empty<string>();
    }
}
