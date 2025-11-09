using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for TTS provider management and voice synthesis operations
/// </summary>
[ApiController]
[Route("api/tts")]
public class TtsController : ControllerBase
{
    private readonly ILogger<TtsController> _logger;
    private readonly TtsProviderFactory _providerFactory;

    public TtsController(
        ILogger<TtsController> logger,
        TtsProviderFactory providerFactory)
    {
        _logger = logger;
        _providerFactory = providerFactory;
    }

    /// <summary>
    /// Lists all available TTS providers with their status
    /// </summary>
    /// <returns>List of TTS provider information</returns>
    [HttpGet("providers")]
    public IActionResult ListProviders()
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] Listing available TTS providers", correlationId);

            var providers = _providerFactory.CreateAvailableProviders();
            
            var providerInfoList = providers.Select(p => new
            {
                name = p.Key,
                type = GetProviderType(p.Key),
                tier = GetProviderTier(p.Key),
                requiresApiKey = RequiresApiKey(p.Key),
                supportsOffline = SupportsOffline(p.Key),
                description = GetProviderDescription(p.Key)
            }).ToList();

            return Ok(new
            {
                success = true,
                providers = providerInfoList,
                totalCount = providerInfoList.Count,
                correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to list TTS providers", correlationId);
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to list TTS providers",
                details = ex.Message,
                correlationId
            });
        }
    }

    /// <summary>
    /// Lists available voices for a specific TTS provider
    /// </summary>
    /// <param name="provider">Provider name (e.g., "ElevenLabs", "OpenAI", "EdgeTTS")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of voice names</returns>
    [HttpGet("voices")]
    public async Task<IActionResult> ListVoices(
        [FromQuery] [Required] string provider,
        CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] Fetching voices for provider: {Provider}", 
                correlationId, provider);

            var ttsProvider = _providerFactory.TryCreateProvider(provider);
            
            if (ttsProvider == null)
            {
                return NotFound(new
                {
                    success = false,
                    error = $"Provider '{provider}' not found or not available",
                    correlationId
                });
            }

            var voices = await ttsProvider.GetAvailableVoicesAsync();

            return Ok(new
            {
                success = true,
                provider,
                voices = voices,
                count = voices.Count,
                correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to fetch voices for provider: {Provider}", 
                correlationId, provider);
            return StatusCode(500, new
            {
                success = false,
                error = $"Failed to fetch voices for provider '{provider}'",
                details = ex.Message,
                correlationId
            });
        }
    }

    /// <summary>
    /// Generates a short preview of a voice
    /// </summary>
    /// <param name="request">Preview request with provider, voice, and sample text</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Path to generated preview audio</returns>
    [HttpPost("preview")]
    public async Task<IActionResult> GeneratePreview(
        [FromBody] TtsPreviewRequest request,
        CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.Provider))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Provider name is required",
                    correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.Voice))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Voice name is required",
                    correlationId
                });
            }

            var sampleText = string.IsNullOrWhiteSpace(request.SampleText)
                ? "Hello, this is a sample of my voice. How does it sound?"
                : request.SampleText;

            if (sampleText.Length > 500)
            {
                sampleText = sampleText[..500];
                _logger.LogWarning("[{CorrelationId}] Sample text truncated to 500 characters", correlationId);
            }

            _logger.LogInformation("[{CorrelationId}] Generating voice preview: Provider={Provider}, Voice={Voice}", 
                correlationId, request.Provider, request.Voice);

            var ttsProvider = _providerFactory.TryCreateProvider(request.Provider);
            
            if (ttsProvider == null)
            {
                return NotFound(new
                {
                    success = false,
                    error = $"Provider '{request.Provider}' not found or not available",
                    correlationId
                });
            }

            var scriptLine = new ScriptLine(
                SceneIndex: 0,
                Text: sampleText,
                Start: TimeSpan.Zero,
                Duration: TimeSpan.FromSeconds(5)
            );

            var voiceSpec = new VoiceSpec(
                VoiceName: request.Voice,
                Rate: request.Speed ?? 1.0,
                Pitch: request.Pitch ?? 0.0,
                Pause: PauseStyle.Natural
            );

            var audioPath = await ttsProvider.SynthesizeAsync(new[] { scriptLine }, voiceSpec, ct);

            return Ok(new
            {
                success = true,
                audioPath,
                provider = request.Provider,
                voice = request.Voice,
                text = sampleText,
                correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to generate voice preview", correlationId);
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to generate voice preview",
                details = ex.Message,
                correlationId
            });
        }
    }

    /// <summary>
    /// Checks the availability and health status of TTS providers
    /// </summary>
    /// <param name="provider">Optional specific provider to check (checks all if not specified)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Provider status information</returns>
    [HttpGet("status")]
    public async Task<IActionResult> CheckStatus(
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] Checking TTS provider status: {Provider}", 
                correlationId, provider ?? "all");

            var providers = _providerFactory.CreateAvailableProviders();

            if (!string.IsNullOrWhiteSpace(provider))
            {
                if (!providers.ContainsKey(provider))
                {
                    return NotFound(new
                    {
                        success = false,
                        error = $"Provider '{provider}' not found",
                        correlationId
                    });
                }

                var status = await CheckProviderHealthAsync(provider, providers[provider], ct);
                
                return Ok(new
                {
                    success = true,
                    provider,
                    status,
                    correlationId
                });
            }

            var statusList = new List<object>();
            
            foreach (var (name, ttsProvider) in providers)
            {
                try
                {
                    var status = await CheckProviderHealthAsync(name, ttsProvider, ct);
                    statusList.Add(status);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{CorrelationId}] Failed to check status for provider: {Provider}", 
                        correlationId, name);
                    statusList.Add(new
                    {
                        name,
                        isAvailable = false,
                        error = ex.Message
                    });
                }
            }

            return Ok(new
            {
                success = true,
                providers = statusList,
                totalCount = statusList.Count,
                correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to check TTS provider status", correlationId);
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to check TTS provider status",
                details = ex.Message,
                correlationId
            });
        }
    }

    private async Task<object> CheckProviderHealthAsync(string name, ITtsProvider provider, CancellationToken ct)
    {
        try
        {
            var voices = await provider.GetAvailableVoicesAsync();
            var voiceCount = voices?.Count ?? 0;
            
            return new
            {
                name,
                isAvailable = voiceCount > 0,
                voiceCount,
                tier = GetProviderTier(name),
                requiresApiKey = RequiresApiKey(name),
                supportsOffline = SupportsOffline(name)
            };
        }
        catch (Exception ex)
        {
            return new
            {
                name,
                isAvailable = false,
                voiceCount = 0,
                error = ex.Message
            };
        }
    }

    private static string GetProviderType(string providerName)
    {
        return providerName switch
        {
            "ElevenLabs" or "OpenAI" or "PlayHT" or "Azure" => "Cloud",
            "EdgeTTS" => "Cloud-Free",
            "Piper" or "Mimic3" => "Local",
            "Windows" => "System",
            "Null" => "Fallback",
            _ => "Unknown"
        };
    }

    private static string GetProviderTier(string providerName)
    {
        return providerName switch
        {
            "ElevenLabs" or "OpenAI" or "PlayHT" or "Azure" => "Pro",
            "EdgeTTS" or "Piper" or "Mimic3" or "Windows" => "Free",
            "Null" => "Fallback",
            _ => "Unknown"
        };
    }

    private static bool RequiresApiKey(string providerName)
    {
        return providerName switch
        {
            "ElevenLabs" or "OpenAI" or "PlayHT" or "Azure" => true,
            _ => false
        };
    }

    private static bool SupportsOffline(string providerName)
    {
        return providerName switch
        {
            "Piper" or "Mimic3" or "Windows" or "Null" => true,
            _ => false
        };
    }

    private static string GetProviderDescription(string providerName)
    {
        return providerName switch
        {
            "ElevenLabs" => "Premium TTS with ultra-realistic voices and voice cloning",
            "OpenAI" => "High-quality TTS with streaming support (tts-1, tts-1-hd)",
            "PlayHT" => "Professional TTS with voice cloning and emotion control",
            "Azure" => "Enterprise-grade TTS from Microsoft Azure Cognitive Services",
            "EdgeTTS" => "Free Microsoft Edge TTS with good quality voices",
            "Piper" => "Fast offline neural TTS with multiple language support",
            "Mimic3" => "Open-source offline neural TTS with high-quality voices",
            "Windows" => "System TTS using Windows Speech API (SAPI)",
            "Null" => "Fallback that generates silence (used when all else fails)",
            _ => "Unknown TTS provider"
        };
    }
}

/// <summary>
/// Request model for generating voice previews
/// </summary>
public record TtsPreviewRequest
{
    [Required]
    public string Provider { get; init; } = string.Empty;
    
    [Required]
    public string Voice { get; init; } = string.Empty;
    
    public string? SampleText { get; init; }
    
    public double? Speed { get; init; }
    
    public double? Pitch { get; init; }
}
