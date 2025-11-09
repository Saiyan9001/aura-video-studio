namespace Aura.Api.Models.ApiModels.V1;

/// <summary>
/// Provider configuration DTOs for comprehensive provider and quality settings management
/// </summary>

/// <summary>
/// Provider configuration with API keys, priorities, and cost limits
/// </summary>
public record ProviderConfigDto(
    string Name,
    string Type,
    bool Enabled,
    int Priority,
    string? ApiKey,
    Dictionary<string, string>? AdditionalSettings,
    decimal? CostLimit,
    string? Status);

/// <summary>
/// Request to save provider configuration
/// </summary>
public record SaveProviderConfigRequest(
    List<ProviderConfigDto> Providers);

/// <summary>
/// Model selection configuration
/// </summary>
public record ModelSelectionDto(
    string ProviderName,
    string ProviderType,
    string? SelectedModel,
    List<AvailableModelDto> AvailableModels);

/// <summary>
/// Available model information
/// </summary>
public record AvailableModelDto(
    string Id,
    string Name,
    string? Description,
    List<string> Capabilities,
    decimal? EstimatedCostPer1kTokens,
    bool IsAvailable,
    string? RequiredApiKey);

/// <summary>
/// Quality configuration for video and audio
/// </summary>
public record QualityConfigDto(
    VideoQualityDto Video,
    AudioQualityDto Audio,
    SubtitleStyleDto? Subtitles);

/// <summary>
/// Video quality settings
/// </summary>
public record VideoQualityDto(
    string Resolution,
    int Width,
    int Height,
    int Framerate,
    string BitratePreset,
    int BitrateKbps,
    string Codec,
    string Container);

/// <summary>
/// Audio quality settings
/// </summary>
public record AudioQualityDto(
    int Bitrate,
    int SampleRate,
    int Channels,
    string Codec);

/// <summary>
/// Subtitle style configuration
/// </summary>
public record SubtitleStyleDto(
    string FontFamily,
    int FontSize,
    string FontColor,
    string BackgroundColor,
    double BackgroundOpacity,
    string Position,
    int OutlineWidth,
    string OutlineColor);

/// <summary>
/// Configuration validation result (uses existing ValidationIssueDto from Dtos.cs)
/// </summary>
public record ConfigValidationResultDto(
    bool IsValid,
    List<ValidationIssueDto> Issues,
    List<ValidationIssueDto> Warnings);

/// <summary>
/// Configuration profile
/// </summary>
public record ConfigurationProfileDto(
    string Id,
    string Name,
    string Description,
    SaveProviderConfigRequest ProviderConfig,
    QualityConfigDto QualityConfig,
    DateTime Created,
    DateTime LastModified,
    bool IsDefault,
    string Version);

/// <summary>
/// Request to save a configuration profile
/// </summary>
public record SaveConfigProfileRequest(
    string Name,
    string Description,
    SaveProviderConfigRequest ProviderConfig,
    QualityConfigDto QualityConfig);

/// <summary>
/// Export/Import configuration container
/// </summary>
public record ConfigurationExportDto(
    string Version,
    DateTime ExportedAt,
    List<ConfigurationProfileDto> Profiles,
    ConfigurationProfileDto CurrentProfile);

/// <summary>
/// Request to import configuration
/// </summary>
public record ImportConfigurationRequest(
    ConfigurationExportDto Configuration,
    bool OverwriteExisting);

/// <summary>
/// Available resolution presets
/// </summary>
public enum ResolutionPreset
{
    SD_480p,
    HD_720p,
    FullHD_1080p,
    QHD_1440p,
    UHD_4K
}

/// <summary>
/// Available bitrate presets
/// </summary>
public enum BitratePreset
{
    Low,
    Medium,
    High,
    VeryHigh,
    Custom
}

/// <summary>
/// Provider availability check result
/// </summary>
public record ProviderAvailabilityDto(
    string Name,
    bool IsAvailable,
    string? Reason,
    List<string> MissingDependencies);

/// <summary>
/// Disk space check result
/// </summary>
public record DiskSpaceCheckDto(
    string Path,
    long AvailableBytes,
    long TotalBytes,
    bool HasSufficientSpace,
    long RequiredBytes);

/// <summary>
/// Model capabilities response
/// </summary>
public record ModelCapabilitiesDto(
    string ModelId,
    List<string> Capabilities,
    int? MaxTokens,
    List<string> SupportedLanguages,
    decimal? CostPer1kTokens,
    string? QualityLevel);
