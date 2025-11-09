using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Setup;

public record ProviderAvailabilityResult(
    string ProviderName,
    string ProviderType,
    bool IsAvailable,
    bool IsReachable,
    string? Status,
    int? Latency,
    string? ErrorMessage
);

public record ProviderAvailabilityReport(
    DateTime Timestamp,
    List<ProviderAvailabilityResult> Providers,
    bool OllamaAvailable,
    bool StableDiffusionAvailable,
    bool DatabaseAvailable,
    bool NetworkConnected
);

public class ProviderAvailabilityService
{
    private readonly ILogger<ProviderAvailabilityService> _logger;
    private readonly HttpClient? _httpClient;

    public ProviderAvailabilityService(
        ILogger<ProviderAvailabilityService> logger,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<ProviderAvailabilityReport> CheckAllProvidersAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting provider availability check");

        var providers = new List<ProviderAvailabilityResult>();

        // Check Ollama service
        var ollamaAvailable = await CheckOllamaServiceAsync(ct).ConfigureAwait(false);
        if (ollamaAvailable)
        {
            providers.Add(new ProviderAvailabilityResult(
                ProviderName: "Ollama",
                ProviderType: "LLM",
                IsAvailable: true,
                IsReachable: true,
                Status: "Available",
                Latency: null,
                ErrorMessage: null
            ));
        }

        // Check Stable Diffusion
        var stableDiffusionAvailable = await CheckStableDiffusionAsync(ct).ConfigureAwait(false);
        if (stableDiffusionAvailable)
        {
            providers.Add(new ProviderAvailabilityResult(
                ProviderName: "Stable Diffusion",
                ProviderType: "Image",
                IsAvailable: true,
                IsReachable: true,
                Status: "Available",
                Latency: null,
                ErrorMessage: null
            ));
        }

        // Check database (using in-memory or file-based storage, so always available)
        var databaseAvailable = true;

        // Check network connectivity
        var networkConnected = await CheckNetworkConnectivityAsync(ct).ConfigureAwait(false);

        return new ProviderAvailabilityReport(
            Timestamp: DateTime.UtcNow,
            Providers: providers,
            OllamaAvailable: ollamaAvailable,
            StableDiffusionAvailable: stableDiffusionAvailable,
            DatabaseAvailable: databaseAvailable,
            NetworkConnected: networkConnected
        );
    }

    private async Task<bool> CheckOllamaServiceAsync(CancellationToken ct)
    {
        try
        {
            if (_httpClient == null)
            {
                return false;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await _httpClient.GetAsync("http://localhost:11434/api/tags", cts.Token).ConfigureAwait(false);
            var isAvailable = response.IsSuccessStatusCode;
            
            _logger.LogInformation("Ollama service check: {Available}", isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama service not reachable");
            return false;
        }
    }

    private async Task<bool> CheckStableDiffusionAsync(CancellationToken ct)
    {
        try
        {
            if (_httpClient == null)
            {
                return false;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var endpoints = new[]
            {
                "http://localhost:7860",
                "http://localhost:7861",
                "http://127.0.0.1:7860",
                "http://127.0.0.1:7861"
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync(endpoint, cts.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Stable Diffusion found at {Endpoint}", endpoint);
                        return true;
                    }
                }
                catch
                {
                    continue;
                }
            }

            _logger.LogDebug("Stable Diffusion not found at any known endpoint");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stable Diffusion check failed");
            return false;
        }
    }

    private async Task<bool> CheckNetworkConnectivityAsync(CancellationToken ct)
    {
        try
        {
            if (_httpClient == null)
            {
                return false;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync("https://www.google.com", cts.Token).ConfigureAwait(false);
            var connected = response.IsSuccessStatusCode;
            
            _logger.LogInformation("Network connectivity: {Connected}", connected);
            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Network connectivity check failed");
            return false;
        }
    }
}
