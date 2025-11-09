using System;
using System.Collections.Generic;

namespace Aura.Core.Models.Assets;

/// <summary>
/// Type of asset in the library
/// </summary>
public enum AssetType
{
    Image,
    Video,
    Audio
}

/// <summary>
/// Source from which the asset was obtained
/// </summary>
public enum AssetSource
{
    Uploaded,
    StockPexels,
    StockPixabay,
    AIGenerated,
    Sample
}

/// <summary>
/// Metadata for an asset including technical details
/// </summary>
public record AssetMetadata
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public TimeSpan? Duration { get; init; }
    public long? FileSizeBytes { get; init; }
    public string? Format { get; init; }
    public string? Codec { get; init; }
    public int? Bitrate { get; init; }
    public int? SampleRate { get; init; }
    public Dictionary<string, string> Extra { get; init; } = new();
}

/// <summary>
/// A tag with confidence score
/// </summary>
public record AssetTag(
    string Name,
    int Confidence = 100)
{
    public string Name { get; init; } = Name.ToLowerInvariant();
}

/// <summary>
/// Asset record in the library
/// </summary>
public record Asset
{
    public Guid Id { get; init; }
    public AssetType Type { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string? ThumbnailPath { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<AssetTag> Tags { get; init; } = new();
    public AssetSource Source { get; init; }
    public AssetMetadata Metadata { get; init; } = new();
    public DateTime DateAdded { get; init; }
    public DateTime DateModified { get; init; }
    public int UsageCount { get; init; }
    public List<string> Collections { get; init; } = new();
    public string? DominantColor { get; init; }
    public AssetLicensingInfo? Licensing { get; init; }
}

/// <summary>
/// Collection of assets
/// </summary>
public record AssetCollection
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Color { get; init; } = "#0078D4";
    public DateTime DateCreated { get; init; }
    public DateTime DateModified { get; init; }
    public List<Guid> AssetIds { get; init; } = new();
}

/// <summary>
/// Search filters for assets
/// </summary>
public record AssetSearchFilters
{
    public AssetType? Type { get; init; }
    public List<string> Tags { get; init; } = new();
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? MinWidth { get; init; }
    public int? MaxWidth { get; init; }
    public int? MinHeight { get; init; }
    public int? MaxHeight { get; init; }
    public TimeSpan? MinDuration { get; init; }
    public TimeSpan? MaxDuration { get; init; }
    public AssetSource? Source { get; init; }
    public List<string> Collections { get; init; } = new();
    public bool? UsedInTimeline { get; init; }
}

/// <summary>
/// Search result with pagination
/// </summary>
public record AssetSearchResult
{
    public List<Asset> Assets { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// Stock image from external provider
/// </summary>
public record StockImage
{
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string FullSizeUrl { get; init; } = string.Empty;
    public string PreviewUrl { get; init; } = string.Empty;
    public string? Photographer { get; init; }
    public string? PhotographerUrl { get; init; }
    public string Source { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// AI image generation request
/// </summary>
public record AIImageGenerationRequest
{
    public string Prompt { get; init; } = string.Empty;
    public string? NegativePrompt { get; init; }
    public string Style { get; init; } = "photorealistic";
    public string Size { get; init; } = "1024x1024";
    public int Steps { get; init; } = 30;
    public double CfgScale { get; init; } = 7.5;
    public int? Seed { get; init; }
}

/// <summary>
/// Licensing information for an asset
/// </summary>
public record AssetLicensingInfo
{
    public string LicenseType { get; init; } = string.Empty;
    public string? Attribution { get; init; }
    public string? LicenseUrl { get; init; }
    public bool CommercialUseAllowed { get; init; }
    public bool AttributionRequired { get; init; }
    public string? CreatorName { get; init; }
    public string? CreatorUrl { get; init; }
    public string SourcePlatform { get; init; } = string.Empty;
}
