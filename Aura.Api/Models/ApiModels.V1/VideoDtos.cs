using System;
using System.Collections.Generic;

namespace Aura.Api.Models.ApiModels.V1;

/// <summary>
/// Request for generating a new video
/// </summary>
public record VideoGenerationRequest(
    string Brief,
    string? VoiceId,
    string? Style,
    double DurationMinutes,
    VideoGenerationOptions? Options = null);

/// <summary>
/// Additional options for video generation
/// </summary>
public record VideoGenerationOptions(
    string? Audience = null,
    string? Goal = null,
    string? Tone = null,
    string? Language = null,
    string? Aspect = null,
    string? Pacing = null,
    string? Density = null,
    int? Width = null,
    int? Height = null,
    int? Fps = null,
    string? Codec = null,
    bool EnableHardwareAcceleration = true);

/// <summary>
/// Response from video generation initiation
/// </summary>
public record VideoGenerationResponse(
    string JobId,
    string Status,
    string? VideoUrl,
    DateTime CreatedAt,
    string CorrelationId);

/// <summary>
/// Progress update for video generation
/// </summary>
public record ProgressUpdate(
    int Percentage,
    string Stage,
    string Message,
    DateTime Timestamp,
    string? CurrentTask = null,
    TimeSpan? EstimatedTimeRemaining = null);

/// <summary>
/// Detailed video status information
/// </summary>
public record VideoStatus(
    string JobId,
    string Status,
    int ProgressPercentage,
    string CurrentStage,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? VideoUrl,
    string? ErrorMessage,
    List<string> ProcessingSteps,
    string CorrelationId);

/// <summary>
/// Video metadata information
/// </summary>
public record VideoMetadata(
    string JobId,
    string OutputPath,
    long FileSizeBytes,
    DateTime CreatedAt,
    DateTime CompletedAt,
    TimeSpan Duration,
    string Resolution,
    string Codec,
    int Fps,
    List<ArtifactInfo> Artifacts,
    string CorrelationId);

/// <summary>
/// Information about a generated artifact
/// </summary>
public record ArtifactInfo(
    string Name,
    string Path,
    string Type,
    long SizeBytes);
