using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aura.Core.Models.Assets;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Assets;

/// <summary>
/// Service for managing built-in sample assets for demos and testing
/// </summary>
public class SampleAssetsService
{
    private readonly ILogger<SampleAssetsService> _logger;
    private readonly string _samplesPath;
    private readonly AssetLibraryService _assetLibrary;
    private readonly SampleImageGenerator? _imageGenerator;

    private List<BriefTemplate>? _briefTemplates;
    private List<VoiceConfiguration>? _voiceConfigs;
    private bool _initialized;

    public SampleAssetsService(
        ILogger<SampleAssetsService> logger,
        string samplesPath,
        AssetLibraryService assetLibrary,
        SampleImageGenerator? imageGenerator = null)
    {
        _logger = logger;
        _samplesPath = samplesPath;
        _assetLibrary = assetLibrary;
        _imageGenerator = imageGenerator;
    }

    /// <summary>
    /// Initialize sample assets and load into asset library
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _logger.LogInformation("Initializing sample assets from {Path}", _samplesPath);

        try
        {
            await LoadBriefTemplatesAsync();
            await LoadVoiceConfigurationsAsync();
            await LoadSampleImagesAsync();
            await LoadSampleAudioAsync();

            _initialized = true;
            _logger.LogInformation("Sample assets initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize sample assets");
        }
    }

    /// <summary>
    /// Get all brief templates
    /// </summary>
    public async Task<List<BriefTemplate>> GetBriefTemplatesAsync()
    {
        if (_briefTemplates == null)
            await LoadBriefTemplatesAsync();

        return _briefTemplates ?? new List<BriefTemplate>();
    }

    /// <summary>
    /// Get brief template by ID
    /// </summary>
    public async Task<BriefTemplate?> GetBriefTemplateAsync(string templateId)
    {
        var templates = await GetBriefTemplatesAsync();
        return templates.FirstOrDefault(t => t.Id == templateId);
    }

    /// <summary>
    /// Get all voice configurations
    /// </summary>
    public async Task<List<VoiceConfiguration>> GetVoiceConfigurationsAsync()
    {
        if (_voiceConfigs == null)
            await LoadVoiceConfigurationsAsync();

        return _voiceConfigs ?? new List<VoiceConfiguration>();
    }

    /// <summary>
    /// Get voice configurations for a specific provider
    /// </summary>
    public async Task<List<VoiceConfiguration>> GetVoiceConfigurationsByProviderAsync(string provider)
    {
        var configs = await GetVoiceConfigurationsAsync();
        return configs.Where(c => c.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Get all sample images
    /// </summary>
    public async Task<List<Asset>> GetSampleImagesAsync()
    {
        var filters = new AssetSearchFilters
        {
            Type = AssetType.Image,
            Source = AssetSource.Sample
        };

        var result = await _assetLibrary.SearchAssetsAsync(null, filters, 1, 100);
        return result.Assets;
    }

    /// <summary>
    /// Get all sample audio
    /// </summary>
    public async Task<List<Asset>> GetSampleAudioAsync()
    {
        var filters = new AssetSearchFilters
        {
            Type = AssetType.Audio,
            Source = AssetSource.Sample
        };

        var result = await _assetLibrary.SearchAssetsAsync(null, filters, 1, 100);
        return result.Assets;
    }

    private async Task LoadBriefTemplatesAsync()
    {
        var templatesFile = Path.Combine(_samplesPath, "Templates", "brief-templates.json");
        
        if (!File.Exists(templatesFile))
        {
            _logger.LogWarning("Brief templates file not found: {Path}", templatesFile);
            _briefTemplates = new List<BriefTemplate>();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(templatesFile);
            var data = JsonSerializer.Deserialize<BriefTemplatesData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _briefTemplates = data?.Templates ?? new List<BriefTemplate>();
            _logger.LogInformation("Loaded {Count} brief templates", _briefTemplates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load brief templates from {Path}", templatesFile);
            _briefTemplates = new List<BriefTemplate>();
        }
    }

    private async Task LoadVoiceConfigurationsAsync()
    {
        var voiceConfigFile = Path.Combine(_samplesPath, "Templates", "voice-configs.json");
        
        if (!File.Exists(voiceConfigFile))
        {
            _logger.LogWarning("Voice configurations file not found: {Path}", voiceConfigFile);
            _voiceConfigs = new List<VoiceConfiguration>();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(voiceConfigFile);
            var data = JsonSerializer.Deserialize<VoiceConfigurationsData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _voiceConfigs = data?.Configurations ?? new List<VoiceConfiguration>();
            _logger.LogInformation("Loaded {Count} voice configurations", _voiceConfigs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load voice configurations from {Path}", voiceConfigFile);
            _voiceConfigs = new List<VoiceConfiguration>();
        }
    }

    private async Task LoadSampleImagesAsync()
    {
        var imagesPath = Path.Combine(_samplesPath, "Images");
        
        if (!Directory.Exists(imagesPath))
        {
            _logger.LogWarning("Sample images directory not found: {Path}", imagesPath);
            Directory.CreateDirectory(imagesPath);
        }

        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };
        var imageFiles = Directory.GetFiles(imagesPath)
            .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0 && _imageGenerator != null)
        {
            _logger.LogInformation("No sample images found, generating placeholder images");
            _imageGenerator.GenerateSampleImages(imagesPath);
            
            imageFiles = Directory.GetFiles(imagesPath)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }

        _logger.LogInformation("Found {Count} sample images", imageFiles.Count);

        foreach (var imageFile in imageFiles)
        {
            try
            {
                var existingAssets = await _assetLibrary.SearchAssetsAsync(
                    Path.GetFileName(imageFile),
                    new AssetSearchFilters { Source = AssetSource.Sample },
                    1, 1);

                if (existingAssets.Assets.Any())
                {
                    _logger.LogDebug("Sample image already in library: {File}", Path.GetFileName(imageFile));
                    continue;
                }

                var asset = await _assetLibrary.AddAssetAsync(imageFile, AssetType.Image, AssetSource.Sample);
                _logger.LogInformation("Added sample image to library: {AssetId}", asset.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add sample image: {File}", imageFile);
            }
        }
    }

    private async Task LoadSampleAudioAsync()
    {
        var audioPath = Path.Combine(_samplesPath, "Audio");
        
        if (!Directory.Exists(audioPath))
        {
            _logger.LogWarning("Sample audio directory not found: {Path}", audioPath);
            return;
        }

        var audioExtensions = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".flac" };
        var audioFiles = Directory.GetFiles(audioPath)
            .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        _logger.LogInformation("Found {Count} sample audio files", audioFiles.Count);

        foreach (var audioFile in audioFiles)
        {
            try
            {
                var existingAssets = await _assetLibrary.SearchAssetsAsync(
                    Path.GetFileName(audioFile),
                    new AssetSearchFilters { Source = AssetSource.Sample },
                    1, 1);

                if (existingAssets.Assets.Any())
                {
                    _logger.LogDebug("Sample audio already in library: {File}", Path.GetFileName(audioFile));
                    continue;
                }

                var asset = await _assetLibrary.AddAssetAsync(audioFile, AssetType.Audio, AssetSource.Sample);
                _logger.LogInformation("Added sample audio to library: {AssetId}", asset.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add sample audio: {File}", audioFile);
            }
        }
    }
}

/// <summary>
/// Brief template for quick start
/// </summary>
public record BriefTemplate
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public BriefTemplateData Brief { get; init; } = new();
    public TemplateSettings Settings { get; init; } = new();
}

/// <summary>
/// Brief data within a template
/// </summary>
public record BriefTemplateData
{
    public string Topic { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Goal { get; init; } = string.Empty;
    public string Tone { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public int Duration { get; init; }
    public List<string> KeyPoints { get; init; } = new();
}

/// <summary>
/// Template settings
/// </summary>
public record TemplateSettings
{
    public string Aspect { get; init; } = "16:9";
    public string Quality { get; init; } = "high";
    public bool UseLocalResourcesOnly { get; init; }
}

/// <summary>
/// Voice configuration for TTS provider
/// </summary>
public record VoiceConfiguration
{
    public string Provider { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string VoiceId { get; init; } = string.Empty;
    public Dictionary<string, object> Settings { get; init; } = new();
    public string SampleText { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public bool RequiresInstallation { get; init; }
}

internal record BriefTemplatesData
{
    public string Version { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<BriefTemplate> Templates { get; init; } = new();
}

internal record VoiceConfigurationsData
{
    public string Version { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<VoiceConfiguration> Configurations { get; init; } = new();
}
