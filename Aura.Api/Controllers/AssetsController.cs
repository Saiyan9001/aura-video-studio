using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aura.Core.Models.Assets;
using Aura.Core.Services.Assets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for asset library operations
/// </summary>
[ApiController]
[Route("api/assets")]
public class AssetsController : ControllerBase
{
    private readonly ILogger<AssetsController> _logger;
    private readonly AssetLibraryService _assetLibrary;
    private readonly AssetTagger _assetTagger;
    private readonly StockImageService _stockImageService;
    private readonly AIImageGenerator _aiImageGenerator;
    private readonly AssetUsageTracker _usageTracker;
    private readonly SampleAssetsService? _sampleAssets;

    public AssetsController(
        ILogger<AssetsController> logger,
        AssetLibraryService assetLibrary,
        AssetTagger assetTagger,
        StockImageService stockImageService,
        AIImageGenerator aiImageGenerator,
        AssetUsageTracker usageTracker,
        SampleAssetsService? sampleAssets = null)
    {
        _logger = logger;
        _assetLibrary = assetLibrary;
        _assetTagger = assetTagger;
        _stockImageService = stockImageService;
        _aiImageGenerator = aiImageGenerator;
        _usageTracker = usageTracker;
        _sampleAssets = sampleAssets;
    }

    /// <summary>
    /// Get all assets with optional search
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAssets(
        [FromQuery] string? query = null,
        [FromQuery] string? type = null,
        [FromQuery] string? source = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortBy = "dateAdded",
        [FromQuery] bool sortDescending = true)
    {
        try
        {
            var filters = new AssetSearchFilters();

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<Core.Models.Assets.AssetType>(type, true, out var assetType))
                filters = filters with { Type = assetType };

            if (!string.IsNullOrWhiteSpace(source) && Enum.TryParse<AssetSource>(source, true, out var assetSource))
                filters = filters with { Source = assetSource };

            var result = await _assetLibrary.SearchAssetsAsync(query, filters, page, pageSize, sortBy, sortDescending);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assets");
            return StatusCode(500, new { error = "Failed to retrieve assets" });
        }
    }

    /// <summary>
    /// Get asset by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsset(Guid id)
    {
        try
        {
            var asset = await _assetLibrary.GetAssetAsync(id);
            if (asset == null)
                return NotFound(new { error = "Asset not found" });

            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get asset {AssetId}", id);
            return StatusCode(500, new { error = "Failed to retrieve asset" });
        }
    }

    /// <summary>
    /// Upload and add asset to library
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAsset([FromForm] IFormFile file, [FromForm] string? type = null)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            // Determine asset type from file extension or parameter
            var assetType = DetermineAssetType(file.FileName, type);

            // Save uploaded file temporarily
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + Path.GetExtension(file.FileName));
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Add to library
            var asset = await _assetLibrary.AddAssetAsync(tempPath, assetType, AssetSource.Uploaded);

            // Generate tags
            var tags = await _assetTagger.GenerateTagsAsync(asset);
            if (tags.Any())
            {
                await _assetLibrary.TagAssetAsync(asset.Id, tags.Select(t => t.Name).ToList());
                asset = await _assetLibrary.GetAssetAsync(asset.Id);
            }

            // Clean up temp file
            System.IO.File.Delete(tempPath);

            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload asset");
            return StatusCode(500, new { error = "Failed to upload asset" });
        }
    }

    /// <summary>
    /// Add tags to an asset
    /// </summary>
    [HttpPost("{id}/tags")]
    public async Task<IActionResult> AddTags(Guid id, [FromBody] List<string> tags)
    {
        try
        {
            await _assetLibrary.TagAssetAsync(id, tags);
            var asset = await _assetLibrary.GetAssetAsync(id);
            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add tags to asset {AssetId}", id);
            return StatusCode(500, new { error = "Failed to add tags" });
        }
    }

    /// <summary>
    /// Delete an asset
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsset(Guid id, [FromQuery] bool deleteFromDisk = false)
    {
        try
        {
            // Check if asset is used in timelines
            var references = await _usageTracker.GetAssetReferencesAsync(id);
            if (references.Any() && !deleteFromDisk)
            {
                return BadRequest(new 
                { 
                    error = "Asset is used in timelines",
                    timelines = references
                });
            }

            var success = await _assetLibrary.DeleteAssetAsync(id, deleteFromDisk);
            if (!success)
                return NotFound(new { error = "Asset not found" });

            return Ok(new { message = "Asset deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete asset {AssetId}", id);
            return StatusCode(500, new { error = "Failed to delete asset" });
        }
    }

    /// <summary>
    /// Search stock images
    /// </summary>
    [HttpGet("stock/search")]
    public async Task<IActionResult> SearchStockImages([FromQuery] string query, [FromQuery] int count = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "Query is required" });

            var results = await _stockImageService.SearchStockImagesAsync(query, count);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search stock images");
            return StatusCode(500, new { error = "Failed to search stock images" });
        }
    }

    /// <summary>
    /// Download and add stock image to library
    /// </summary>
    [HttpPost("stock/download")]
    public async Task<IActionResult> DownloadStockImage([FromBody] StockImageDownloadRequest request)
    {
        try
        {
            // Generate temporary filename
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");

            // Download the image
            await _stockImageService.DownloadStockImageAsync(request.ImageUrl, tempPath);

            // Add to library
            var source = request.Source?.ToLowerInvariant() switch
            {
                "pexels" => AssetSource.StockPexels,
                "pixabay" => AssetSource.StockPixabay,
                _ => AssetSource.Uploaded
            };

            var asset = await _assetLibrary.AddAssetAsync(tempPath, Core.Models.Assets.AssetType.Image, source);

            // Generate tags
            var tags = await _assetTagger.GenerateTagsAsync(asset);
            if (tags.Any())
            {
                await _assetLibrary.TagAssetAsync(asset.Id, tags.Select(t => t.Name).ToList());
                asset = await _assetLibrary.GetAssetAsync(asset.Id);
            }

            // Clean up temp file
            System.IO.File.Delete(tempPath);

            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download stock image");
            return StatusCode(500, new { error = "Failed to download stock image" });
        }
    }

    /// <summary>
    /// Generate AI image
    /// </summary>
    [HttpPost("ai/generate")]
    public async Task<IActionResult> GenerateAIImage([FromBody] AIImageGenerationRequest request)
    {
        try
        {
            // Check if AI generation is available
            if (!await _aiImageGenerator.IsAvailableAsync())
            {
                return BadRequest(new { error = "AI image generation is not available. Please install Stable Diffusion." });
            }

            var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            var generatedPath = await _aiImageGenerator.GenerateImageAsync(request, outputPath);

            if (generatedPath == null)
            {
                return StatusCode(500, new { error = "Failed to generate image" });
            }

            // Add to library
            var asset = await _assetLibrary.AddAssetAsync(generatedPath, Core.Models.Assets.AssetType.Image, AssetSource.AIGenerated);

            // Add prompt as description
            var updatedAsset = asset with { Description = request.Prompt };

            return Ok(updatedAsset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI image");
            return StatusCode(500, new { error = "Failed to generate AI image" });
        }
    }

    /// <summary>
    /// Get all collections
    /// </summary>
    [HttpGet("collections")]
    public async Task<IActionResult> GetCollections()
    {
        try
        {
            var collections = await _assetLibrary.GetCollectionsAsync();
            return Ok(collections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collections");
            return StatusCode(500, new { error = "Failed to retrieve collections" });
        }
    }

    /// <summary>
    /// Create a collection
    /// </summary>
    [HttpPost("collections")]
    public async Task<IActionResult> CreateCollection([FromBody] CreateCollectionRequest request)
    {
        try
        {
            var collection = await _assetLibrary.CreateCollectionAsync(
                request.Name,
                request.Description,
                request.Color ?? "#0078D4");

            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection");
            return StatusCode(500, new { error = "Failed to create collection" });
        }
    }

    /// <summary>
    /// Add asset to collection
    /// </summary>
    [HttpPost("collections/{collectionId}/assets/{assetId}")]
    public async Task<IActionResult> AddToCollection(Guid collectionId, Guid assetId)
    {
        try
        {
            await _assetLibrary.AddToCollectionAsync(assetId, collectionId);
            return Ok(new { message = "Asset added to collection" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add asset to collection");
            return StatusCode(500, new { error = "Failed to add asset to collection" });
        }
    }

    /// <summary>
    /// Get all brief templates
    /// </summary>
    [HttpGet("samples/templates/briefs")]
    public async Task<IActionResult> GetBriefTemplates()
    {
        try
        {
            if (_sampleAssets == null)
                return NotFound(new { error = "Sample assets service not available" });

            var templates = await _sampleAssets.GetBriefTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get brief templates");
            return StatusCode(500, new { error = "Failed to retrieve brief templates" });
        }
    }

    /// <summary>
    /// Get brief template by ID
    /// </summary>
    [HttpGet("samples/templates/briefs/{templateId}")]
    public async Task<IActionResult> GetBriefTemplate(string templateId)
    {
        try
        {
            if (_sampleAssets == null)
                return NotFound(new { error = "Sample assets service not available" });

            var template = await _sampleAssets.GetBriefTemplateAsync(templateId);
            if (template == null)
                return NotFound(new { error = "Template not found" });

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get brief template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Failed to retrieve brief template" });
        }
    }

    /// <summary>
    /// Get all voice configurations
    /// </summary>
    [HttpGet("samples/voice-configs")]
    public async Task<IActionResult> GetVoiceConfigurations([FromQuery] string? provider = null)
    {
        try
        {
            if (_sampleAssets == null)
                return NotFound(new { error = "Sample assets service not available" });

            var configs = string.IsNullOrWhiteSpace(provider)
                ? await _sampleAssets.GetVoiceConfigurationsAsync()
                : await _sampleAssets.GetVoiceConfigurationsByProviderAsync(provider);

            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get voice configurations");
            return StatusCode(500, new { error = "Failed to retrieve voice configurations" });
        }
    }

    /// <summary>
    /// Get all sample images
    /// </summary>
    [HttpGet("samples/images")]
    public async Task<IActionResult> GetSampleImages()
    {
        try
        {
            if (_sampleAssets == null)
                return NotFound(new { error = "Sample assets service not available" });

            var images = await _sampleAssets.GetSampleImagesAsync();
            return Ok(images);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sample images");
            return StatusCode(500, new { error = "Failed to retrieve sample images" });
        }
    }

    /// <summary>
    /// Get all sample audio
    /// </summary>
    [HttpGet("samples/audio")]
    public async Task<IActionResult> GetSampleAudio()
    {
        try
        {
            if (_sampleAssets == null)
                return NotFound(new { error = "Sample assets service not available" });

            var audio = await _sampleAssets.GetSampleAudioAsync();
            return Ok(audio);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sample audio");
            return StatusCode(500, new { error = "Failed to retrieve sample audio" });
        }
    }

    private Core.Models.Assets.AssetType DetermineAssetType(string fileName, string? typeParam)
    {
        if (!string.IsNullOrWhiteSpace(typeParam) && Enum.TryParse<Core.Models.Assets.AssetType>(typeParam, true, out var parsedType))
            return parsedType;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => Core.Models.Assets.AssetType.Image,
            ".mp4" or ".avi" or ".mov" or ".mkv" or ".webm" => Core.Models.Assets.AssetType.Video,
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac" => Core.Models.Assets.AssetType.Audio,
            _ => Core.Models.Assets.AssetType.Image
        };
    }
}

/// <summary>
/// Request to download a stock image
/// </summary>
public record StockImageDownloadRequest(
    string ImageUrl,
    string? Source = null,
    string? Photographer = null);

/// <summary>
/// Request to create a collection
/// </summary>
public record CreateCollectionRequest(
    string Name,
    string? Description = null,
    string? Color = null);
