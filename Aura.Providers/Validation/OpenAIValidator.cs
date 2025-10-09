using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Security;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

public class OpenAIValidator : IProviderValidator
{
    private readonly ILogger<OpenAIValidator> _logger;
    private readonly IKeyStore _keyStore;
    private readonly HttpClient _httpClient;

    public string ProviderName => "OpenAI";
    public bool IsCloudProvider => true;

    public OpenAIValidator(ILogger<OpenAIValidator> logger, IKeyStore keyStore, HttpClient httpClient)
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
            var apiKey = await _keyStore.GetKeyAsync("openai");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                sw.Stop();
                return new ValidationResult
                {
                    Name = ProviderName,
                    Ok = false,
                    Details = "API key not configured",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            // Try to list models as a simple validation
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

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
                    Details = "API key valid, models accessible",
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
            _logger.LogError(ex, "OpenAI validation failed");
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
