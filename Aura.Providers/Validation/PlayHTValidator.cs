using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Security;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

public class PlayHTValidator : IProviderValidator
{
    private readonly ILogger<PlayHTValidator> _logger;
    private readonly IKeyStore _keyStore;
    private readonly HttpClient _httpClient;

    public string ProviderName => "PlayHT";
    public bool IsCloudProvider => true;

    public PlayHTValidator(ILogger<PlayHTValidator> logger, IKeyStore keyStore, HttpClient httpClient)
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
            var apiKey = await _keyStore.GetKeyAsync("playht");
            var userId = await _keyStore.GetKeyAsync("playht_user");
            
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(userId))
            {
                sw.Stop();
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = false,
                    Details = "API key or user ID not configured",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            // Try to list voices
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.play.ht/api/v2/voices");
            request.Headers.Add("Authorization", apiKey);
            request.Headers.Add("X-User-Id", userId);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.SendAsync(request, cts.Token);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = true,
                    Details = "API credentials valid, voices accessible",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            else if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
            {
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = false,
                    Details = $"Invalid API credentials ({response.StatusCode})",
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
            _logger.LogError(ex, "PlayHT validation failed");
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
