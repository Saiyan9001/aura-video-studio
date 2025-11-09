using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Providers;

/// <summary>
/// Service for detecting and managing Stable Diffusion WebUI models
/// </summary>
public class StableDiffusionDetectionService
{
    private readonly ILogger<StableDiffusionDetectionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public StableDiffusionDetectionService(
        ILogger<StableDiffusionDetectionService> logger,
        HttpClient httpClient,
        string baseUrl = "http://127.0.0.1:7860")
    {
        _logger = logger;
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Checks if Stable Diffusion WebUI is running and accessible
    /// </summary>
    public async Task<SDStatus> DetectStableDiffusionAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking Stable Diffusion WebUI availability at {BaseUrl}", _baseUrl);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/cmd-flags", cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Stable Diffusion WebUI is running at {BaseUrl}", _baseUrl);
                
                return new SDStatus(
                    IsRunning: true,
                    BaseUrl: _baseUrl,
                    ErrorMessage: null
                );
            }

            _logger.LogWarning("Stable Diffusion WebUI responded but with status code {StatusCode}", response.StatusCode);
            return new SDStatus(
                IsRunning: false,
                BaseUrl: _baseUrl,
                ErrorMessage: $"Stable Diffusion WebUI responded with status code {response.StatusCode}"
            );
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Stable Diffusion WebUI not responding at {BaseUrl} (timeout)", _baseUrl);
            return new SDStatus(
                IsRunning: false,
                BaseUrl: _baseUrl,
                ErrorMessage: "Stable Diffusion WebUI not responding (timeout)"
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Cannot connect to Stable Diffusion WebUI at {BaseUrl}", _baseUrl);
            return new SDStatus(
                IsRunning: false,
                BaseUrl: _baseUrl,
                ErrorMessage: $"Cannot connect to Stable Diffusion WebUI: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting Stable Diffusion WebUI at {BaseUrl}", _baseUrl);
            return new SDStatus(
                IsRunning: false,
                BaseUrl: _baseUrl,
                ErrorMessage: $"Error detecting Stable Diffusion WebUI: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Lists all available Stable Diffusion models
    /// </summary>
    public async Task<List<SDModel>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching Stable Diffusion models from {BaseUrl}/sdapi/v1/sd-models", _baseUrl);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/sd-models", cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var modelsArray = JsonDocument.Parse(content).RootElement;

            var models = new List<SDModel>();

            if (modelsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var modelElement in modelsArray.EnumerateArray())
                {
                    var title = modelElement.TryGetProperty("title", out var titleProp)
                        ? titleProp.GetString() ?? ""
                        : "";
                    
                    var modelName = modelElement.TryGetProperty("model_name", out var nameProp)
                        ? nameProp.GetString() ?? ""
                        : "";

                    var hash = modelElement.TryGetProperty("hash", out var hashProp)
                        ? hashProp.GetString()
                        : null;

                    var sha256 = modelElement.TryGetProperty("sha256", out var sha256Prop)
                        ? sha256Prop.GetString()
                        : null;

                    var filename = modelElement.TryGetProperty("filename", out var filenameProp)
                        ? filenameProp.GetString()
                        : null;

                    if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(modelName))
                    {
                        models.Add(new SDModel(
                            Title: title,
                            ModelName: modelName,
                            Hash: hash,
                            Sha256: sha256,
                            Filename: filename
                        ));
                    }
                }
            }

            _logger.LogInformation("Found {Count} Stable Diffusion models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Stable Diffusion models");
            return new List<SDModel>();
        }
    }

    /// <summary>
    /// Gets the currently selected model
    /// </summary>
    public async Task<string?> GetCurrentModelAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching current Stable Diffusion model");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/options", cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var options = JsonDocument.Parse(content).RootElement;

            if (options.TryGetProperty("sd_model_checkpoint", out var modelProp))
            {
                var currentModel = modelProp.GetString();
                _logger.LogInformation("Current Stable Diffusion model: {Model}", currentModel);
                return currentModel;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current Stable Diffusion model");
            return null;
        }
    }

    /// <summary>
    /// Sets the active Stable Diffusion model
    /// </summary>
    public async Task<bool> SetCurrentModelAsync(string modelName, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Setting Stable Diffusion model to: {ModelName}", modelName);

            var requestBody = new { sd_model_checkpoint = modelName };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await _httpClient.PostAsync($"{_baseUrl}/sdapi/v1/options", content, cts.Token);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully set Stable Diffusion model to: {ModelName}", modelName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Stable Diffusion model to: {ModelName}", modelName);
            return false;
        }
    }

    /// <summary>
    /// Gets system information from Stable Diffusion WebUI
    /// </summary>
    public async Task<SDSystemInfo?> GetSystemInfoAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching Stable Diffusion system info");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/memory", cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var memory = JsonDocument.Parse(content).RootElement;

            var ramTotal = memory.TryGetProperty("ram", out var ramProp) &&
                          ramProp.TryGetProperty("total", out var totalProp)
                ? totalProp.GetInt64()
                : 0;

            var ramUsed = memory.TryGetProperty("ram", out var ramProp2) &&
                         ramProp2.TryGetProperty("used", out var usedProp)
                ? usedProp.GetInt64()
                : 0;

            var cudaSystemRam = memory.TryGetProperty("cuda", out var cudaProp) &&
                               cudaProp.TryGetProperty("system", out var systemProp) &&
                               systemProp.TryGetProperty("total", out var cudaTotalProp)
                ? cudaTotalProp.GetInt64()
                : 0;

            return new SDSystemInfo(
                RamTotalBytes: ramTotal,
                RamUsedBytes: ramUsed,
                CudaSystemRamBytes: cudaSystemRam
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Stable Diffusion system info");
            return null;
        }
    }
}

/// <summary>
/// Status of Stable Diffusion WebUI
/// </summary>
public record SDStatus(
    bool IsRunning,
    string BaseUrl,
    string? ErrorMessage
);

/// <summary>
/// Information about a Stable Diffusion model
/// </summary>
public record SDModel(
    string Title,
    string ModelName,
    string? Hash,
    string? Sha256,
    string? Filename
);

/// <summary>
/// System information from Stable Diffusion WebUI
/// </summary>
public record SDSystemInfo(
    long RamTotalBytes,
    long RamUsedBytes,
    long CudaSystemRamBytes
);
