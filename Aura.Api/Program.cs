using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Core.Services;
using Aura.Providers.Llm;
using Aura.Providers.Tts;
using Aura.Providers.Video;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON options to handle string enum conversion
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/aura-api-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register core services
builder.Services.AddSingleton<HardwareDetector>();
builder.Services.AddSingleton<Aura.Core.Configuration.ProviderSettings>();
builder.Services.AddSingleton<ILlmProvider, RuleBasedLlmProvider>();
builder.Services.AddSingleton<ITtsProvider, WindowsTtsProvider>();
builder.Services.AddSingleton<IVideoComposer>(sp => 
{
    var logger = sp.GetRequiredService<ILogger<FfmpegVideoComposer>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var ffmpegPath = providerSettings.GetFfmpegPath();
    var outputDirectory = providerSettings.GetOutputDirectory();
    return new FfmpegVideoComposer(logger, ffmpegPath, outputDirectory);
});
builder.Services.AddSingleton<VideoOrchestrator>();

// Register DependencyManager
builder.Services.AddHttpClient<Aura.Core.Dependencies.DependencyManager>();
builder.Services.AddSingleton<Aura.Core.Dependencies.DependencyManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.DependencyManager>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var manifestPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "manifest.json");
    var downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "dependencies");
    return new Aura.Core.Dependencies.DependencyManager(logger, httpClient, manifestPath, downloadDirectory);
});

// Register PreflightService
builder.Services.AddSingleton<PreflightService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PreflightService>>();
    var hardwareDetector = sp.GetRequiredService<HardwareDetector>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new PreflightService(logger, hardwareDetector, providerSettings, httpClientFactory);
});

// Configure Kestrel to listen on specific port
builder.WebHost.UseUrls("http://127.0.0.1:5005");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files from wwwroot (must be before routing)
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (Directory.Exists(wwwrootPath))
{
    Log.Information("Serving static files from: {Path}", wwwrootPath);
    
    app.UseDefaultFiles(); // Serve index.html as default file
    app.UseStaticFiles();
}
else
{
    Log.Warning("wwwroot directory not found at: {Path}", wwwrootPath);
    Log.Warning("Static file serving is disabled. Web UI will not be available.");
}

// API endpoints are grouped under /api prefix
var apiGroup = app.MapGroup("/api");

// Health check endpoint
apiGroup.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

// Capabilities endpoint
apiGroup.MapGet("/capabilities", async (HardwareDetector detector) =>
{
    try
    {
        var profile = await detector.DetectSystemAsync();
        return Results.Ok(new
        {
            tier = profile.Tier.ToString(),
            cpu = new { cores = profile.PhysicalCores, threads = profile.LogicalCores },
            ram = new { gb = profile.RamGB },
            gpu = profile.Gpu != null ? new { model = profile.Gpu.Model, vramGB = profile.Gpu.VramGB, vendor = profile.Gpu.Vendor } : null,
            enableNVENC = profile.EnableNVENC,
            enableSD = profile.EnableSD,
            offlineOnly = profile.OfflineOnly
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error detecting capabilities");
        return Results.Problem("Error detecting system capabilities", statusCode: 500);
    }
})
.WithName("GetCapabilities")
.WithOpenApi();

// Plan endpoint - create or update timeline plan
apiGroup.MapPost("/plan", ([FromBody] PlanRequest request) =>
{
    try
    {
        var plan = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(request.TargetDurationMinutes),
            Pacing: request.Pacing,
            Density: request.Density,
            Style: request.Style
        );
        
        return Results.Ok(new { success = true, plan });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating plan");
        return Results.Problem("Error creating plan", statusCode: 500);
    }
})
.WithName("CreatePlan")
.WithOpenApi();

// Script generation endpoint
apiGroup.MapPost("/script", async ([FromBody] ScriptRequest request, ILlmProvider llmProvider, CancellationToken ct) =>
{
    try
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            return Results.Problem(
                detail: "Topic is required",
                statusCode: 400,
                title: "Invalid Brief",
                type: "https://docs.aura.studio/errors/E303");
        }
        
        if (request.TargetDurationMinutes <= 0 || request.TargetDurationMinutes > 120)
        {
            return Results.Problem(
                detail: "Target duration must be between 0 and 120 minutes",
                statusCode: 400,
                title: "Invalid Plan",
                type: "https://docs.aura.studio/errors/E304");
        }
        
        var brief = new Brief(
            Topic: request.Topic,
            Audience: request.Audience,
            Goal: request.Goal,
            Tone: request.Tone,
            Language: request.Language,
            Aspect: request.Aspect
        );
        
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(request.TargetDurationMinutes),
            Pacing: request.Pacing,
            Density: request.Density,
            Style: request.Style
        );
        
        Log.Information("Generating script for topic: {Topic}, duration: {Duration} min", request.Topic, request.TargetDurationMinutes);
        var script = await llmProvider.DraftScriptAsync(brief, planSpec, ct);
        
        // Validate script is not empty
        if (string.IsNullOrWhiteSpace(script))
        {
            Log.Error("Script generation returned empty result");
            return Results.Problem(
                detail: "Script generation returned empty result. Provider may have failed.",
                statusCode: 500,
                title: "Script Generation Failed",
                type: "https://docs.aura.studio/errors/E302");
        }
        
        Log.Information("Script generated successfully: {Length} characters", script.Length);
        return Results.Ok(new { success = true, script });
    }
    catch (ArgumentException ex)
    {
        Log.Error(ex, "Invalid argument for script generation");
        return Results.Problem(
            detail: ex.Message,
            statusCode: 400,
            title: "Invalid Request",
            type: "https://docs.aura.studio/errors/E303");
    }
    catch (TaskCanceledException)
    {
        Log.Warning("Script generation was cancelled");
        return Results.Problem(
            detail: "Script generation was cancelled",
            statusCode: 408,
            title: "Request Timeout",
            type: "https://docs.aura.studio/errors/E301");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating script: {Message}", ex.Message);
        return Results.Problem(
            detail: $"Error generating script: {ex.Message}",
            statusCode: 500,
            title: "Script Provider Failed",
            type: "https://docs.aura.studio/errors/E300");
    }
})
.WithName("GenerateScript")
.WithOpenApi();

// TTS endpoint
apiGroup.MapPost("/tts", async ([FromBody] TtsRequest request, ITtsProvider ttsProvider, CancellationToken ct) =>
{
    try
    {
        var lines = request.Lines.Select(l => new ScriptLine(
            SceneIndex: l.SceneIndex,
            Text: l.Text,
            Start: TimeSpan.FromSeconds(l.StartSeconds),
            Duration: TimeSpan.FromSeconds(l.DurationSeconds)
        )).ToList();
        
        var voiceSpec = new VoiceSpec(
            VoiceName: request.VoiceName,
            Rate: request.Rate,
            Pitch: request.Pitch,
            Pause: request.PauseStyle
        );
        
        var result = await ttsProvider.SynthesizeAsync(lines, voiceSpec, ct);
        
        return Results.Ok(new { success = true, audioPath = result });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error synthesizing audio");
        return Results.Problem("Error synthesizing audio", statusCode: 500);
    }
})
.WithName("SynthesizeAudio")
.WithOpenApi();

// Downloads manifest endpoint
apiGroup.MapGet("/downloads/manifest", async (Aura.Core.Dependencies.DependencyManager depManager) =>
{
    try
    {
        var manifest = await depManager.LoadManifestAsync();
        return Results.Ok(manifest);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error reading manifest");
        return Results.Problem("Error reading manifest", statusCode: 500);
    }
})
.WithName("GetManifest")
.WithOpenApi();

// Check if component is installed
apiGroup.MapGet("/downloads/{component}/status", async (string component, Aura.Core.Dependencies.DependencyManager depManager) =>
{
    try
    {
        var isInstalled = await depManager.IsComponentInstalledAsync(component);
        return Results.Ok(new { component, isInstalled });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error checking component status");
        return Results.Problem("Error checking component status", statusCode: 500);
    }
})
.WithName("CheckComponentStatus")
.WithOpenApi();

// Download and install component
apiGroup.MapPost("/downloads/{component}/install", async (string component, Aura.Core.Dependencies.DependencyManager depManager, CancellationToken ct) =>
{
    try
    {
        // Create progress handler
        var progress = new Progress<Aura.Core.Dependencies.DownloadProgress>(p =>
        {
            Log.Information("Download progress: {Component} - {Percent}%", component, p.PercentComplete);
        });

        await depManager.DownloadComponentAsync(component, progress, ct);
        
        return Results.Ok(new { success = true, message = $"{component} installed successfully" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error installing component");
        return Results.Problem($"Error installing {component}", statusCode: 500);
    }
})
.WithName("InstallComponent")
.WithOpenApi();

// Settings endpoints
apiGroup.MapPost("/settings/save", ([FromBody] Dictionary<string, object> settings) =>
{
    try
    {
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error saving settings");
        return Results.Problem("Error saving settings", statusCode: 500);
    }
})
.WithName("SaveSettings")
.WithOpenApi();

apiGroup.MapGet("/settings/load", () =>
{
    try
    {
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "settings.json");
        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return Results.Ok(settings);
        }
        return Results.Ok(new Dictionary<string, object>());
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error loading settings");
        return Results.Problem("Error loading settings", statusCode: 500);
    }
})
.WithName("LoadSettings")
.WithOpenApi();

// Compose/Render endpoints - stub implementations for UI development
var renderJobs = new Dictionary<string, RenderJobDto>();

apiGroup.MapPost("/compose", ([FromBody] ComposeRequest request) =>
{
    try
    {
        var jobId = Guid.NewGuid().ToString();
        renderJobs[jobId] = new RenderJobDto(
            Id: jobId,
            Status: "queued",
            Progress: 0,
            OutputPath: null,
            CreatedAt: DateTime.UtcNow
        );
        
        return Results.Ok(new { success = true, jobId });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error composing timeline");
        return Results.Problem("Error composing timeline", statusCode: 500);
    }
})
.WithName("ComposeTimeline")
.WithOpenApi();

apiGroup.MapPost("/render", ([FromBody] RenderRequest request) =>
{
    try
    {
        var jobId = Guid.NewGuid().ToString();
        renderJobs[jobId] = new RenderJobDto(
            Id: jobId,
            Status: "queued",
            Progress: 0,
            OutputPath: null,
            CreatedAt: DateTime.UtcNow
        );
        
        return Results.Ok(new { success = true, jobId });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error starting render");
        return Results.Problem("Error starting render", statusCode: 500);
    }
})
.WithName("StartRender")
.WithOpenApi();

apiGroup.MapGet("/render/{id}/progress", (string id) =>
{
    if (!renderJobs.ContainsKey(id))
    {
        return Results.NotFound(new { error = "Render job not found" });
    }
    
    var job = renderJobs[id];
    return Results.Ok(new 
    { 
        id = job.Id,
        status = job.Status,
        progress = job.Progress,
        outputPath = job.OutputPath,
        createdAt = job.CreatedAt
    });
})
.WithName("GetRenderProgress")
.WithOpenApi();

apiGroup.MapPost("/render/{id}/cancel", (string id) =>
{
    if (!renderJobs.ContainsKey(id))
    {
        return Results.NotFound(new { error = "Render job not found" });
    }
    
    renderJobs[id] = renderJobs[id] with { Status = "cancelled" };
    return Results.Ok(new { success = true });
})
.WithName("CancelRender")
.WithOpenApi();

apiGroup.MapGet("/queue", () =>
{
    try
    {
        var jobs = renderJobs.Values.OrderByDescending(j => j.CreatedAt).ToList();
        return Results.Ok(new { jobs });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error retrieving queue");
        return Results.Problem("Error retrieving queue", statusCode: 500);
    }
})
.WithName("GetRenderQueue")
.WithOpenApi();

apiGroup.MapGet("/logs/stream", async (HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    
    try
    {
        // Simple log streaming - send a test message
        var message = $"data: {{\"timestamp\":\"{DateTime.UtcNow:O}\",\"level\":\"INFO\",\"message\":\"Log stream connected\"}}\n\n";
        await context.Response.WriteAsync(message);
        await context.Response.Body.FlushAsync();
        
        // Keep connection alive
        await Task.Delay(Timeout.Infinite, context.RequestAborted);
    }
    catch (OperationCanceledException)
    {
        // Client disconnected
    }
})
.WithName("StreamLogs")
.WithOpenApi();

apiGroup.MapPost("/probes/run", async (HardwareDetector detector) =>
{
    try
    {
        await detector.RunHardwareProbeAsync();
        var profile = await detector.DetectSystemAsync();
        return Results.Ok(new { success = true, profile });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error running probes");
        return Results.Problem("Error running hardware probes", statusCode: 500);
    }
})
.WithName("RunProbes")
.WithOpenApi();

apiGroup.MapGet("/profiles/list", () =>
{
    try
    {
        var profiles = new[]
        {
            new { name = "Free-Only", description = "Uses only free providers (no API keys required)" },
            new { name = "Balanced Mix", description = "Pro providers with free fallbacks" },
            new { name = "Pro-Max", description = "All pro providers (requires API keys)" }
        };
        return Results.Ok(new { profiles });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error listing profiles");
        return Results.Problem("Error listing profiles", statusCode: 500);
    }
})
.WithName("ListProfiles")
.WithOpenApi();

apiGroup.MapPost("/profiles/apply", ([FromBody] ApplyProfileRequest request) =>
{
    try
    {
        // Store profile selection in settings
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        
        var settings = new Dictionary<string, object> { ["profile"] = request.ProfileName };
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error applying profile");
        return Results.Problem("Error applying profile", statusCode: 500);
    }
})
.WithName("ApplyProfile")
.WithOpenApi();

// API Key Management
apiGroup.MapPost("/apikeys/save", ([FromBody] ApiKeysRequest request) =>
{
    try
    {
        var keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "apikeys.json");
        Directory.CreateDirectory(Path.GetDirectoryName(keysPath)!);
        
        // In production, these should be encrypted using DPAPI or similar
        var keys = new Dictionary<string, string>
        {
            ["openai"] = request.OpenAiKey ?? "",
            ["elevenlabs"] = request.ElevenLabsKey ?? "",
            ["pexels"] = request.PexelsKey ?? "",
            ["stabilityai"] = request.StabilityAiKey ?? ""
        };
        
        File.WriteAllText(keysPath, JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }));
        
        return Results.Ok(new { success = true, message = "API keys saved successfully" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error saving API keys");
        return Results.Problem("Error saving API keys", statusCode: 500);
    }
})
.WithName("SaveApiKeys")
.WithOpenApi();

apiGroup.MapGet("/apikeys/load", () =>
{
    try
    {
        var keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "apikeys.json");
        if (File.Exists(keysPath))
        {
            var json = File.ReadAllText(keysPath);
            var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            
            // Return masked keys (only show first 8 characters)
            var maskedKeys = keys?.ToDictionary(
                k => k.Key,
                k => string.IsNullOrEmpty(k.Value) ? "" : k.Value.Substring(0, Math.Min(8, k.Value.Length)) + "..."
            );
            
            return Results.Ok(maskedKeys);
        }
        return Results.Ok(new Dictionary<string, string>
        {
            ["openai"] = "",
            ["elevenlabs"] = "",
            ["pexels"] = "",
            ["stabilityai"] = ""
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error loading API keys");
        return Results.Problem("Error loading API keys", statusCode: 500);
    }
})
.WithName("LoadApiKeys")
.WithOpenApi();

// Local Provider Paths Configuration
apiGroup.MapPost("/providers/paths/save", ([FromBody] ProviderPathsRequest request) =>
{
    try
    {
        var pathsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "provider-paths.json");
        Directory.CreateDirectory(Path.GetDirectoryName(pathsConfigPath)!);
        
        var paths = new Dictionary<string, object>
        {
            ["stableDiffusionUrl"] = request.StableDiffusionUrl ?? "http://127.0.0.1:7860",
            ["ollamaUrl"] = request.OllamaUrl ?? "http://127.0.0.1:11434",
            ["ffmpegPath"] = request.FfmpegPath ?? "",
            ["ffprobePath"] = request.FfprobePath ?? "",
            ["outputDirectory"] = request.OutputDirectory ?? ""
        };
        
        File.WriteAllText(pathsConfigPath, JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true }));
        
        return Results.Ok(new { success = true, message = "Provider paths saved successfully" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error saving provider paths");
        return Results.Problem("Error saving provider paths", statusCode: 500);
    }
})
.WithName("SaveProviderPaths")
.WithOpenApi();

apiGroup.MapGet("/providers/paths/load", () =>
{
    try
    {
        var pathsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "provider-paths.json");
        if (File.Exists(pathsConfigPath))
        {
            var json = File.ReadAllText(pathsConfigPath);
            var paths = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return Results.Ok(paths);
        }
        
        // Return defaults
        return Results.Ok(new Dictionary<string, object>
        {
            ["stableDiffusionUrl"] = "http://127.0.0.1:7860",
            ["ollamaUrl"] = "http://127.0.0.1:11434",
            ["ffmpegPath"] = "",
            ["ffprobePath"] = "",
            ["outputDirectory"] = ""
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error loading provider paths");
        return Results.Problem("Error loading provider paths", statusCode: 500);
    }
})
.WithName("LoadProviderPaths")
.WithOpenApi();

// Test provider connections
apiGroup.MapPost("/providers/test/{provider}", async (string provider, [FromBody] ProviderTestRequest request) =>
{
    try
    {
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);
        
        switch (provider.ToLower())
        {
            case "stablediffusion":
                try
                {
                    var sdUrl = request.Url ?? "http://127.0.0.1:7860";
                    var response = await httpClient.GetAsync($"{sdUrl}/sdapi/v1/sd-models");
                    if (response.IsSuccessStatusCode)
                    {
                        return Results.Ok(new { success = true, message = "Successfully connected to Stable Diffusion WebUI" });
                    }
                    return Results.Ok(new { success = false, message = $"Stable Diffusion WebUI returned status: {response.StatusCode}" });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new { success = false, message = $"Failed to connect: {ex.Message}" });
                }
                
            case "ollama":
                try
                {
                    var ollamaUrl = request.Url ?? "http://127.0.0.1:11434";
                    var response = await httpClient.GetAsync($"{ollamaUrl}/api/tags");
                    if (response.IsSuccessStatusCode)
                    {
                        return Results.Ok(new { success = true, message = "Successfully connected to Ollama" });
                    }
                    return Results.Ok(new { success = false, message = $"Ollama returned status: {response.StatusCode}" });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new { success = false, message = $"Failed to connect: {ex.Message}" });
                }
                
            case "ffmpeg":
                try
                {
                    var ffmpegPath = request.Path ?? "ffmpeg";
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using var process = System.Diagnostics.Process.Start(processInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0)
                        {
                            var output = await process.StandardOutput.ReadToEndAsync();
                            var versionLine = output.Split('\n').FirstOrDefault(l => l.Contains("ffmpeg version"));
                            return Results.Ok(new { success = true, message = versionLine ?? "FFmpeg found and working" });
                        }
                    }
                    return Results.Ok(new { success = false, message = "FFmpeg returned non-zero exit code" });
                }
                catch (Exception ex)
                {
                    return Results.Ok(new { success = false, message = $"Failed to execute FFmpeg: {ex.Message}" });
                }
                
            default:
                return Results.BadRequest(new { success = false, message = $"Unknown provider: {provider}" });
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error testing provider connection");
        return Results.Problem($"Error testing {provider}", statusCode: 500);
    }
})
.WithName("TestProviderConnection")
.WithOpenApi();

// Preflight checks endpoint
apiGroup.MapPost("/preflight/run", async (PreflightService preflightService) =>
{
    try
    {
        var result = await preflightService.RunPreflightChecksAsync();
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error running preflight checks");
        return Results.Problem("Error running preflight checks", statusCode: 500);
    }
})
.WithName("RunPreflightChecks")
.WithOpenApi();

// Fallback to index.html for client-side routing (must be after all API routes)
if (Directory.Exists(wwwrootPath))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

// DTOs
record PlanRequest(double TargetDurationMinutes, Pacing Pacing, Density Density, string Style);
record ScriptRequest(string Topic, string Audience, string Goal, string Tone, string Language, Aspect Aspect, double TargetDurationMinutes, Pacing Pacing, Density Density, string Style);
record TtsRequest(List<LineDto> Lines, string VoiceName, double Rate, double Pitch, PauseStyle PauseStyle);
record LineDto(int SceneIndex, string Text, double StartSeconds, double DurationSeconds);
record ComposeRequest(string TimelineJson);
record RenderRequest(string TimelineJson, string PresetName);
record RenderJobDto(string Id, string Status, float Progress, string? OutputPath, DateTime CreatedAt);
record ApplyProfileRequest(string ProfileName);
record ApiKeysRequest(string? OpenAiKey, string? ElevenLabsKey, string? PexelsKey, string? StabilityAiKey);
record ProviderPathsRequest(string? StableDiffusionUrl, string? OllamaUrl, string? FfmpegPath, string? FfprobePath, string? OutputDirectory);
record ProviderTestRequest(string? Url, string? Path);
