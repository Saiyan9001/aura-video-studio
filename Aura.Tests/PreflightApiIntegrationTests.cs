using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Aura.Core.Hardware;
using Aura.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aura.Tests;

public class PreflightApiIntegrationTests
{
    [Fact]
    public async Task PreflightEndpoint_ReturnsOk()
    {
        // Arrange
        using var host = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/api/preflight/run", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PreflightEndpoint_ReturnsValidJson()
    {
        // Arrange
        using var host = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/api/preflight/run", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        Assert.True(result.TryGetProperty("correlationId", out _));
        Assert.True(result.TryGetProperty("ok", out _));
        Assert.True(result.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() > 0);
    }

    [Fact]
    public async Task PreflightEndpoint_ReturnsChecksWithRequiredFields()
    {
        // Arrange
        using var host = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/api/preflight/run", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        Assert.True(result.TryGetProperty("checks", out var checks));
        
        foreach (var check in checks.EnumerateArray())
        {
            Assert.True(check.TryGetProperty("name", out _));
            Assert.True(check.TryGetProperty("ok", out _));
            Assert.True(check.TryGetProperty("message", out _));
        }
    }

    [Fact]
    public async Task PreflightEndpoint_IncludesAllRequiredChecks()
    {
        // Arrange
        using var host = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/api/preflight/run", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        Assert.True(result.TryGetProperty("checks", out var checks));
        
        var checkNames = new System.Collections.Generic.List<string>();
        foreach (var check in checks.EnumerateArray())
        {
            if (check.TryGetProperty("name", out var nameElement))
            {
                checkNames.Add(nameElement.GetString() ?? "");
            }
        }

        // Verify we have all required checks
        Assert.Contains("Provider Selection Coherence", checkNames);
        Assert.Contains("API Keys", checkNames);
        Assert.Contains("Ollama Reachability", checkNames);
        Assert.Contains("Stable Diffusion Reachability", checkNames);
        Assert.Contains("FFmpeg Presence", checkNames);
        Assert.Contains("NVENC Support", checkNames);
        Assert.Contains("Disk Space", checkNames);
        Assert.Contains("Offline Mode Consistency", checkNames);
    }

    [Fact]
    public async Task PreflightEndpoint_ReturnsTimestamp()
    {
        // Arrange
        using var host = await CreateTestHost();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/api/preflight/run", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        Assert.True(result.TryGetProperty("timestamp", out var timestamp));
        Assert.True(DateTime.TryParse(timestamp.GetString(), out _));
    }

    private async Task<IHost> CreateTestHost()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        // Register required services
                        services.AddSingleton(sp => 
                        {
                            var logger = sp.GetRequiredService<ILogger<HardwareDetector>>();
                            return new HardwareDetector(logger);
                        });
                        services.AddSingleton(sp => 
                        {
                            var logger = sp.GetRequiredService<ILogger<ProviderSettings>>();
                            return new ProviderSettings(logger);
                        });
                        services.AddHttpClient();
                        services.AddSingleton<PreflightService>(sp =>
                        {
                            var logger = sp.GetRequiredService<ILogger<PreflightService>>();
                            var hardwareDetector = sp.GetRequiredService<HardwareDetector>();
                            var providerSettings = sp.GetRequiredService<ProviderSettings>();
                            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                            return new PreflightService(logger, hardwareDetector, providerSettings, httpClientFactory);
                        });

                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            var apiGroup = endpoints.MapGroup("/api");
                            
                            apiGroup.MapPost("/preflight/run", async (PreflightService preflightService) =>
                            {
                                try
                                {
                                    var result = await preflightService.RunPreflightChecksAsync();
                                    return Results.Ok(result);
                                }
                                catch (Exception)
                                {
                                    return Results.Problem("Error running preflight checks", statusCode: 500);
                                }
                            });
                        });
                    });
            });

        var host = await builder.StartAsync();
        return host;
    }
}
