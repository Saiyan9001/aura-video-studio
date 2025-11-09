using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Aura.Api.Models.ApiModels.V1;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Aura.Tests.Integration;

/// <summary>
/// Integration tests for VideoController API endpoints
/// </summary>
public class VideoControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public VideoControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_GenerateVideo_ValidRequest_Returns202Accepted()
    {
        // Arrange
        var request = new VideoGenerationRequest(
            Brief: "Create a short video about artificial intelligence and its impact on society",
            VoiceId: null,
            Style: "informative",
            DurationMinutes: 0.5,
            Options: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/videos/generate", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<VideoGenerationResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.JobId);
        Assert.NotEmpty(result.JobId);
        Assert.NotNull(result.CorrelationId);
        
        // Check Location header
        Assert.True(response.Headers.Location != null);
    }

    [Fact]
    public async Task POST_GenerateVideo_EmptyBrief_Returns400BadRequest()
    {
        // Arrange
        var request = new VideoGenerationRequest(
            Brief: "",
            VoiceId: null,
            Style: null,
            DurationMinutes: 1.0,
            Options: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/videos/generate", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problemDetails.TryGetProperty("title", out _));
        Assert.True(problemDetails.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task POST_GenerateVideo_InvalidDuration_Returns400BadRequest()
    {
        // Arrange
        var request = new VideoGenerationRequest(
            Brief: "Valid brief content here",
            VoiceId: null,
            Style: null,
            DurationMinutes: 0,
            Options: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/videos/generate", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_GenerateVideo_ExcessiveDuration_Returns400BadRequest()
    {
        // Arrange
        var request = new VideoGenerationRequest(
            Brief: "Valid brief content here",
            VoiceId: null,
            Style: null,
            DurationMinutes: 15,
            Options: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/videos/generate", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_VideoStatus_NonexistentJob_Returns404NotFound()
    {
        // Arrange
        var jobId = "nonexistent-job-id";

        // Act
        var response = await _client.GetAsync($"/api/videos/{jobId}/status");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problemDetails.TryGetProperty("title", out var title));
        Assert.Contains("Not Found", title.GetString());
    }

    [Fact]
    public async Task GET_VideoStatus_ExistingJob_Returns200OK()
    {
        // Arrange - First create a job
        var createRequest = new VideoGenerationRequest(
            Brief: "Test video for status check",
            VoiceId: null,
            Style: null,
            DurationMinutes: 0.5,
            Options: null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/videos/generate", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<VideoGenerationResponse>();
        
        Assert.NotNull(createResult);
        var jobId = createResult.JobId;

        // Act - Get status
        var response = await _client.GetAsync($"/api/videos/{jobId}/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var status = await response.Content.ReadFromJsonAsync<VideoStatus>();
        Assert.NotNull(status);
        Assert.Equal(jobId, status.JobId);
        Assert.NotNull(status.Status);
        Assert.NotNull(status.CurrentStage);
        Assert.True(status.ProgressPercentage >= 0 && status.ProgressPercentage <= 100);
    }

    [Fact]
    public async Task GET_VideoStream_ValidJob_ReturnsSSEStream()
    {
        // Arrange - First create a job
        var createRequest = new VideoGenerationRequest(
            Brief: "Test video for SSE streaming",
            VoiceId: null,
            Style: null,
            DurationMinutes: 0.25,
            Options: null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/videos/generate", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<VideoGenerationResponse>();
        
        Assert.NotNull(createResult);
        var jobId = createResult.JobId;

        // Act - Connect to SSE stream
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/videos/{jobId}/stream");
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public async Task GET_VideoStream_NonexistentJob_Returns404()
    {
        // Arrange
        var jobId = "nonexistent-job-for-stream";

        // Act
        var response = await _client.GetAsync($"/api/videos/{jobId}/stream");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_GenerateVideo_WithOptions_Returns202Accepted()
    {
        // Arrange
        var request = new VideoGenerationRequest(
            Brief: "Create a video about space exploration",
            VoiceId: "en-US-neural",
            Style: "documentary",
            DurationMinutes: 1.0,
            Options: new VideoGenerationOptions(
                Audience: "science enthusiasts",
                Goal: "educate and inspire",
                Tone: "inspiring",
                Language: "English",
                Aspect: "16:9",
                Pacing: "normal",
                Density: "balanced",
                Width: 1920,
                Height: 1080,
                Fps: 30,
                Codec: "H264",
                EnableHardwareAcceleration: true
            )
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/videos/generate", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<VideoGenerationResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.JobId);
    }

    [Fact]
    public async Task VideoGeneration_FullWorkflow_CreatesAndTracksJob()
    {
        // Arrange
        var createRequest = new VideoGenerationRequest(
            Brief: "Test the complete video generation workflow",
            VoiceId: null,
            Style: "tutorial",
            DurationMinutes: 0.25,
            Options: null
        );

        // Act & Assert - Step 1: Create job
        var createResponse = await _client.PostAsJsonAsync("/api/videos/generate", createRequest);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);
        
        var jobInfo = await createResponse.Content.ReadFromJsonAsync<VideoGenerationResponse>();
        Assert.NotNull(jobInfo);
        var jobId = jobInfo.JobId;
        
        // Step 2: Check status immediately
        var statusResponse = await _client.GetAsync($"/api/videos/{jobId}/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        
        var status = await statusResponse.Content.ReadFromJsonAsync<VideoStatus>();
        Assert.NotNull(status);
        Assert.Equal(jobId, status.JobId);
        
        // Step 3: Verify stream connection
        var streamRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/videos/{jobId}/stream");
        var streamResponse = await _client.SendAsync(streamRequest, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
        Assert.Equal("text/event-stream", streamResponse.Content.Headers.ContentType?.MediaType);
    }
}
