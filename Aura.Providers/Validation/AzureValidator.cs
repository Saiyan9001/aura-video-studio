using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Security;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

public class AzureValidator : IProviderValidator
{
    private readonly ILogger<AzureValidator> _logger;
    private readonly IKeyStore _keyStore;
    private readonly HttpClient _httpClient;

    public string ProviderName => "Azure";
    public bool IsCloudProvider => true;

    public AzureValidator(ILogger<AzureValidator> logger, IKeyStore keyStore, HttpClient httpClient)
    {
        _logger = logger;
        _keyStore = keyStore;
        _httpClient = httpClient;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var apiKey = await _keyStore.GetKeyAsync("azure");
            var endpoint = await _keyStore.GetKeyAsync("azure_endpoint");
            
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
            {
                sw.Stop();
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = false,
                    Details = "API key or endpoint not configured",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            // Try to access the endpoint with API key
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("api-key", apiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.SendAsync(request, cts.Token);
            sw.Stop();

            if (response.IsSuccessStatusCode || (int)response.StatusCode == 404) // 404 is ok, means endpoint is reachable
            {
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = true,
                    Details = "Endpoint reachable with valid credentials",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            else if ((int)response.StatusCode == 401)
            {
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = false,
                    Details = "Invalid API key (401 Unauthorized)",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            else
            {
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = false,
                    Details = $"API error: {response.StatusCode}",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new ValidationResult
            {
                Name = ProviderName,
                Ok = false,
                Details = "Validation timed out (10s)",
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Azure validation failed");
            return new ValidationResult
            {
                Name = ProviderName,
                Ok = false,
                Details = $"Error: {ex.Message}",
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }
}
