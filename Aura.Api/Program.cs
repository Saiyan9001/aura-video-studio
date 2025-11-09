using Aura.Api.Filters;
using Aura.Api.Helpers;
using Aura.Api.Middleware;
using Aura.Api.Serialization;
using Aura.Api.Validation;
using Aura.Api.Validators;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Planner;
using Aura.Core.Providers;
using Aura.Providers;
using Aura.Providers.Images;
using Aura.Providers.Llm;
using Aura.Providers.Tts;
using Aura.Providers.Video;
using Aura.Providers.Validation;
using AspNetCoreRateLimit;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiV1 = Aura.Api.Models.ApiModels.V1;
using PlanRequest = Aura.Api.Models.ApiModels.V1.PlanRequest;
using ScriptRequest = Aura.Api.Models.ApiModels.V1.ScriptRequest;
using TtsRequest = Aura.Api.Models.ApiModels.V1.TtsRequest;
using LineDto = Aura.Api.Models.ApiModels.V1.LineDto;
using ComposeRequest = Aura.Api.Models.ApiModels.V1.ComposeRequest;
using RenderRequest = Aura.Api.Models.ApiModels.V1.RenderRequest;
using RenderSettingsDto = Aura.Api.Models.ApiModels.V1.RenderSettingsDto;
using RenderJobDto = Aura.Api.Models.ApiModels.V1.RenderJobDto;
using ApplyProfileRequest = Aura.Api.Models.ApiModels.V1.ApplyProfileRequest;
using ApiKeysRequest = Aura.Api.Models.ApiModels.V1.ApiKeysRequest;
using ProviderPathsRequest = Aura.Api.Models.ApiModels.V1.ProviderPathsRequest;
using ProviderTestRequest = Aura.Api.Models.ApiModels.V1.ProviderTestRequest;
using RecommendationsRequestDto = Aura.Api.Models.ApiModels.V1.RecommendationsRequestDto;
using ConstraintsDto = Aura.Api.Models.ApiModels.V1.ConstraintsDto;
using AssetSearchRequest = Aura.Api.Models.ApiModels.V1.AssetSearchRequest;
using AssetGenerateRequest = Aura.Api.Models.ApiModels.V1.AssetGenerateRequest;
using CaptionsRequest = Aura.Api.Models.ApiModels.V1.CaptionsRequest;
using ValidateProvidersRequest = Aura.Api.Models.ApiModels.V1.ValidateProvidersRequest;
using StockProviderDto = Aura.Api.Models.ApiModels.V1.StockProviderDto;
using StockProvidersResponse = Aura.Api.Models.ApiModels.V1.StockProvidersResponse;
using QuotaStatusResponse = Aura.Api.Models.ApiModels.V1.QuotaStatusResponse;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});

// Configure JSON options to handle string enum conversion for minimal APIs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Add all tolerant enum converters from ApiModels.V1 contract
    EnumJsonConverters.AddToOptions(options.SerializerOptions);
});

// Configure Serilog with structured logging and separate log files
var outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext() // Enable correlation ID enrichment
    .Enrich.WithProperty("Application", "Aura.Api")
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/aura-api-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, // 30 days retention
        outputTemplate: outputTemplate)
    .WriteTo.File("logs/errors-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        outputTemplate: outputTemplate)
    .WriteTo.File("logs/warnings-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning,
        outputTemplate: outputTemplate)
    .WriteTo.File("logs/performance-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: outputTemplate,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
    .WriteTo.File("logs/audit-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90, // 90 days for audit logs
        outputTemplate: outputTemplate)
    .CreateLogger();

builder.Host.UseSerilog();

// Add global exception handler and ProblemDetails support
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Configure response compression (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/json", "text/plain", "text/css", "text/html", "application/javascript", "text/javascript" });
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Configure form options for large file uploads (100GB support)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100L * 1024L * 1024L * 1024L; // 100GB max file size
    options.ValueLengthLimit = 100 * 1024 * 1024; // 100MB max form value
    options.MultipartHeadersLengthLimit = 1024 * 1024; // 1MB max headers
});

// Add services to the container
builder.Services.AddControllers(options =>
    {
        // Add validation filter to all controllers
        options.Filters.Add<ValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Add all tolerant enum converters for controller endpoints
        EnumJsonConverters.AddToOptions(options.JsonSerializerOptions);
    });

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<ScriptRequestValidator>();
builder.Services.AddScoped<ValidationFilter>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Aura Video Studio API",
        Version = "v1",
        Description = "API for AI-powered video generation and management",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Aura Video Studio",
            Url = new Uri("https://github.com/Saiyan9001/aura-video-studio")
        }
    });
    
    // Include XML comments for better API documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
    
    // Add correlation ID header to all operations
    options.OperationFilter<Aura.Api.Filters.CorrelationIdOperationFilter>();
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<Aura.Api.HealthChecks.DependencyHealthCheck>("Dependencies")
    .AddCheck<Aura.Api.HealthChecks.DiskSpaceHealthCheck>("DiskSpace")
    .AddCheck<Aura.Api.HealthChecks.ProviderHealthCheck>("Providers");

// Configure database with WAL mode for better concurrency
const string MigrationsAssembly = "Aura.Api";
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aura.db");
builder.Services.AddDbContext<Aura.Core.Data.AuraDbContext>(options =>
{
    var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;";
    options.UseSqlite(connectionString, 
        sqliteOptions => sqliteOptions.MigrationsAssembly(MigrationsAssembly));
});

// Register ProjectStateRepository for state persistence
builder.Services.AddScoped<Aura.Core.Data.ProjectStateRepository>();

// Register configuration management services
builder.Services.AddScoped<Aura.Core.Data.ConfigurationRepository>();
builder.Services.AddSingleton<Aura.Core.Services.ConfigurationManager>();
builder.Services.AddSingleton<Aura.Core.Services.DatabaseInitializationService>();

// CheckpointManager is scoped but cannot be injected into singleton JobRunner
// It will be passed as null to JobRunner's optional parameter
// builder.Services.AddScoped<Aura.Core.Services.CheckpointManager>();

// Register wizard project management service
builder.Services.AddScoped<Aura.Core.Services.WizardProjectService>();

// Register project versioning services
builder.Services.AddScoped<Aura.Core.Data.ProjectVersionRepository>();
builder.Services.AddScoped<Aura.Core.Services.ProjectVersionService>();
// ProjectAutosaveService cannot inject scoped ProjectVersionService as singleton
// Commenting out until refactored to use IServiceScopeFactory
// builder.Services.AddSingleton<Aura.Api.HostedServices.ProjectAutosaveService>();
// builder.Services.AddHostedService(sp => sp.GetRequiredService<Aura.Api.HostedServices.ProjectAutosaveService>());

// Register ActionService for server-side undo/redo
builder.Services.AddScoped<Aura.Core.Services.IActionService, Aura.Core.Services.ActionService>();

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
builder.Services.AddSingleton<IHardwareDetector>(sp => sp.GetRequiredService<HardwareDetector>());
builder.Services.AddSingleton<Aura.Core.Hardware.DiagnosticsHelper>();
builder.Services.AddSingleton<Aura.Core.Configuration.ProviderSettings>();

// Register secure storage and key management services
builder.Services.AddSingleton<Aura.Core.Services.ISecureStorageService, Aura.Core.Services.SecureStorageService>();
builder.Services.AddSingleton<Aura.Core.Services.IKeyValidationService, Aura.Core.Services.KeyValidationService>();

// Register settings export/import service
builder.Services.AddScoped<Aura.Core.Configuration.SettingsExportImportService>();

// Register Ollama service for process control
builder.Services.AddSingleton<Aura.Core.Services.OllamaService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.OllamaService>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var logsDirectory = Path.Combine(providerSettings.GetLogsDirectory(), "ollama");
    return new Aura.Core.Services.OllamaService(logger, httpClient, logsDirectory);
});

// Note: OllamaDetectionService is registered in ProviderServicesExtensions.cs
// Register OllamaDetectionService as a hosted service for background detection
builder.Services.AddHostedService(sp => sp.GetRequiredService<Aura.Core.Services.Providers.OllamaDetectionService>());

// Configure FFmpeg options from appsettings
builder.Services.Configure<Aura.Core.Configuration.FFmpegOptions>(
    builder.Configuration.GetSection("FFmpeg"));

// Configure Circuit Breaker options from appsettings
builder.Services.Configure<Aura.Core.Configuration.CircuitBreakerSettings>(
    builder.Configuration.GetSection("CircuitBreaker"));
builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Aura.Core.Configuration.CircuitBreakerSettings>>().Value);

// Configure OpenAI provider options from appsettings
builder.Services.Configure<Aura.Core.Configuration.OpenAIConfiguration>(
    builder.Configuration.GetSection("Providers:OpenAI"));

// Register FFmpeg locator for centralized FFmpeg path resolution
builder.Services.AddSingleton<Aura.Core.Dependencies.IFfmpegLocator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.FfmpegLocator>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var toolsDir = providerSettings.GetToolsDirectory();
    return new Aura.Core.Dependencies.FfmpegLocator(logger, toolsDir);
});

// Register FFmpeg resolver with managed install precedence and caching
builder.Services.AddSingleton<Aura.Core.Dependencies.FFmpegResolver>();
builder.Services.AddMemoryCache(); // Required for FFmpegResolver caching

// Configure distributed caching with Redis fallback to in-memory
var cachingConfig = builder.Configuration.GetSection("Caching").Get<Aura.Core.Configuration.CachingConfiguration>() 
    ?? new Aura.Core.Configuration.CachingConfiguration();

if (cachingConfig.Enabled)
{
    if (cachingConfig.UseRedis && !string.IsNullOrEmpty(cachingConfig.RedisConnection))
    {
        try
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cachingConfig.RedisConnection;
                options.InstanceName = "Aura:";
            });
            Log.Information("Redis distributed cache configured");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to configure Redis, falling back to in-memory cache");
            builder.Services.AddDistributedMemoryCache();
        }
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
        Log.Information("In-memory distributed cache configured");
    }

    builder.Services.AddSingleton<Aura.Core.Services.Caching.IDistributedCacheService, 
        Aura.Core.Services.Caching.DistributedCacheService>();
    builder.Services.AddSingleton(cachingConfig);
}

// Provider mixing configuration
builder.Services.AddSingleton(sp =>
{
    var config = new ProviderMixingConfig
    {
        ActiveProfile = "Free-Only", // Default to free-only
        AutoFallback = true,
        LogProviderSelection = true
    };
    return config;
});

// Provider mixer
builder.Services.AddSingleton<Aura.Core.Orchestrator.ProviderMixer>();

// Provider recommendation, cost tracking and monitoring services
// Note: Basic provider health/cost services registered via AddProviderHealthServices() above
builder.Services.AddSingleton<Aura.Core.Services.CostTracking.EnhancedCostTrackingService>();
builder.Services.AddSingleton<Aura.Core.Services.CostTracking.TokenTrackingService>();
builder.Services.AddSingleton<Aura.Core.Services.CostTracking.RunCostReportService>();

// Offline provider availability service
builder.Services.AddSingleton<Aura.Core.Services.OfflineProviderAvailabilityService>();

builder.Services.AddSingleton<Aura.Core.Services.Providers.LlmProviderRecommendationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Providers.LlmProviderRecommendationService>>();
    var healthMonitor = sp.GetRequiredService<Aura.Core.Services.Providers.ProviderHealthMonitoringService>();
    var costTracker = sp.GetRequiredService<Aura.Core.Services.Providers.ProviderCostTrackingService>();
    var settings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var factory = sp.GetRequiredService<Aura.Core.Orchestrator.LlmProviderFactory>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    
    var providers = factory.CreateAvailableProviders(loggerFactory);
    
    return new Aura.Core.Services.Providers.LlmProviderRecommendationService(
        logger,
        healthMonitor,
        costTracker,
        settings,
        providers);
});

// Register system health checker
builder.Services.AddSingleton<Aura.Core.Services.Health.SystemHealthChecker>();

// Script orchestrator with lazy provider creation
builder.Services.AddSingleton<Aura.Core.Orchestrator.ScriptOrchestrator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Orchestrator.ScriptOrchestrator>>();
    var mixer = sp.GetRequiredService<Aura.Core.Orchestrator.ProviderMixer>();
    var factory = sp.GetRequiredService<Aura.Core.Orchestrator.LlmProviderFactory>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    
    // Create available providers
    var providers = factory.CreateAvailableProviders(loggerFactory);
    
    return new Aura.Core.Orchestrator.ScriptOrchestrator(logger, loggerFactory, mixer, providers);
});

// Keep backward compatibility - secure key store
builder.Services.AddSingleton<Aura.Core.Configuration.IKeyStore, Aura.Core.Configuration.KeyStore>();

// Register provider profile and validation services
builder.Services.AddSingleton<Aura.Core.Services.ProviderProfileService>();
builder.Services.AddSingleton<Aura.Core.Services.PreflightValidationService>();
builder.Services.AddHttpClient<Aura.Core.Services.PreflightValidationService>();

// Register LLM Cache services
builder.Services.Configure<Aura.Core.AI.Cache.LlmCacheOptions>(
    builder.Configuration.GetSection("LlmCache"));
builder.Services.Configure<Aura.Core.AI.Cache.LlmPrewarmOptions>(
    builder.Configuration.GetSection("LlmPrewarm"));

// Register cache implementation based on configuration
builder.Services.AddSingleton<Aura.Core.AI.Cache.ILlmCache>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.AI.Cache.ILlmCache>>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Aura.Core.AI.Cache.LlmCacheOptions>>();
    
    // Note: DiskLlmCache referenced in tests but not found in Core - using MemoryLlmCache
    var memoryLogger = sp.GetRequiredService<ILogger<Aura.Core.AI.Cache.MemoryLlmCache>>();
    if (options.Value.UseDiskStorage)
    {
        logger.LogWarning("DiskLlmCache requested but implementation not found - falling back to MemoryLlmCache");
        return new Aura.Core.AI.Cache.MemoryLlmCache(memoryLogger, options);
    }
    else
    {
        logger.LogInformation("Using MemoryLlmCache (disk storage disabled)");
        return new Aura.Core.AI.Cache.MemoryLlmCache(memoryLogger, options);
    }
});

builder.Services.AddSingleton<Aura.Core.AI.Cache.CachedLlmProviderService>();
builder.Services.AddHostedService<Aura.Api.HostedServices.LlmCacheMaintenanceService>();
builder.Services.AddHostedService<Aura.Api.HostedServices.LlmPrewarmService>();

// Register Conversation/Context Management services
builder.Services.AddSingleton<Aura.Core.Services.Conversation.ContextPersistence>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Conversation.ContextPersistence>>();
    var baseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aura");
    return new Aura.Core.Services.Conversation.ContextPersistence(logger, baseDirectory);
});
builder.Services.AddSingleton<Aura.Core.Services.Conversation.ConversationContextManager>();
builder.Services.AddSingleton<Aura.Core.Services.Conversation.ProjectContextManager>();
builder.Services.AddSingleton<Aura.Core.Services.Conversation.ConversationalLlmService>();

// Add Prompt Engineering services
builder.Services.AddSingleton<Aura.Core.Services.AI.PromptCustomizationService>();
builder.Services.AddScoped<Aura.Core.Services.AI.ChainOfThoughtOrchestrator>();

// Add LLM Orchestration services with schema validation
builder.Services.AddSingleton<Aura.Core.AI.Validation.SchemaValidator>();
builder.Services.AddSingleton<Aura.Core.AI.Orchestration.LlmOrchestrationService>();
builder.Services.AddScoped<Aura.Core.AI.Orchestration.StructuredLlmProviderAdapter>();

// Add LLM Router services for dynamic provider selection
builder.Services.AddSingleton<Aura.Core.AI.Routing.IRouterProviderFactory, Aura.Providers.Llm.RouterProviderFactory>();
builder.Services.Configure<Aura.Core.AI.Routing.RoutingConfiguration>(config =>
{
    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "routing-policies.json");
    if (File.Exists(configPath))
    {
        try
        {
            var json = File.ReadAllText(configPath);
            var loadedConfig = System.Text.Json.JsonSerializer.Deserialize<Aura.Core.AI.Routing.RoutingConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (loadedConfig != null)
            {
                config.CircuitBreaker = loadedConfig.CircuitBreaker;
                config.HealthCheck = loadedConfig.HealthCheck;
                config.CostTracking = loadedConfig.CostTracking;
                config.EnableFailover = loadedConfig.EnableFailover;
                config.EnableCostTracking = loadedConfig.EnableCostTracking;
                config.Policies = loadedConfig.Policies;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load routing configuration from {ConfigPath}", configPath);
        }
    }
});
builder.Services.AddSingleton<Aura.Core.AI.Routing.ILlmRouterService, Aura.Core.AI.Routing.LlmRouterService>();

// Add Prompt Management services
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.IPromptRepository, 
    Aura.Core.Services.PromptManagement.InMemoryPromptRepository>();
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.PromptVariableResolver>();
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.PromptValidator>();
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.PromptAnalyticsService>();
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.PromptTestingService>();
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.PromptABTestingService>();
builder.Services.AddSingleton<Aura.Core.Services.PromptManagement.PromptManagementService>();

// Initialize system prompt templates on startup
builder.Services.AddHostedService<Aura.Api.HostedServices.SystemPromptInitializer>();

// Register Profile Management services
builder.Services.AddSingleton<Aura.Core.Services.Profiles.ProfilePersistence>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Profiles.ProfilePersistence>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var baseDirectory = providerSettings.GetAuraDataDirectory();
    return new Aura.Core.Services.Profiles.ProfilePersistence(logger, baseDirectory);
});
builder.Services.AddSingleton<Aura.Core.Services.Profiles.ProfileService>();
builder.Services.AddSingleton<Aura.Core.Services.Profiles.ProfileContextProvider>();

// Register Learning services
builder.Services.AddSingleton<Aura.Core.Services.Learning.LearningPersistence>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Learning.LearningPersistence>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var baseDirectory = providerSettings.GetAuraDataDirectory();
    return new Aura.Core.Services.Learning.LearningPersistence(logger, baseDirectory);
});
builder.Services.AddSingleton<Aura.Core.Services.Learning.DecisionAnalysisEngine>();
builder.Services.AddSingleton<Aura.Core.Services.Learning.PatternRecognitionSystem>();
builder.Services.AddSingleton<Aura.Core.Services.Learning.PreferenceInferenceEngine>();
builder.Services.AddSingleton<Aura.Core.Services.Learning.PredictiveSuggestionRanker>();
builder.Services.AddSingleton<Aura.Core.Services.Learning.LearningService>();

// Register Ideation service
builder.Services.AddMemoryCache(); // For trending topics caching and rate limiting
builder.Services.AddSingleton<Aura.Core.Services.Ideation.TrendingTopicsService>();
builder.Services.AddSingleton<Aura.Core.Services.Ideation.IdeationService>();

// Register Audience Profile services
builder.Services.AddSingleton<Aura.Core.Services.Audience.AudienceProfileStore>();
builder.Services.AddSingleton<Aura.Core.Services.Audience.AudienceProfileValidator>();
builder.Services.AddSingleton<Aura.Core.Services.Audience.AudienceProfileConverter>();

// Register User Preferences service
builder.Services.AddSingleton<Aura.Core.Services.UserPreferences.UserPreferencesService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.UserPreferences.UserPreferencesService>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var dataDirectory = providerSettings.GetAuraDataDirectory();
    return new Aura.Core.Services.UserPreferences.UserPreferencesService(logger, dataDirectory);
});

// Register Content Adaptation services
builder.Services.AddScoped<Aura.Core.Services.Audience.ContentAdaptationEngine>();
builder.Services.AddScoped<Aura.Core.Services.Audience.AdaptationPreviewService>();

// Register Rate Limiting services
builder.Services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<AspNetCoreRateLimit.IIpPolicyStore, AspNetCoreRateLimit.MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<AspNetCoreRateLimit.IRateLimitCounterStore, AspNetCoreRateLimit.MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<AspNetCoreRateLimit.IRateLimitConfiguration, AspNetCoreRateLimit.RateLimitConfiguration>();
builder.Services.AddSingleton<AspNetCoreRateLimit.IProcessingStrategy, AspNetCoreRateLimit.AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Register Authentication services
builder.Services.Configure<Aura.Api.Security.ApiAuthenticationOptions>(builder.Configuration.GetSection("Authentication"));

// Register Performance Telemetry services
builder.Services.AddSingleton<Aura.Api.Telemetry.PerformanceMetrics>();

// Register Content Verification services
builder.Services.AddSingleton<Aura.Core.Services.ContentVerification.FactCheckingService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentVerification.SourceAttributionService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentVerification.ConfidenceAnalysisService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentVerification.MisinformationDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentVerification.ContentVerificationOrchestrator>();
builder.Services.AddSingleton<Aura.Core.Services.ContentVerification.VerificationPersistence>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.ContentVerification.VerificationPersistence>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var dataDir = providerSettings.GetAuraDataDirectory();
    return new Aura.Core.Services.ContentVerification.VerificationPersistence(logger, dataDir);
});

// Register Content Planning services
builder.Services.AddSingleton<Aura.Core.Services.ContentPlanning.TrendAnalysisService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentPlanning.TopicGenerationService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentPlanning.AudienceAnalysisService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentPlanning.ContentSchedulingService>();

// Register Content Safety services
builder.Services.AddSingleton<Aura.Core.Services.ContentSafety.KeywordListManager>();
builder.Services.AddSingleton<Aura.Core.Services.ContentSafety.TopicFilterManager>();
builder.Services.AddSingleton<Aura.Core.Services.ContentSafety.ContentSafetyService>();
builder.Services.AddSingleton<Aura.Core.Services.ContentSafety.SafetyIntegrationService>();

// Register Audio services
builder.Services.AddSingleton<Aura.Core.Audio.WavValidator>();
builder.Services.AddSingleton<Aura.Core.Audio.SilentWavGenerator>();
builder.Services.AddSingleton<Aura.Core.Services.Audio.NarrationOptimizationService>();
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.LicensingService>();

// Register AI Editing services
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.SceneDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.HighlightDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.BeatDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.AutoFramingService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.SpeechRecognitionService>();

// Register Voice Enhancement services
builder.Services.AddSingleton<Aura.Core.Services.VoiceEnhancement.NoiseReductionService>();
builder.Services.AddSingleton<Aura.Core.Services.VoiceEnhancement.EqualizeService>();
builder.Services.AddSingleton<Aura.Core.Services.VoiceEnhancement.ProsodyAdjustmentService>();
builder.Services.AddSingleton<Aura.Core.Services.VoiceEnhancement.EmotionDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.VoiceEnhancement.VoiceProcessingService>();

// Register HTTP client factory (required by providers)
builder.Services.AddHttpClient();

// Register all providers using centralized extension methods from Aura.Providers
// This ensures consistent DI registration across LLM, TTS, and Image providers
builder.Services.AddAuraProviders();
builder.Services.AddProviderFactories();
builder.Services.AddProviderHealthServices();

// Register Azure TTS voice discovery (additional service beyond basic provider)
builder.Services.AddSingleton<Aura.Providers.Tts.AzureVoiceDiscovery>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Providers.Tts.AzureVoiceDiscovery>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var apiKey = providerSettings.GetAzureSpeechKey();
    var region = providerSettings.GetAzureSpeechRegion();
    
    return new Aura.Providers.Tts.AzureVoiceDiscovery(logger, httpClient, region, apiKey);
});

// DO NOT resolve default provider during startup - let it be resolved lazily when first needed
// This prevents startup crashes due to provider resolution issues

builder.Services.AddSingleton<IVideoComposer>(sp => 
{
    var logger = sp.GetRequiredService<ILogger<FfmpegVideoComposer>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
    var configuredFfmpegPath = providerSettings.GetFfmpegPath();
    var outputDirectory = providerSettings.GetOutputDirectory();
    return new FfmpegVideoComposer(logger, ffmpegLocator, configuredFfmpegPath, outputDirectory);
});

// Register validators
builder.Services.AddSingleton<Aura.Core.Validation.PreGenerationValidator>();
builder.Services.AddSingleton<Aura.Core.Validation.ScriptValidator>();
builder.Services.AddSingleton<Aura.Core.Validation.TtsOutputValidator>();
builder.Services.AddSingleton<Aura.Core.Validation.ImageOutputValidator>();
builder.Services.AddSingleton<Aura.Core.Validation.LlmOutputValidator>();

// Register Model Selection services
builder.Services.AddSingleton<Aura.Core.AI.Adapters.ModelCatalog>();
builder.Services.AddSingleton<Aura.Core.Services.ModelSelection.ModelSelectionStore>();
builder.Services.AddSingleton<Aura.Core.Services.ModelSelection.ModelSelectionService>();

// Register latency management services
builder.Services.Configure<Aura.Core.Services.Performance.LlmTimeoutPolicy>(
    builder.Configuration.GetSection("LlmTimeouts"));
builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Aura.Core.Services.Performance.LlmTimeoutPolicy>>().Value);
builder.Services.AddSingleton<Aura.Core.Services.Performance.LatencyTelemetry>();
builder.Services.AddSingleton<Aura.Core.Services.Performance.LatencyManagementService>();
builder.Services.AddSingleton<Aura.Core.Services.Performance.LlmOperationContext>();

builder.Services.AddSingleton<Aura.Core.Services.ProviderRetryWrapper>();
builder.Services.AddSingleton<Aura.Core.Services.ResourceCleanupManager>();

// Register resource management services
builder.Services.AddSingleton<Aura.Core.Services.Resources.TemporaryFileCleanupService>();
builder.Services.AddSingleton<Aura.Core.Services.Resources.DiskSpaceChecker>();
builder.Services.AddSingleton<Aura.Core.Services.Resources.SystemResourceMonitor>();
builder.Services.AddSingleton<Aura.Core.Services.Resources.ResourceThrottler>();

// Register smart orchestration services
builder.Services.AddSingleton<Aura.Core.Services.Generation.ResourceMonitor>();
builder.Services.AddSingleton<Aura.Core.Services.Generation.StrategySelector>();
builder.Services.AddSingleton<Aura.Core.Services.Generation.VideoGenerationOrchestrator>();

// Register timeline and pacing services (required for VideoOrchestrator)
builder.Services.AddSingleton<Aura.Core.Timeline.TimelineBuilder>();

// Register ML models first (dependencies)
builder.Services.AddSingleton<Aura.Core.ML.Models.AttentionRetentionModel>();
builder.Services.AddSingleton<Aura.Core.ML.Models.FrameImportanceModel>();

// Register ML training service
builder.Services.AddSingleton<Aura.Core.Services.ML.ModelTrainingService>();

// Register pacing services in dependency order
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.ContentComplexityAnalyzer>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.SceneImportanceAnalyzer>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.AttentionCurvePredictor>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.TransitionRecommender>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.EmotionalBeatAnalyzer>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.SceneRelationshipMapper>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.IntelligentPacingOptimizer>();
builder.Services.AddSingleton<Aura.Core.Services.PacingServices.PacingApplicationService>();

// Register pacing API services
builder.Services.AddSingleton<Aura.Api.Services.PacingAnalysisCacheService>();

// Register Pipeline Orchestration services (PR 21 intelligent pipeline)
builder.Services.AddSingleton<Aura.Core.Services.Orchestration.PipelineCache>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Orchestration.PipelineCache>>();
    return new Aura.Core.Services.Orchestration.PipelineCache(logger);
});

builder.Services.AddSingleton<Aura.Core.Services.Orchestration.PipelineHealthCheck>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Orchestration.PipelineHealthCheck>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    var ttsProvider = sp.GetRequiredService<ITtsProvider>();
    var narrationOptimizer = sp.GetService<Aura.Core.Services.Audio.NarrationOptimizationService>();
    var pacingOptimizer = sp.GetService<Aura.Core.Services.PacingServices.IntelligentPacingOptimizer>();
    
    return new Aura.Core.Services.Orchestration.PipelineHealthCheck(
        logger: logger,
        llmProvider: llmProvider,
        ttsProvider: ttsProvider,
        contentAdvisor: null,
        narrativeAnalyzer: null,
        pacingOptimizer: pacingOptimizer,
        toneEnforcer: null,
        visualPromptService: null,
        visualAlignmentService: null,
        narrationOptimizer: narrationOptimizer,
        scriptRefinement: null
    );
});

builder.Services.AddSingleton<Aura.Core.Services.Orchestration.PipelineOrchestrationEngine>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Orchestration.PipelineOrchestrationEngine>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    var cache = sp.GetRequiredService<Aura.Core.Services.Orchestration.PipelineCache>();
    var healthCheck = sp.GetRequiredService<Aura.Core.Services.Orchestration.PipelineHealthCheck>();
    var ttsProvider = sp.GetRequiredService<ITtsProvider>();
    var narrationOptimizer = sp.GetService<Aura.Core.Services.Audio.NarrationOptimizationService>();
    var pacingOptimizer = sp.GetService<Aura.Core.Services.PacingServices.IntelligentPacingOptimizer>();
    
    var config = new Aura.Core.Services.Orchestration.PipelineConfiguration
    {
        MaxConcurrentLlmCalls = Math.Max(1, Environment.ProcessorCount / 2),
        EnableCaching = true,
        CacheTtl = TimeSpan.FromHours(1),
        ContinueOnOptionalFailure = true,
        EnableParallelExecution = true
    };
    
    return new Aura.Core.Services.Orchestration.PipelineOrchestrationEngine(
        logger: logger,
        llmProvider: llmProvider,
        cache: cache,
        healthCheck: healthCheck,
        config: config,
        ttsProvider: ttsProvider,
        contentAdvisor: null,
        narrativeAnalyzer: null,
        pacingOptimizer: pacingOptimizer,
        toneEnforcer: null,
        visualPromptService: null,
        visualAlignmentService: null,
        narrationOptimizer: narrationOptimizer,
        scriptRefinement: null
    );
});

builder.Services.AddSingleton<VideoOrchestrator>();

// Register AI pacing and rhythm optimization services
builder.Services.AddSingleton<Aura.Core.AI.Pacing.RhythmDetector>();
builder.Services.AddSingleton<Aura.Core.AI.Pacing.RetentionOptimizer>();
builder.Services.AddSingleton<Aura.Core.AI.Pacing.PacingAnalyzer>();
builder.Services.AddSingleton<Aura.Core.Services.Analytics.ViewerRetentionPredictor>();

// Register Performance Analytics services
builder.Services.AddSingleton<Aura.Core.Services.PerformanceAnalytics.AnalyticsPersistence>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.PerformanceAnalytics.AnalyticsPersistence>>();
    var baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura");
    return new Aura.Core.Services.PerformanceAnalytics.AnalyticsPersistence(logger, baseDirectory);
});
builder.Services.AddSingleton<Aura.Core.Services.PerformanceAnalytics.AnalyticsImporter>();
builder.Services.AddSingleton<Aura.Core.Services.PerformanceAnalytics.VideoProjectLinker>();
builder.Services.AddSingleton<Aura.Core.Services.PerformanceAnalytics.CorrelationAnalyzer>();
builder.Services.AddSingleton<Aura.Core.Services.PerformanceAnalytics.PerformancePatternDetector>();
builder.Services.AddSingleton<Aura.Core.Services.PerformanceAnalytics.PerformanceAnalyticsService>();

// Register timeline editor services
builder.Services.AddSingleton<Aura.Core.Services.Editor.TimelineRenderer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Editor.TimelineRenderer>>();
    var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
    var ffmpegPath = ffmpegLocator.GetEffectiveFfmpegPathAsync().GetAwaiter().GetResult();
    return new Aura.Core.Services.Editor.TimelineRenderer(logger, ffmpegPath);
});

// Register planner provider factory and PlannerService with lazy LLM routing
builder.Services.AddSingleton<Aura.Providers.Planner.PlannerProviderFactory>();
builder.Services.AddSingleton<IRecommendationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Planner.PlannerService>>();
    var factory = sp.GetRequiredService<Aura.Providers.Planner.PlannerProviderFactory>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    
    // Use factory delegate for lazy provider initialization - providers created on first use
    // Use ProIfAvailable by default - will use Pro providers if API keys exist, else fall back to free
    Func<Dictionary<string, ILlmPlannerProvider>> providerFactory = () => factory.CreateAvailableProviders(loggerFactory);
    return new Aura.Core.Planner.PlannerService(logger, providerFactory, "ProIfAvailable");
});

// Keep HeuristicRecommendationService available for direct use if needed
builder.Services.AddSingleton<Aura.Core.Planner.HeuristicRecommendationService>();

builder.Services.AddSingleton<Aura.Providers.Validation.ProviderValidationService>();
builder.Services.AddSingleton<Aura.Api.Services.PreflightService>();

// Register API key validation and secure storage services
builder.Services.AddSingleton<Aura.Api.Services.ApiKeyValidationService>();
builder.Services.AddSingleton<Aura.Core.Services.ISecureStorageService, Aura.Core.Services.SecureStorageService>();

// Register content analysis services
builder.Services.AddSingleton<Aura.Core.Services.Content.ContentAnalyzer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Content.ContentAnalyzer>>();
    // Use the singleton ILlmProvider (RuleBased fallback)
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    return new Aura.Core.Services.Content.ContentAnalyzer(logger, llmProvider);
});

builder.Services.AddSingleton<Aura.Core.Services.Content.ScriptEnhancer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Content.ScriptEnhancer>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    return new Aura.Core.Services.Content.ScriptEnhancer(logger, llmProvider);
});

// Register Document Import services
builder.Services.AddSingleton<Aura.Core.Services.Content.DocumentImportService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Content.DocumentImportService>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new Aura.Core.Services.Content.DocumentImportService(logger, llmProvider, loggerFactory);
});

builder.Services.AddSingleton<Aura.Core.Services.Content.ScriptConverter>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Content.ScriptConverter>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    var adaptationEngine = sp.GetService<Aura.Core.Services.Audience.ContentAdaptationEngine>();
    var audienceProfileStore = sp.GetService<Aura.Core.Services.Audience.AudienceProfileStore>();
    return new Aura.Core.Services.Content.ScriptConverter(logger, llmProvider, adaptationEngine, audienceProfileStore);
});

// Register RAG services for Retrieval-Augmented Generation
builder.Services.AddSingleton<Aura.Core.Services.RAG.EmbeddingConfig>(sp =>
{
    var config = builder.Configuration.GetSection("RAG:Embedding");
    return new Aura.Core.Services.RAG.EmbeddingConfig
    {
        Provider = config.GetValue("Provider", Aura.Core.Services.RAG.EmbeddingProvider.Local),
        ApiKey = config.GetValue<string>("ApiKey"),
        BaseUrl = config.GetValue<string>("BaseUrl"),
        ModelName = config.GetValue<string>("ModelName"),
        DimensionSize = config.GetValue("DimensionSize", 384)
    };
});

builder.Services.AddSingleton<Aura.Core.Services.RAG.EmbeddingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.RAG.EmbeddingService>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var config = sp.GetRequiredService<Aura.Core.Services.RAG.EmbeddingConfig>();
    return new Aura.Core.Services.RAG.EmbeddingService(logger, httpClientFactory, config);
});

builder.Services.AddSingleton<Aura.Core.Services.RAG.VectorIndex>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.RAG.VectorIndex>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var dataDir = Path.Combine(providerSettings.GetAuraDataDirectory(), "rag");
    Directory.CreateDirectory(dataDir);
    var indexPath = Path.Combine(dataDir, "vector_index.json");
    return new Aura.Core.Services.RAG.VectorIndex(logger, indexPath);
});

builder.Services.AddSingleton<Aura.Core.Services.RAG.DocumentChunkingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.RAG.DocumentChunkingService>>();
    return new Aura.Core.Services.RAG.DocumentChunkingService(logger);
});

builder.Services.AddSingleton<Aura.Core.Services.RAG.RagContextBuilder>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.RAG.RagContextBuilder>>();
    var vectorIndex = sp.GetRequiredService<Aura.Core.Services.RAG.VectorIndex>();
    var embeddingService = sp.GetRequiredService<Aura.Core.Services.RAG.EmbeddingService>();
    return new Aura.Core.Services.RAG.RagContextBuilder(logger, vectorIndex, embeddingService);
});

builder.Services.AddSingleton<Aura.Core.Services.RAG.DocumentIngestService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.RAG.DocumentIngestService>>();
    var importService = sp.GetRequiredService<Aura.Core.Services.Content.DocumentImportService>();
    var chunkingService = sp.GetRequiredService<Aura.Core.Services.RAG.DocumentChunkingService>();
    var embeddingService = sp.GetRequiredService<Aura.Core.Services.RAG.EmbeddingService>();
    var vectorIndex = sp.GetRequiredService<Aura.Core.Services.RAG.VectorIndex>();
    return new Aura.Core.Services.RAG.DocumentIngestService(
        logger, importService, chunkingService, embeddingService, vectorIndex);
});

builder.Services.AddSingleton<Aura.Core.Services.RAG.RagScriptEnhancer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.RAG.RagScriptEnhancer>>();
    var contextBuilder = sp.GetRequiredService<Aura.Core.Services.RAG.RagContextBuilder>();
    return new Aura.Core.Services.RAG.RagScriptEnhancer(logger, contextBuilder);
});

// Register Script Enhancement services (for AI Audio Intelligence integration)
builder.Services.AddSingleton<Aura.Core.Services.ScriptEnhancement.ScriptAnalysisService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.ScriptEnhancement.ScriptAnalysisService>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    return new Aura.Core.Services.ScriptEnhancement.ScriptAnalysisService(logger, llmProvider);
});

builder.Services.AddSingleton<Aura.Core.Services.ScriptEnhancement.AdvancedScriptEnhancer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.ScriptEnhancement.AdvancedScriptEnhancer>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    var analysisService = sp.GetRequiredService<Aura.Core.Services.ScriptEnhancement.ScriptAnalysisService>();
    return new Aura.Core.Services.ScriptEnhancement.AdvancedScriptEnhancer(logger, llmProvider, analysisService);
});

builder.Services.AddSingleton<Aura.Core.Services.Content.VisualAssetSuggester>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Content.VisualAssetSuggester>>();
    var llmProvider = sp.GetRequiredService<ILlmProvider>();
    // Try to get stock provider if available
    IStockProvider? stockProvider = null;
    try
    {
        stockProvider = sp.GetService<IStockProvider>();
    }
    catch
    {
        // Stock provider is optional
    }
    return new Aura.Core.Services.Content.VisualAssetSuggester(logger, llmProvider, stockProvider);
});

builder.Services.AddSingleton<Aura.Core.Services.Content.PacingOptimizer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Content.PacingOptimizer>>();
    return new Aura.Core.Services.Content.PacingOptimizer(logger);
});

// Register analytics services
builder.Services.AddSingleton<Aura.Core.Analytics.Retention.RetentionPredictor>();
builder.Services.AddSingleton<Aura.Core.Analytics.Platforms.PlatformOptimizer>();
builder.Services.AddSingleton<Aura.Core.Analytics.Content.ContentAnalyzer>();
builder.Services.AddSingleton<Aura.Core.Analytics.Recommendations.ImprovementEngine>();

// Register asset library services
builder.Services.AddSingleton<Aura.Core.Services.Assets.ThumbnailGenerator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Assets.ThumbnailGenerator>>();
    return new Aura.Core.Services.Assets.ThumbnailGenerator(logger);
});

builder.Services.AddSingleton<Aura.Core.Services.Assets.AssetLibraryService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Assets.AssetLibraryService>>();
    var thumbnailGenerator = sp.GetRequiredService<Aura.Core.Services.Assets.ThumbnailGenerator>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var libraryPath = Path.Combine(providerSettings.GetOutputDirectory(), "AssetLibrary");
    return new Aura.Core.Services.Assets.AssetLibraryService(logger, libraryPath, thumbnailGenerator);
});

builder.Services.AddSingleton<Aura.Core.Services.Assets.AssetTagger>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Assets.AssetTagger>>();
    return new Aura.Core.Services.Assets.AssetTagger(logger);
});

builder.Services.AddSingleton<Aura.Core.Services.Assets.StockImageService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Assets.StockImageService>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    // API keys can be configured through environment variables or app settings
    var configuration = sp.GetRequiredService<IConfiguration>();
    var pexelsKey = configuration["StockImages:PexelsApiKey"];
    var pixabayKey = configuration["StockImages:PixabayApiKey"];
    return new Aura.Core.Services.Assets.StockImageService(logger, httpClient, pexelsKey, pixabayKey);
});

builder.Services.AddSingleton<Aura.Core.Services.Visual.AestheticScoringService>();
builder.Services.AddSingleton<Aura.Core.Services.Visual.ImageSelectionService>();
builder.Services.AddSingleton<Aura.Core.Services.Visual.CandidateCacheService>();
builder.Services.AddSingleton<Aura.Core.Services.Visual.VisualSelectionService>();

builder.Services.AddSingleton<Aura.Core.Services.Assets.AIImageGenerator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Assets.AIImageGenerator>>();
    return new Aura.Core.Services.Assets.AIImageGenerator(logger);
});

builder.Services.AddSingleton<Aura.Core.Services.Assets.AssetUsageTracker>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Assets.AssetUsageTracker>>();
    return new Aura.Core.Services.Assets.AssetUsageTracker(logger);
});

// Register platform optimization services
builder.Services.AddSingleton<Aura.Core.Services.Platform.PlatformProfileService>();
builder.Services.AddSingleton<Aura.Core.Services.Platform.PlatformOptimizationService>();
builder.Services.AddSingleton<Aura.Core.Services.Platform.MetadataOptimizationService>();
builder.Services.AddSingleton<Aura.Core.Services.Platform.ThumbnailIntelligenceService>();
builder.Services.AddSingleton<Aura.Core.Services.Platform.KeywordResearchService>();
builder.Services.AddSingleton<Aura.Core.Services.Platform.SchedulingOptimizationService>();

// Register health check and startup validation services
builder.Services.AddSingleton<Aura.Api.Services.HealthCheckService>();
builder.Services.AddSingleton<Aura.Api.Services.HealthDiagnosticsService>();
builder.Services.AddSingleton<Aura.Api.Services.StartupValidator>();
builder.Services.AddSingleton<Aura.Api.Services.FirstRunDiagnostics>();
builder.Services.AddSingleton<ConfigurationValidator>();

// Register diagnostic and error aggregation services
builder.Services.AddSingleton<Aura.Core.Services.Diagnostics.ErrorAggregationService>();
builder.Services.AddSingleton<Aura.Core.Services.Diagnostics.PerformanceTrackingService>();
builder.Services.AddSingleton<Aura.Core.Services.Diagnostics.DiagnosticReportGenerator>();
builder.Services.AddSingleton<Aura.Core.Services.Diagnostics.DiagnosticBundleService>();
builder.Services.AddSingleton<Aura.Core.Services.Diagnostics.FailureAnalysisService>();

// Register Startup Initialization Service - runs first to ensure critical services are ready
builder.Services.AddHostedService<Aura.Api.HostedServices.StartupInitializationService>();

// Register Provider Warmup Service - warms up providers in background, never crashes startup
builder.Services.AddHostedService<Aura.Api.HostedServices.ProviderWarmupService>();

// Register Health Check Background Service - runs scheduled health checks
builder.Services.AddHostedService<Aura.Api.HostedServices.HealthCheckBackgroundService>();

// Register OrphanedFileCleanupService for cleaning up old projects and temp files
builder.Services.AddHostedService<Aura.Api.HostedServices.OrphanedFileCleanupService>();

// Register ActionCleanupService for cleaning up expired actions
builder.Services.AddHostedService<Aura.Api.HostedServices.ActionCleanupService>();

// Register DependencyManager
builder.Services.AddHttpClient<Aura.Core.Dependencies.DependencyManager>();
builder.Services.AddSingleton<Aura.Core.Dependencies.DependencyManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.DependencyManager>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    
    // Portable-only mode: always use portable root
    var portableRoot = providerSettings.GetPortableRootPath();
    var manifestPath = Path.Combine(providerSettings.GetAuraDataDirectory(), "install-manifest.json");
    var downloadDirectory = providerSettings.GetDownloadsDirectory();
    
    return new Aura.Core.Dependencies.DependencyManager(logger, httpClient, manifestPath, downloadDirectory, portableRoot);
});

// Register DownloadService
builder.Services.AddSingleton<Aura.Api.Services.DownloadService>();

// Register Engine services
builder.Services.AddHttpClient<Aura.Core.Downloads.EngineManifestLoader>();
builder.Services.AddSingleton<Aura.Core.Downloads.EngineManifestLoader>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Downloads.EngineManifestLoader>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var manifestPath = Path.Combine(providerSettings.GetAuraDataDirectory(), "engines-manifest.json");
    return new Aura.Core.Downloads.EngineManifestLoader(logger, httpClient, manifestPath);
});

// Register GitHubReleaseResolver
builder.Services.AddHttpClient<Aura.Core.Dependencies.GitHubReleaseResolver>();
builder.Services.AddSingleton<Aura.Core.Dependencies.GitHubReleaseResolver>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.GitHubReleaseResolver>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new Aura.Core.Dependencies.GitHubReleaseResolver(logger, httpClient);
});

builder.Services.AddHttpClient<Aura.Core.Downloads.EngineInstaller>();
builder.Services.AddSingleton<Aura.Core.Downloads.EngineInstaller>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Downloads.EngineInstaller>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    
    // Portable-only mode: always use Tools folder
    var installRoot = providerSettings.GetToolsDirectory();
    return new Aura.Core.Downloads.EngineInstaller(logger, httpClient, installRoot);
});

builder.Services.AddSingleton<Aura.Core.Runtime.ExternalProcessManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Runtime.ExternalProcessManager>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var logDirectory = Path.Combine(providerSettings.GetLogsDirectory(), "tools");
    return new Aura.Core.Runtime.ExternalProcessManager(logger, httpClient, logDirectory);
});

// Register ModelInstaller
builder.Services.AddHttpClient<Aura.Core.Downloads.ModelInstaller>();
builder.Services.AddSingleton<Aura.Core.Downloads.ModelInstaller>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Downloads.ModelInstaller>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    
    // Portable-only mode: always use Tools folder
    var installRoot = providerSettings.GetToolsDirectory();
    return new Aura.Core.Downloads.ModelInstaller(logger, httpClient, installRoot);
});

builder.Services.AddSingleton<Aura.Core.Runtime.LocalEnginesRegistry>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Runtime.LocalEnginesRegistry>>();
    var processManager = sp.GetRequiredService<Aura.Core.Runtime.ExternalProcessManager>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    var configPath = Path.Combine(providerSettings.GetAuraDataDirectory(), "engines-config.json");
    return new Aura.Core.Runtime.LocalEnginesRegistry(logger, processManager, configPath);
});

// Register Engine Lifecycle Manager
builder.Services.AddSingleton<Aura.Core.Runtime.EngineLifecycleManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Runtime.EngineLifecycleManager>>();
    var registry = sp.GetRequiredService<Aura.Core.Runtime.LocalEnginesRegistry>();
    var processManager = sp.GetRequiredService<Aura.Core.Runtime.ExternalProcessManager>();
    return new Aura.Core.Runtime.EngineLifecycleManager(logger, registry, processManager, maxRestartAttempts: 3);
});

// Register Engine Detector
builder.Services.AddSingleton<Aura.Core.Runtime.EngineDetector>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Runtime.EngineDetector>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    
    // Portable-only mode: always use Tools folder
    var toolsRoot = providerSettings.GetToolsDirectory();
    return new Aura.Core.Runtime.EngineDetector(logger, httpClient, toolsRoot);
});

// Register HttpDownloader for FFmpeg installer
builder.Services.AddHttpClient<Aura.Core.Downloads.HttpDownloader>();
builder.Services.AddSingleton<Aura.Core.Downloads.HttpDownloader>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Downloads.HttpDownloader>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new Aura.Core.Downloads.HttpDownloader(logger, httpClient);
});

// Register FFmpeg Installer
builder.Services.AddSingleton<Aura.Core.Dependencies.FfmpegInstaller>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.FfmpegInstaller>>();
    var downloader = sp.GetRequiredService<Aura.Core.Downloads.HttpDownloader>();
    var resolver = sp.GetRequiredService<Aura.Core.Dependencies.GitHubReleaseResolver>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    
    // Portable-only mode: always use Tools folder
    var toolsDirectory = providerSettings.GetToolsDirectory();
    return new Aura.Core.Dependencies.FfmpegInstaller(logger, downloader, toolsDirectory, resolver);
});

// Register FFmpeg Locator
builder.Services.AddSingleton<Aura.Core.Dependencies.FfmpegLocator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.FfmpegLocator>>();
    var providerSettings = sp.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
    
    // Portable-only mode: always use Tools folder
    var toolsDirectory = providerSettings.GetToolsDirectory();
    return new Aura.Core.Dependencies.FfmpegLocator(logger, toolsDirectory);
});

// Register ComponentDownloader
builder.Services.AddSingleton<Aura.Core.Dependencies.ComponentDownloader>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.ComponentDownloader>>();
    var resolver = sp.GetRequiredService<Aura.Core.Dependencies.GitHubReleaseResolver>();
    var downloader = sp.GetRequiredService<Aura.Core.Downloads.HttpDownloader>();
    var componentsJsonPath = Path.Combine(AppContext.BaseDirectory, "Dependencies", "components.json");
    return new Aura.Core.Dependencies.ComponentDownloader(logger, resolver, downloader, componentsJsonPath);
});

// Register DependencyRescanService
builder.Services.AddSingleton<Aura.Core.Dependencies.DependencyRescanService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Dependencies.DependencyRescanService>>();
    var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.FfmpegLocator>();
    var componentDownloader = sp.GetRequiredService<Aura.Core.Dependencies.ComponentDownloader>();
    return new Aura.Core.Dependencies.DependencyRescanService(logger, ffmpegLocator, componentDownloader);
});

// Register Setup services
builder.Services.AddSingleton<Aura.Core.Services.Setup.DependencyDetector>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Setup.DependencyDetector>>();
    var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.FfmpegLocator>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new Aura.Core.Services.Setup.DependencyDetector(logger, ffmpegLocator, httpClient);
});

builder.Services.AddSingleton<Aura.Core.Services.Setup.DependencyInstaller>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Setup.DependencyInstaller>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new Aura.Core.Services.Setup.DependencyInstaller(logger, httpClient);
});

// Register FFmpeg detection service with caching
builder.Services.AddSingleton<Aura.Core.Services.Setup.IFFmpegDetectionService, Aura.Core.Services.Setup.FFmpegDetectionService>();

builder.Services.AddSingleton<Aura.Api.Services.SseService>();
builder.Services.AddSingleton<Aura.Api.Services.ProgressService>();

// Register Audio/Caption services
builder.Services.AddSingleton<Aura.Core.Audio.AudioProcessor>();
builder.Services.AddSingleton<Aura.Core.Audio.DspChain>();
builder.Services.AddSingleton<Aura.Core.Captions.CaptionBuilder>();

// Register Audio Intelligence services
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.MusicRecommendationService>();
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.BeatDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.VoiceDirectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.SoundEffectService>();
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.AudioMixingService>();
builder.Services.AddSingleton<Aura.Core.Services.AudioIntelligence.AudioContinuityService>();

// Register Template Service
builder.Services.AddScoped<Aura.Core.Services.TemplateService>();

// Register Quality Validation services
builder.Services.AddSingleton<Aura.Api.Services.QualityValidation.ResolutionValidationService>();
builder.Services.AddSingleton<Aura.Api.Services.QualityValidation.AudioQualityService>();
builder.Services.AddSingleton<Aura.Api.Services.QualityValidation.FrameRateService>();
builder.Services.AddSingleton<Aura.Api.Services.QualityValidation.ConsistencyAnalysisService>();
builder.Services.AddSingleton<Aura.Api.Services.QualityValidation.PlatformRequirementsService>();

// Register Quality Dashboard services
builder.Services.AddSingleton<Aura.Api.Services.Dashboard.MetricsAggregationService>();
builder.Services.AddSingleton<Aura.Api.Services.Dashboard.TrendAnalysisService>();
builder.Services.AddSingleton<Aura.Api.Services.Dashboard.RecommendationService>();
builder.Services.AddSingleton<Aura.Api.Services.Dashboard.ReportGenerationService>();

// Register Editing Intelligence services
builder.Services.AddSingleton<Aura.Core.Services.EditingIntelligence.CutPointDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.EditingIntelligence.PacingOptimizationService>();
builder.Services.AddSingleton<Aura.Core.Services.EditingIntelligence.TransitionRecommendationService>();
builder.Services.AddSingleton<Aura.Core.Services.EditingIntelligence.EngagementOptimizationService>();
builder.Services.AddSingleton<Aura.Core.Services.EditingIntelligence.QualityControlService>();
builder.Services.AddSingleton<Aura.Core.Services.EditingIntelligence.EditingIntelligenceOrchestrator>();

// Register AI Editing services
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.SceneDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.HighlightDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.BeatDetectionService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.AutoFramingService>();
builder.Services.AddSingleton<Aura.Core.Services.AIEditing.SpeechRecognitionService>();

// Register Export services
builder.Services.AddSingleton<Aura.Core.Services.FFmpeg.IFFmpegService, Aura.Core.Services.FFmpeg.FFmpegService>();
builder.Services.AddSingleton<Aura.Core.Services.FFmpeg.IFFmpegStatusService, Aura.Core.Services.FFmpeg.FFmpegStatusService>();
builder.Services.AddSingleton<Aura.Core.Services.Export.IFormatConversionService, Aura.Core.Services.Export.FormatConversionService>();
builder.Services.AddSingleton<Aura.Core.Services.Export.IResolutionService, Aura.Core.Services.Export.ResolutionService>();
builder.Services.AddSingleton<Aura.Core.Services.Export.IBitrateOptimizationService, Aura.Core.Services.Export.BitrateOptimizationService>();
// Changed from Singleton to Scoped because ExportOrchestrationService depends on scoped AuraDbContext
builder.Services.AddScoped<Aura.Core.Services.Export.IExportOrchestrationService, Aura.Core.Services.Export.ExportOrchestrationService>();

// Register Licensing services
builder.Services.AddSingleton<Aura.Core.Services.Licensing.ILicensingService, Aura.Core.Services.Licensing.LicensingService>();

// Register Cloud Storage services
builder.Services.AddSingleton(sp =>
{
    var settings = new Aura.Core.Models.Settings.CloudStorageSettings();
    builder.Configuration.GetSection("CloudStorage").Bind(settings);
    return settings;
});
builder.Services.AddSingleton<Aura.Core.Services.Storage.CloudStorageProviderFactory>();

// Register CloudExportService conditionally based on configuration
var cloudStorageConfig = new Aura.Core.Models.Settings.CloudStorageSettings();
builder.Configuration.GetSection("CloudStorage").Bind(cloudStorageConfig);
if (cloudStorageConfig.Enabled)
{
    builder.Services.AddSingleton<Aura.Core.Services.Export.ICloudExportService, Aura.Core.Services.Export.CloudExportService>();
}

// Proxy media and caching services
builder.Services.AddSingleton<Aura.Core.Services.Media.IProxyMediaService>(sp =>
{
    var ffmpegService = sp.GetRequiredService<Aura.Core.Services.FFmpeg.IFFmpegService>();
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Media.ProxyMediaService>>();
    return new Aura.Core.Services.Media.ProxyMediaService(ffmpegService, logger);
});
builder.Services.AddSingleton<Aura.Core.Services.Media.WaveformGenerator>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Services.Media.WaveformGenerator>>();
    var ffmpegLocator = sp.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
    var ffmpegPath = ffmpegLocator.GetEffectiveFfmpegPathAsync().GetAwaiter().GetResult();
    return new Aura.Core.Services.Media.WaveformGenerator(logger, ffmpegPath);
});

// Register Job Runner, Artifact Manager, and Cleanup Service
builder.Services.AddSingleton<Aura.Core.Artifacts.ArtifactManager>();
builder.Services.AddSingleton<Aura.Core.Services.CleanupService>();
builder.Services.AddSingleton<Aura.Core.Orchestrator.JobRunner>();
builder.Services.AddSingleton<Aura.Core.Orchestrator.QuickService>();
builder.Services.AddHostedService<Aura.Api.HostedServices.CleanupHostedService>();

// Register Run Telemetry Collector and Integration
builder.Services.AddSingleton<Aura.Core.Telemetry.RunTelemetryCollector>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Aura.Core.Telemetry.RunTelemetryCollector>>();
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var artifactsPath = Path.Combine(localAppData, "Aura", "jobs");
    return new Aura.Core.Telemetry.RunTelemetryCollector(logger, artifactsPath);
});
builder.Services.AddSingleton<Aura.Core.Telemetry.TelemetryIntegration>();

// Register Smart Provider Selector
builder.Services.AddSingleton<Aura.Core.Services.Providers.SmartProviderSelector>();

// Register ML Training services
builder.Services.AddSingleton<Aura.Core.Services.ML.AnnotationStorageService>();
builder.Services.AddSingleton<Aura.Core.Services.ML.ModelTrainingService>();
builder.Services.AddSingleton<Aura.Core.ML.ModelManager>();
builder.Services.AddSingleton<Aura.Core.Services.ML.MlTrainingWorker>();
builder.Services.AddSingleton<Aura.Core.Services.ML.PreflightCheckService>();
builder.Services.AddSingleton<Aura.Core.Services.ML.TrainingAuditService>();
builder.Services.AddSingleton<Aura.Core.Services.ML.LabelingFocusAdvisor>();
builder.Services.AddSingleton<Aura.Core.Services.ML.PostTrainingAnalysisService>();

// Register Advanced Mode service
builder.Services.AddSingleton<Aura.Core.Services.AdvancedModeService>();

// Configure Kestrel to listen on specific port with environment variable overrides
var apiUrl = Environment.GetEnvironmentVariable("AURA_API_URL") 
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") 
    ?? "http://127.0.0.1:5005";
builder.WebHost.UseUrls(apiUrl);

// Configure Kestrel for large file uploads
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 100L * 1024L * 1024L * 1024L; // 100GB max request size
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10); // Increased timeout for large uploads
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

// Apply database migrations
// Initialize database with enhanced error handling and recovery
Log.Information("Initializing database system...");
try
{
    var dbInitService = app.Services.GetRequiredService<Aura.Core.Services.DatabaseInitializationService>();
    var initResult = await dbInitService.InitializeAsync();

    if (initResult.Success)
    {
        Log.Information(
            "Database initialization completed successfully in {Duration}ms. WAL mode: {WalMode}, Integrity: {Integrity}",
            initResult.DurationMs, initResult.WalModeEnabled, initResult.IntegrityCheck);

        if (initResult.RepairAttempted)
        {
            Log.Warning("Database repair was performed during initialization");
        }

        // Initialize configuration system with defaults
        var configManager = app.Services.GetRequiredService<Aura.Core.Services.ConfigurationManager>();
        await configManager.InitializeAsync();
        Log.Information("Configuration system initialized successfully");
    }
    else
    {
        Log.Error(
            "Database initialization failed: {Error}. Path writable: {PathWritable}, Exists: {Exists}",
            initResult.Error, initResult.PathWritable, initResult.DatabaseExists);
        
        if (!initResult.PathWritable)
        {
            Log.Error(
                "Database path is not writable: {Path}. Please check file system permissions.",
                initResult.DatabasePath);
        }
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Critical error during database initialization");
    Log.Warning("Application will continue with degraded database functionality");
}


Log.Information("=== Aura Video Studio API Starting ===");
Log.Information("Initialization Phase 1: Service Registration Complete");

// Perform configuration validation
Log.Information("Initialization Phase 2: Configuration Validation");
var configValidator = app.Services.GetRequiredService<ConfigurationValidator>();
var configResult = configValidator.Validate();
if (!configResult.IsValid)
{
    Log.Error("Configuration validation failed with critical issues. Application cannot start.");
    Log.Error("Please fix the configuration issues listed above and restart the application.");
    // Exit if configuration validation has critical issues
    Environment.Exit(1);
}

// Perform startup validation - warn on non-critical issues but continue
Log.Information("Initialization Phase 3: Running Startup Validation");
var startupValidator = app.Services.GetRequiredService<Aura.Api.Services.StartupValidator>();
if (!startupValidator.Validate())
{
    Log.Warning("Startup validation detected some issues. Application will attempt to start anyway.");
    Log.Warning("If you experience problems, please review the errors above and check:");
    Log.Warning("  - File system permissions");
    Log.Warning("  - Antivirus/firewall settings");
    Log.Warning("  - Available disk space");
    // Don't exit - let the application start and users can fix issues via the UI
}
else
{
    Log.Information("Startup validation completed successfully");
}

// Perform model catalog preflight check
Log.Information("Initialization Phase 3.1: Model Catalog Preflight Check");
try
{
    using var scope = app.Services.CreateScope();
    var modelCatalog = scope.ServiceProvider.GetService<Aura.Core.AI.Adapters.ModelCatalog>();
    var providerSettings = scope.ServiceProvider.GetService<Aura.Core.Configuration.ProviderSettings>();
    
    if (modelCatalog != null && providerSettings != null)
    {
        Log.Information("Starting model catalog preflight validation...");
        
        var apiKeys = new Dictionary<string, string>();
        var openAiKey = providerSettings.GetOpenAiApiKey();
        if (!string.IsNullOrWhiteSpace(openAiKey))
            apiKeys["openai"] = openAiKey;
            
        var geminiKey = providerSettings.GetGeminiApiKey();
        if (!string.IsNullOrWhiteSpace(geminiKey))
            apiKeys["gemini"] = geminiKey;
        
        var ollamaUrl = providerSettings.GetOllamaUrl();
        
        var providersToCheck = new Dictionary<string, string>
        {
            ["OpenAI"] = "gpt-4o-mini",
            ["Anthropic"] = "claude-3-5-sonnet-20241022",
            ["Gemini"] = "gemini-1.5-pro",
            ["Ollama"] = "llama3.1"
        };
        
        var results = await modelCatalog.PreflightCheckAsync(providersToCheck, apiKeys, ollamaUrl);
        
        var availableCount = results.Count(r => r.Value);
        Log.Information("Model catalog preflight completed: {Available}/{Total} providers available",
            availableCount, results.Count);
        
        if (availableCount == 0)
        {
            Log.Warning("No LLM providers are available. The application will use RuleBased fallback provider.");
        }
    }
    else
    {
        Log.Debug("Model catalog or provider settings not available - skipping preflight check");
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Model catalog preflight check failed. Application will continue with static registry.");
}

// Perform FFmpeg detection and validation
Log.Information("Initialization Phase 3.5: FFmpeg Detection and Validation");
try
{
    using var scope = app.Services.CreateScope();
    var ffmpegOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Aura.Core.Configuration.FFmpegOptions>>();
    var hardwareDetector = scope.ServiceProvider.GetRequiredService<HardwareDetector>();
    
    Log.Information("FFmpeg Configuration:");
    Log.Information("  Explicit Path: {Path}", string.IsNullOrEmpty(ffmpegOptions.Value.ExecutablePath) ? "(auto-detect)" : ffmpegOptions.Value.ExecutablePath);
    Log.Information("  Search Paths: {Count} configured", ffmpegOptions.Value.SearchPaths.Count);
    Log.Information("  Minimum Version Required: {Version}", string.IsNullOrEmpty(ffmpegOptions.Value.RequireMinimumVersion) ? "(none)" : ffmpegOptions.Value.RequireMinimumVersion);
    
    // Attempt to detect FFmpeg
    var systemProfile = await hardwareDetector.DetectSystemAsync();
    
    // Try to get FFmpeg path from locator
    var ffmpegLocator = scope.ServiceProvider.GetRequiredService<Aura.Core.Dependencies.IFfmpegLocator>();
    var ffmpegPath = await ffmpegLocator.GetEffectiveFfmpegPathAsync();
    
    if (!string.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath))
    {
        Log.Information("✓ FFmpeg detected successfully");
        Log.Information("  Path: {Path}", ffmpegPath);
        
        // Try to get version
        try
        {
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
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                var versionLine = output.Split('\n').FirstOrDefault(l => l.Contains("ffmpeg version"));
                if (!string.IsNullOrEmpty(versionLine))
                {
                    Log.Information("  Version Info: {Version}", versionLine.Trim());
                }
            }
        }
        catch (Exception versionEx)
        {
            Log.Warning(versionEx, "Could not determine FFmpeg version");
        }
    }
    else
    {
        if (app.Environment.IsProduction())
        {
            Log.Warning("═══════════════════════════════════════════════════════════════");
            Log.Warning("⚠ WARNING: FFmpeg not found");
            Log.Warning("═══════════════════════════════════════════════════════════════");
            Log.Warning("FFmpeg is required for video generation but was not detected.");
            Log.Warning("Video rendering will NOT work until FFmpeg is installed.");
            Log.Warning("");
            Log.Warning("Configured search paths:");
            foreach (var path in ffmpegOptions.Value.SearchPaths)
            {
                Log.Warning("  - {Path}", path);
            }
            Log.Warning("");
            Log.Warning("To fix this issue:");
            Log.Warning("  1. Install FFmpeg from https://ffmpeg.org/download.html");
            Log.Warning("  2. Add FFmpeg to your system PATH");
            Log.Warning("  3. Or configure FFmpeg:ExecutablePath in appsettings.json");
            Log.Warning("═══════════════════════════════════════════════════════════════");
        }
        else
        {
            Log.Information("FFmpeg not found - this is normal for development");
            Log.Information("Video generation will use fallback or manual installation");
        }
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Error during FFmpeg detection - continuing startup anyway");
}

// Add correlation ID middleware early in the pipeline
app.UseCorrelationId();

// Add response compression (before most other middleware)
app.UseResponseCompression();

// Add response caching headers middleware
app.UseMiddleware<Aura.Api.Middleware.ResponseCachingMiddleware>();

// Add performance tracking middleware (after correlation ID)
app.UsePerformanceTracking();

// Add request validation middleware (after correlation ID)
app.UseRequestValidation();

// Add request logging middleware (after correlation ID)
app.UseRequestLogging();

// Add new global exception handler (ASP.NET Core IExceptionHandler pattern)
// This replaces the legacy ExceptionHandlingMiddleware
app.UseExceptionHandler();

// Configure the HTTP request pipeline
// Enable Swagger in all environments for contract testing
app.UseSwagger();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Aura Video Studio API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();

// Add routing BEFORE static files (API routes take precedence)
app.UseRouting();

// Authentication middleware (after routing)
app.UseApiAuthentication();

// Add first-run wizard check middleware (checks if setup is completed)
app.UseFirstRunCheck();

// Add AspNetCoreRateLimit middleware after routing and before authorization
app.UseIpRateLimiting();

// Add Performance Telemetry middleware
app.UseMiddleware<Aura.Api.Telemetry.PerformanceMiddleware>();

// Map health check endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // Skip all registered checks for liveness - just return 200 if app is running
    AllowCachingResponses = false
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true, // Run all registered checks for readiness
    AllowCachingResponses = false,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var result = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                data = e.Value.Data
            }).ToArray()
        };
        
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Add controller routing
app.MapControllers();

// Serve static files from wwwroot with proper content types
var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(wwwrootPath))
{
    // Validate that wwwroot has the minimum required files
    var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
    var assetsPath = Path.Combine(wwwrootPath, "assets");
    var fileCount = Directory.GetFiles(wwwrootPath, "*", SearchOption.AllDirectories).Length;
    
    if (!File.Exists(indexHtmlPath))
    {
        Log.Warning("=================================================================");
        Log.Warning("wwwroot directory exists but index.html is missing: {Path}", wwwrootPath);
        Log.Warning("Files found in wwwroot: {Count}", fileCount);
        Log.Warning("The web UI will not be available. Please ensure the build completed successfully.");
        Log.Warning("Visit http://127.0.0.1:5005/diag for more diagnostics.");
        Log.Warning("=================================================================");
    }
    else if (!Directory.Exists(assetsPath))
    {
        Log.Warning("=================================================================");
        Log.Warning("index.html found but assets directory is missing: {Path}", assetsPath);
        Log.Warning("Files found in wwwroot: {Count}", fileCount);
        Log.Warning("The web UI may not function correctly. JavaScript and CSS may be missing.");
        Log.Warning("Visit http://127.0.0.1:5005/diag for more diagnostics.");
        Log.Warning("=================================================================");
    }
    else
    {
        Log.Information("=================================================================");
        Log.Information("Static UI: ENABLED");
        Log.Information("  Path: {Path}", wwwrootPath);
        Log.Information("  Files: {Count}", fileCount);
        Log.Information("  index.html: ✓");
        Log.Information("  assets/: ✓");
        Log.Information("  SPA fallback: ACTIVE (handles client-side routing)");
        Log.Information("=================================================================");
        
        // Serve index.html as default file for root requests
        app.UseDefaultFiles();
        
        // Configure static file serving with proper MIME types
        var staticFileOptions = new StaticFileOptions
        {
            ContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider
            {
                Mappings =
                {
                    [".js"] = "application/javascript",
                    [".mjs"] = "application/javascript",
                    [".json"] = "application/json",
                    [".css"] = "text/css",
                    [".map"] = "application/json",
                    [".svg"] = "image/svg+xml",
                    [".webp"] = "image/webp",
                    [".wasm"] = "application/wasm",
                    [".woff"] = "font/woff",
                    [".woff2"] = "font/woff2",
                    [".ttf"] = "font/ttf",
                    [".eot"] = "application/vnd.ms-fontobject"
                }
            },
            OnPrepareResponse = ctx =>
            {
                // Add caching headers for static assets (not for index.html)
                if (!ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                {
                    // Cache static assets for 1 year (they have content hashes in filenames)
                    ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
                }
                else
                {
                    // Never cache index.html to ensure latest version is always served
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                    ctx.Context.Response.Headers.Append("Expires", "0");
                }
            }
        };
        
        app.UseStaticFiles(staticFileOptions);
    }
}
else
{
    Log.Error("=================================================================");
    Log.Error("CRITICAL: wwwroot directory not found at: {Path}", wwwrootPath);
    Log.Error("The web UI cannot be served without this directory.");
    Log.Error("=================================================================");
    Log.Error("");
    Log.Error("This usually means one of the following:");
    Log.Error("  1. The portable build did not complete successfully");
    Log.Error("  2. The frontend build (npm run build) failed");
    Log.Error("  3. The wwwroot folder was not extracted from the ZIP");
    Log.Error("");
    Log.Error("To fix this issue:");
    Log.Error("  - Re-extract the portable ZIP file completely");
    Log.Error("  - Or rebuild the application with: scripts\\packaging\\build-portable.ps1");
    Log.Error("");
    Log.Error("The API will continue to run, but accessing http://127.0.0.1:5005");
    Log.Error("in your browser will show a blank page or 404 error.");
    Log.Error("Visit http://127.0.0.1:5005/diag for diagnostics.");
    Log.Error("=================================================================");
}

// API endpoints are grouped under /api prefix
var apiGroup = app.MapGroup("/api");

// Health check endpoints
apiGroup.MapGet("/health/live", (Aura.Api.Services.HealthCheckService healthService) =>
{
    var result = healthService.CheckLiveness();
    return Results.Ok(result);
})
.WithName("HealthLive")
.WithOpenApi();

apiGroup.MapGet("/health/ready", async (Aura.Api.Services.HealthCheckService healthService, CancellationToken ct) =>
{
    var result = await healthService.CheckReadinessAsync(ct);
    
    // Return 503 Service Unavailable if unhealthy, 200 OK if healthy or degraded
    var statusCode = result.Status == Aura.Api.Models.HealthStatus.Unhealthy ? 503 : 200;
    return Results.Json(result, statusCode: statusCode);
})
.WithName("HealthReady")
.WithOpenApi();

// Health summary endpoint - high-level system status
apiGroup.MapGet("/health/summary", async (Aura.Api.Services.HealthDiagnosticsService healthDiagnostics, CancellationToken ct) =>
{
    try
    {
        var correlationId = Guid.NewGuid().ToString();
        Log.Information("Health summary requested, CorrelationId: {CorrelationId}", correlationId);
        
        var result = await healthDiagnostics.GetHealthSummaryAsync(ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error retrieving health summary");
        return Results.Problem("Error retrieving health summary", statusCode: 500);
    }
})
.WithName("HealthSummary")
.WithOpenApi();

// Health details endpoint - per-check detailed diagnostics
apiGroup.MapGet("/health/details", async (Aura.Api.Services.HealthDiagnosticsService healthDiagnostics, CancellationToken ct) =>
{
    try
    {
        var correlationId = Guid.NewGuid().ToString();
        Log.Information("Health details requested, CorrelationId: {CorrelationId}", correlationId);
        
        var result = await healthDiagnostics.GetHealthDetailsAsync(ct);
        
        // Return 503 if system is not ready (has failed required checks)
        var statusCode = result.IsReady ? 200 : 503;
        return Results.Json(result, statusCode: statusCode);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error retrieving health details");
        return Results.Problem("Error retrieving health details", statusCode: 500);
    }
})
.WithName("HealthDetails")
.WithOpenApi();

// First-run diagnostics endpoint - comprehensive system check with actionable guidance
apiGroup.MapGet("/health/first-run", async (Aura.Api.Services.FirstRunDiagnostics diagnostics, CancellationToken ct) =>
{
    try
    {
        var result = await diagnostics.RunDiagnosticsAsync(ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error running first-run diagnostics");
        return Results.Problem("Error running diagnostics", statusCode: 500);
    }
})
.WithName("FirstRunDiagnostics")
.WithOpenApi();

// Auto-fix endpoint - attempt to automatically resolve common issues
apiGroup.MapPost("/health/auto-fix", async (
    [FromBody] Dictionary<string, object>? options,
    Aura.Api.Services.FirstRunDiagnostics diagnostics,
    Aura.Core.Dependencies.FfmpegInstaller ffmpegInstaller,
    CancellationToken ct) =>
{
    try
    {
        var issueCode = options?.ContainsKey("issueCode") == true 
            ? options["issueCode"]?.ToString() 
            : null;

        if (string.IsNullOrEmpty(issueCode))
        {
            return Results.BadRequest(new { success = false, message = "Issue code is required" });
        }

        // Handle FFmpeg installation
        if (issueCode == "E302-FFMPEG_NOT_FOUND")
        {
            Log.Information("Attempting auto-fix for FFmpeg installation");
            
            var progress = new Progress<Aura.Core.Downloads.HttpDownloadProgress>(p =>
            {
                Log.Information("FFmpeg download progress: {Percent}%", p.PercentComplete);
            });

            // Determine the best mirror to use based on platform
            var mirrors = new List<string>();
            if (OperatingSystem.IsWindows())
            {
                mirrors.Add("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip");
            }
            else if (OperatingSystem.IsLinux())
            {
                mirrors.Add("https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz");
            }

            if (!mirrors.Any())
            {
                return Results.Ok(new 
                { 
                    success = false, 
                    message = "Automatic FFmpeg installation is not supported on this platform. Please install manually."
                });
            }

            var result = await ffmpegInstaller.InstallFromMirrorsAsync(
                mirrors.ToArray(),
                "latest",
                null,
                progress,
                ct);

            if (result.Success)
            {
                Log.Information("FFmpeg installed successfully at: {Path}", result.FfmpegPath);
                return Results.Ok(new 
                { 
                    success = true, 
                    message = "FFmpeg installed successfully",
                    ffmpegPath = result.FfmpegPath
                });
            }
            else
            {
                Log.Error("FFmpeg installation failed: {Error}", result.ErrorMessage);
                return Results.Ok(new 
                { 
                    success = false, 
                    message = $"FFmpeg installation failed: {result.ErrorMessage}"
                });
            }
        }

        return Results.Ok(new 
        { 
            success = false, 
            message = $"Auto-fix not available for issue: {issueCode}"
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during auto-fix");
        return Results.Problem($"Error during auto-fix: {ex.Message}", statusCode: 500);
    }
})
.WithName("AutoFixIssue")
.WithOpenApi();

// Legacy health check endpoint for backward compatibility
apiGroup.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

// Enhanced diagnostic page for debugging static file serving and API connectivity
app.MapGet("/diag", (HttpContext httpContext) =>
{
    var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var wwwrootExists = Directory.Exists(wwwrootPath);
    var indexHtmlPath = Path.Combine(wwwrootPath, "index.html");
    var indexHtmlExists = File.Exists(indexHtmlPath);
    var assetsPath = Path.Combine(wwwrootPath, "assets");
    var assetsExists = Directory.Exists(assetsPath);
    var fileCount = wwwrootExists ? Directory.GetFiles(wwwrootPath, "*", SearchOption.AllDirectories).Length : 0;
    var assetCount = assetsExists ? Directory.GetFiles(assetsPath, "*", SearchOption.TopDirectoryOnly).Length : 0;
    
    // Try to find a sample JS and CSS file for testing
    string? sampleJsFile = null;
    string? sampleCssFile = null;
    if (assetsExists)
    {
        var jsFiles = Directory.GetFiles(assetsPath, "*.js", SearchOption.TopDirectoryOnly);
        var cssFiles = Directory.GetFiles(assetsPath, "*.css", SearchOption.TopDirectoryOnly);
        sampleJsFile = jsFiles.Length > 0 ? Path.GetFileName(jsFiles[0]) : null;
        sampleCssFile = cssFiles.Length > 0 ? Path.GetFileName(cssFiles[0]) : null;
    }
    
    var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>Aura Video Studio - Diagnostics</title>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; background: #f5f5f5; }}
        .container {{ max-width: 900px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        h1 {{ color: #0078d4; border-bottom: 2px solid #0078d4; padding-bottom: 10px; }}
        h2 {{ color: #333; margin-top: 30px; }}
        .status {{ margin: 20px 0; padding: 15px; border-radius: 4px; }}
        .ok {{ background: #dff0d8; border: 1px solid #d6e9c6; color: #3c763d; }}
        .error {{ background: #f2dede; border: 1px solid #ebccd1; color: #a94442; }}
        .warning {{ background: #fcf8e3; border: 1px solid #faebcc; color: #8a6d3b; }}
        .info {{ background: #d9edf7; border: 1px solid #bce8f1; color: #31708f; }}
        .detail {{ font-size: 0.9em; color: #666; margin-top: 5px; }}
        ul {{ list-style-type: none; padding: 0; }}
        li {{ padding: 5px 0; }}
        .check {{ display: inline-block; width: 20px; }}
        .timestamp {{ font-size: 0.8em; color: #999; margin-top: 20px; text-align: center; }}
        code {{ background: #f4f4f4; padding: 2px 6px; border-radius: 3px; font-family: 'Consolas', monospace; }}
        .test-result {{ margin: 10px 0; padding: 10px; background: #f9f9f9; border-left: 4px solid #0078d4; }}
        button {{ 
            background: #0078d4; 
            color: white; 
            border: none; 
            padding: 10px 20px; 
            border-radius: 4px; 
            cursor: pointer; 
            margin: 5px;
        }}
        button:hover {{ background: #005a9e; }}
        #testResults {{ margin-top: 15px; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>🔍 Aura Video Studio Diagnostics</h1>
        
        <div class='status {(wwwrootExists && indexHtmlExists && assetsExists ? "ok" : "error")}'>
            <strong>Static File Hosting Status</strong>
            <ul>
                <li><span class='check'>{(wwwrootExists ? "✅" : "❌")}</span> wwwroot directory: {(wwwrootExists ? "FOUND" : "NOT FOUND")}</li>
                <li class='detail'>Path: <code>{wwwrootPath}</code></li>
                <li><span class='check'>{(indexHtmlExists ? "✅" : "❌")}</span> index.html: {(indexHtmlExists ? "FOUND" : "NOT FOUND")}</li>
                <li><span class='check'>{(assetsExists ? "✅" : "❌")}</span> assets directory: {(assetsExists ? "FOUND" : "NOT FOUND")}</li>
                <li class='detail'>Total files in wwwroot: {fileCount}</li>
                <li class='detail'>Asset files (.js/.css): {assetCount}</li>
            </ul>
        </div>
        
        <div class='status ok'>
            <strong>API Status</strong>
            <ul>
                <li><span class='check'>✅</span> API is reachable (you're seeing this page!)</li>
                <li class='detail'>Request URL: <code>{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}</code></li>
                <li class='detail'>Runtime Origin: <code>{httpContext.Request.Scheme}://{httpContext.Request.Host}</code></li>
                <li class='detail'>API Base: <code>{httpContext.Request.Scheme}://{httpContext.Request.Host}/api</code></li>
            </ul>
        </div>

        <h2>🧪 Asset Fetch Tests</h2>
        <div class='status info'>
            <p>Click the buttons below to test if assets can be fetched correctly:</p>
            <button onclick=""testAsset('/assets/{sampleJsFile ?? "index.js"}', 'application/javascript')"">Test JS File</button>
            <button onclick=""testAsset('/assets/{sampleCssFile ?? "index.css"}', 'text/css')"">Test CSS File</button>
            <button onclick=""testServiceWorker()"">Check Service Worker</button>
            <div id=""testResults""></div>
        </div>
        
        <h2>🔗 Quick Navigation Tests</h2>
        <div class='status info'>
            <ul>
                <li>• Navigate to <a href='/'>http://{httpContext.Request.Host}/</a> - Should show the app UI</li>
                <li>• Navigate to <a href='/api/healthz'>http://{httpContext.Request.Host}/api/healthz</a> - Should return JSON health status</li>
                <li>• Navigate to <a href='/dashboard'>http://{httpContext.Request.Host}/dashboard</a> - Should show dashboard (SPA route)</li>
                <li>• Try a hard refresh (Ctrl+F5) on any page - Should not show 404</li>
                <li>• Open browser DevTools Console - Check for errors</li>
            </ul>
        </div>
        
        {(!wwwrootExists || !indexHtmlExists ? @"
        <h2>⚠️ Issue Detected</h2>
        <div class='status error'>
            <p>The wwwroot directory or index.html is missing. This means the frontend build was not copied correctly.</p>
            <p><strong>To fix:</strong></p>
            <ul>
                <li>1. Ensure you built the frontend: <code>cd Aura.Web && npm run build</code></li>
                <li>2. Copy dist to wwwroot: <code>Copy-Item Aura.Web\dist\* -Destination Aura.Api\wwwroot\ -Recurse</code></li>
                <li>3. Or rebuild the portable package: <code>.\scripts\packaging\build-portable.ps1</code></li>
                <li>4. Restart the API server after copying files</li>
            </ul>
        </div>" : "")}
        
        <h2>📋 Troubleshooting Guide</h2>
        <div class='status info'>
            <strong>White/Blank Page Issues:</strong>
            <ul>
                <li>• Check that asset count above is > 0</li>
                <li>• Open browser DevTools → Console tab → Look for red errors</li>
                <li>• Open browser DevTools → Network tab → Check if .js/.css files return 200</li>
                <li>• Clear browser cache (Ctrl+Shift+Delete) and hard refresh (Ctrl+F5)</li>
                <li>• Check if Content-Security-Policy is blocking scripts (Console warnings)</li>
            </ul>
            
            <strong>Deep Link Refresh Fails:</strong>
            <ul>
                <li>• Ensure SPA fallback is active (check server logs for ""SPA fallback configured"")</li>
                <li>• Navigate to /dashboard, refresh → should still show UI, not 404</li>
                <li>• If fallback doesn't work, consider using HashRouter (/#/route URLs)</li>
            </ul>
            
            <strong>Service Worker or Stale Cache:</strong>
            <ul>
                <li>• Click ""Check Service Worker"" button above</li>
                <li>• If active, unregister it: DevTools → Application → Service Workers → Unregister</li>
                <li>• Clear all site data: DevTools → Application → Clear storage → Clear site data</li>
            </ul>
        </div>
        
        <div class='timestamp'>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>
    </div>
    
    <script>
        // Display runtime environment info
        window.addEventListener('DOMContentLoaded', function() {{
            var envInfo = document.createElement('div');
            envInfo.className = 'status info';
            envInfo.style.marginTop = '20px';
            envInfo.innerHTML = '<strong>Client Runtime Info:</strong>' +
                '<ul>' +
                '<li>Window Origin: <code>' + window.location.origin + '</code></li>' +
                '<li>Current URL: <code>' + window.location.href + '</code></li>' +
                '<li>User Agent: <code>' + navigator.userAgent + '</code></li>' +
                '</ul>';
            document.querySelector('.container').insertBefore(envInfo, document.querySelector('.timestamp'));
        }});
        
        function testAsset(path, expectedType) {{
            var resultsDiv = document.getElementById('testResults');
            resultsDiv.innerHTML = '<p>Testing: <code>' + path + '</code>...</p>';
            
            fetch(path)
                .then(function(response) {{
                    var contentType = response.headers.get('Content-Type') || 'unknown';
                    var status = response.status;
                    
                    if (status === 200) {{
                        var typeMatch = contentType.includes(expectedType);
                        resultsDiv.innerHTML = '<div class=""test-result ' + (typeMatch ? 'ok' : 'warning') + '"">' +
                            '<strong>✓ Asset Fetch Test</strong><br>' +
                            'URL: <code>' + path + '</code><br>' +
                            'Status: ' + status + ' OK<br>' +
                            'Content-Type: <code>' + contentType + '</code>' + 
                            (typeMatch ? ' ✓ Correct' : ' ⚠️ Expected: ' + expectedType) +
                            '</div>';
                    }} else {{
                        resultsDiv.innerHTML = '<div class=""test-result error"">' +
                            '<strong>✗ Asset Fetch Failed</strong><br>' +
                            'URL: <code>' + path + '</code><br>' +
                            'Status: ' + status + '<br>' +
                            'This asset file is missing or inaccessible.' +
                            '</div>';
                    }}
                }})
                .catch(function(err) {{
                    resultsDiv.innerHTML = '<div class=""test-result error"">' +
                        '<strong>✗ Fetch Error</strong><br>' +
                        'URL: <code>' + path + '</code><br>' +
                        'Error: ' + err.message +
                        '</div>';
                }});
        }}
        
        function testServiceWorker() {{
            var resultsDiv = document.getElementById('testResults');
            
            if ('serviceWorker' in navigator) {{
                navigator.serviceWorker.getRegistrations().then(function(registrations) {{
                    if (registrations.length > 0) {{
                        var swInfo = registrations.map(function(reg) {{
                            return 'Scope: <code>' + reg.scope + '</code>, State: ' + 
                                   (reg.active ? reg.active.state : 'inactive');
                        }}).join('<br>');
                        
                        resultsDiv.innerHTML = '<div class=""test-result warning"">' +
                            '<strong>⚠️ Service Worker Detected</strong><br>' +
                            'Active service workers found:<br>' + swInfo + '<br><br>' +
                            '<strong>Action Required:</strong> If you\'re experiencing issues, unregister the service worker:<br>' +
                            '1. Open DevTools → Application tab<br>' +
                            '2. Click ""Service Workers"" in the sidebar<br>' +
                            '3. Click ""Unregister"" next to each service worker<br>' +
                            '4. Hard refresh (Ctrl+F5)' +
                            '</div>';
                    }} else {{
                        resultsDiv.innerHTML = '<div class=""test-result ok"">' +
                            '<strong>✓ No Service Worker</strong><br>' +
                            'No service workers are registered for this origin.' +
                            '</div>';
                    }}
                }});
            }} else {{
                resultsDiv.innerHTML = '<div class=""test-result info"">' +
                    '<strong>Service Workers Not Supported</strong><br>' +
                    'This browser does not support service workers.' +
                    '</div>';
            }}
        }}
    </script>
</body>
</html>";
    
    return Results.Content(html, "text/html");
})
.WithName("DiagnosticsPage")
.WithOpenApi();

// Log viewer endpoint - retrieve logs with optional filtering
apiGroup.MapGet("/logs", (HttpContext httpContext, string? level = null, string? correlationId = null, int lines = 500) =>
{
    // Local helper method for parsing log lines
    static object? ParseLogLine(string line)
    {
        try
        {
            // Expected format: [timestamp] [LEVEL] [CorrelationId] message
            // Example: [2025-10-10 22:39:40.123 +00:00] [INF] [abc123] Application started
            
            if (!line.StartsWith("[")) return null;

            var parts = line.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;

            var timestamp = parts[0].Trim();
            var level = parts[1].Trim();
            var correlationId = parts.Length > 2 ? parts[2].Trim() : "";
            var message = parts.Length > 3 ? string.Join(" ", parts.Skip(3)) : "";

            return new
            {
                timestamp,
                level,
                correlationId,
                message,
                rawLine = line
            };
        }
        catch
        {
            return null;
        }
    }

    try
    {
        var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logsDirectory))
        {
            return Results.Ok(new { logs = Array.Empty<object>(), message = "No logs directory found" });
        }

        // Get the most recent log file
        var logFiles = Directory.GetFiles(logsDirectory, "aura-api-*.log")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .ToList();

        if (logFiles.Count == 0)
        {
            return Results.Ok(new { logs = Array.Empty<object>(), message = "No log files found" });
        }

        var latestLogFile = logFiles[0];
        var allLines = File.ReadAllLines(latestLogFile);
        var logEntries = new List<object>();

        // Parse log lines (take last N lines for performance)
        var linesToRead = Math.Min(lines, allLines.Length);
        var startIndex = Math.Max(0, allLines.Length - linesToRead);

        for (int i = startIndex; i < allLines.Length; i++)
        {
            var line = allLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Parse log line: [timestamp] [LEVEL] [CorrelationId] message
            var parsed = ParseLogLine(line);
            if (parsed == null) continue;

            // Apply filters
            var parsedLevel = (parsed.GetType().GetProperty("level")?.GetValue(parsed) as string) ?? "";
            var parsedCorrelationId = (parsed.GetType().GetProperty("correlationId")?.GetValue(parsed) as string) ?? "";

            if (!string.IsNullOrEmpty(level) && !parsedLevel.Equals(level, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(correlationId) && !parsedCorrelationId.Equals(correlationId, StringComparison.OrdinalIgnoreCase))
                continue;

            logEntries.Add(parsed);
        }

        return Results.Ok(new { logs = logEntries, file = Path.GetFileName(latestLogFile), totalLines = allLines.Length });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error reading logs");
        return Results.Problem("Error reading log files", statusCode: 500);
    }
})
.WithName("GetLogs")
.WithOpenApi();

// Open logs folder in file explorer
apiGroup.MapPost("/logs/open-folder", () =>
{
    try
    {
        var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        
        // Create logs directory if it doesn't exist
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        // Platform-specific logic to open folder in file explorer
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start("explorer.exe", logsDirectory);
        }
        else if (OperatingSystem.IsMacOS())
        {
            System.Diagnostics.Process.Start("open", logsDirectory);
        }
        else if (OperatingSystem.IsLinux())
        {
            // Try xdg-open first (most common), fallback to nautilus/dolphin
            try
            {
                System.Diagnostics.Process.Start("xdg-open", logsDirectory);
            }
            catch
            {
                // Fallback options for Linux
                try
                {
                    System.Diagnostics.Process.Start("nautilus", logsDirectory);
                }
                catch
                {
                    System.Diagnostics.Process.Start("dolphin", logsDirectory);
                }
            }
        }
        else
        {
            return Results.Problem("Opening folders is not supported on this platform", statusCode: 501);
        }

        return Results.Ok(new { success = true, path = logsDirectory });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error opening logs folder");
        return Results.Problem("Error opening logs folder. Please navigate manually.", statusCode: 500);
    }
})
.WithName("OpenLogsFolder")
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
            Pacing: ApiV1.EnumMappings.ToCore(request.Pacing),
            Density: ApiV1.EnumMappings.ToCore(request.Density),
            Style: request.Style
        );
        
        return Results.Ok(new { success = true, plan });
    }
    catch (JsonException ex)
    {
        Log.Error(ex, "Invalid enum value in plan request");
        return Results.Problem(
            detail: ex.Message,
            statusCode: 400,
            title: "Invalid Enum Value",
            type: "https://docs.aura.studio/errors/E303");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error creating plan");
        return Results.Problem("Error creating plan", statusCode: 500);
    }
})
.WithName("CreatePlan")
.WithOpenApi();

// Planner recommendations endpoint
apiGroup.MapPost("/planner/recommendations", async (
    [FromBody] RecommendationsRequestDto request, 
    IRecommendationService recommendationService,
    CancellationToken ct) =>
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
            Audience: request.Audience ?? "General",
            Goal: request.Goal ?? "Inform",
            Tone: request.Tone ?? "Informative",
            Language: request.Language ?? "en-US",
            Aspect: ApiV1.EnumMappings.ToCore(request.Aspect ?? ApiV1.Aspect.Widescreen16x9)
        );
        
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(request.TargetDurationMinutes),
            Pacing: ApiV1.EnumMappings.ToCore(request.Pacing ?? ApiV1.Pacing.Conversational),
            Density: ApiV1.EnumMappings.ToCore(request.Density ?? ApiV1.Density.Balanced),
            Style: request.Style ?? "Standard"
        );

        var constraints = request.Constraints != null 
            ? new RecommendationConstraints(
                MaxSceneCount: request.Constraints.MaxSceneCount,
                MinSceneCount: request.Constraints.MinSceneCount,
                MaxBRollPercentage: request.Constraints.MaxBRollPercentage,
                MaxReadingLevel: request.Constraints.MaxReadingLevel)
            : null;

        var recommendationRequest = new RecommendationRequest(
            Brief: brief,
            PlanSpec: planSpec,
            AudiencePersona: request.AudiencePersona,
            Constraints: constraints
        );

        Log.Information("Generating recommendations for topic: {Topic}, duration: {Duration} min", 
            request.Topic, request.TargetDurationMinutes);
        
        var recommendations = await recommendationService.GenerateRecommendationsAsync(recommendationRequest, ct);
        
        Log.Information("Recommendations generated successfully");
        return Results.Ok(new { success = true, recommendations });
    }
    catch (TaskCanceledException)
    {
        Log.Warning("Recommendation generation was cancelled");
        return Results.Problem(
            detail: "Recommendation generation was cancelled",
            statusCode: 408,
            title: "Request Timeout",
            type: "https://docs.aura.studio/errors/E301");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating recommendations: {Message}", ex.Message);
        return Results.Problem(
            detail: $"Error generating recommendations: {ex.Message}",
            statusCode: 500,
            title: "Recommendation Service Failed",
            type: "https://docs.aura.studio/errors/E305");
    }
})
.WithName("GetPlannerRecommendations")
.WithOpenApi();

// Script generation endpoint
apiGroup.MapPost("/script", async (
    HttpContext httpContext,
    [FromBody] ScriptRequest request, 
    Aura.Core.Orchestrator.ScriptOrchestrator orchestrator,
    HardwareDetector hardwareDetector,
    CancellationToken ct) =>
{
    try
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            return ProblemDetailsHelper.CreateInvalidBrief("Topic is required");
        }
        
        if (request.TargetDurationMinutes <= 0 || request.TargetDurationMinutes > 120)
        {
            return ProblemDetailsHelper.CreateInvalidPlan("Target duration must be between 0 and 120 minutes");
        }
        
        var brief = new Brief(
            Topic: request.Topic,
            Audience: request.Audience,
            Goal: request.Goal,
            Tone: request.Tone,
            Language: request.Language,
            Aspect: ApiV1.EnumMappings.ToCore(request.Aspect)
        );
        
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(request.TargetDurationMinutes),
            Pacing: ApiV1.EnumMappings.ToCore(request.Pacing),
            Density: ApiV1.EnumMappings.ToCore(request.Density),
            Style: request.Style
        );
        
        // Determine provider tier from request or use default
        string preferredTier = request.ProviderTier ?? "Free";
        
        // Use per-stage script provider selection if provided
        if (request.ProviderSelection?.Script != null && request.ProviderSelection.Script != "Auto")
        {
            preferredTier = request.ProviderSelection.Script;
            Log.Information("Using per-stage script provider selection: {Provider}", preferredTier);
        }
        
        // Get system offline status
        var profile = await hardwareDetector.DetectSystemAsync();
        bool offlineOnly = profile.OfflineOnly;
        
        Log.Information("Generating script for topic: {Topic}, duration: {Duration} min, tier: {Tier}, offline: {Offline}", 
            request.Topic, request.TargetDurationMinutes, preferredTier, offlineOnly);
        
        var result = await orchestrator.GenerateScriptAsync(brief, planSpec, preferredTier, offlineOnly, ct);
        
        if (!result.Success)
        {
            Log.Error("Script generation failed: {ErrorCode} - {ErrorMessage}", result.ErrorCode, result.ErrorMessage);
            
            // Use ProblemDetailsHelper for consistent error responses with correlation ID
            return ProblemDetailsHelper.CreateScriptError(
                result.ErrorCode ?? "E300", 
                result.ErrorMessage ?? "Script generation failed",
                httpContext
            );
        }
        
        Log.Information("Script generated successfully with {Provider}: {Length} characters (fallback: {IsFallback})", 
            result.ProviderUsed, result.Script?.Length ?? 0, result.IsFallback);
        
        return Results.Ok(new 
        { 
            success = true, 
            script = result.Script,
            provider = result.ProviderUsed,
            isFallback = result.IsFallback
        });
    }
    catch (JsonException ex)
    {
        Log.Error(ex, "Invalid enum value in script request");
        return ProblemDetailsHelper.CreateScriptError("E303", ex.Message, httpContext);
    }
    catch (ArgumentException ex)
    {
        Log.Error(ex, "Invalid argument for script generation");
        return ProblemDetailsHelper.CreateScriptError("E303", ex.Message, httpContext);
    }
    catch (TaskCanceledException)
    {
        Log.Warning("Script generation was cancelled");
        return ProblemDetailsHelper.CreateScriptError("E301", "Script generation was cancelled", httpContext);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating script: {Message}", ex.Message);
        return ProblemDetailsHelper.CreateScriptError("E300", $"Error generating script: {ex.Message}", httpContext);
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
            Pause: ApiV1.EnumMappings.ToCore(request.PauseStyle)
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

// Captions endpoint
apiGroup.MapPost("/captions/generate", async ([FromBody] CaptionsRequest request, 
    [FromServices] Aura.Core.Captions.CaptionBuilder captionBuilder) =>
{
    try
    {
        var lines = request.Lines.Select(l => new ScriptLine(
            SceneIndex: l.SceneIndex,
            Text: l.Text,
            Start: TimeSpan.FromSeconds(l.StartSeconds),
            Duration: TimeSpan.FromSeconds(l.DurationSeconds)
        )).ToList();
        
        string captions = request.Format.ToUpperInvariant() == "VTT"
            ? captionBuilder.GenerateVtt(lines)
            : captionBuilder.GenerateSrt(lines);
        
        // Optionally save to file if path is provided
        string? filePath = null;
        if (!string.IsNullOrEmpty(request.OutputPath))
        {
            filePath = request.OutputPath;
            await System.IO.File.WriteAllTextAsync(filePath, captions);
            Log.Information("Captions saved to {Path}", filePath);
        }
        
        return Results.Ok(new { success = true, captions, filePath });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating captions");
        return Results.Problem("Error generating captions", statusCode: 500);
    }
})
.WithName("GenerateCaptions")
.WithOpenApi();

// Azure TTS endpoints
apiGroup.MapGet("/tts/azure/voices", async (
    [FromServices] Aura.Providers.Tts.AzureVoiceDiscovery voiceDiscovery,
    string? locale = null,
    string? gender = null,
    string? voiceType = null,
    CancellationToken ct = default) =>
{
    try
    {
        // Parse optional filters
        Aura.Core.Models.Voice.VoiceGender? genderFilter = gender?.ToLowerInvariant() switch
        {
            "male" => Aura.Core.Models.Voice.VoiceGender.Male,
            "female" => Aura.Core.Models.Voice.VoiceGender.Female,
            "neutral" => Aura.Core.Models.Voice.VoiceGender.Neutral,
            _ => null
        };

        Aura.Core.Models.Voice.VoiceType? typeFilter = voiceType?.ToLowerInvariant() switch
        {
            "neural" => Aura.Core.Models.Voice.VoiceType.Neural,
            "standard" => Aura.Core.Models.Voice.VoiceType.Standard,
            _ => null
        };

        var voices = await voiceDiscovery.GetVoicesAsync(locale, genderFilter, typeFilter, ct);

        // Convert to DTOs
        var voiceDtos = voices.Select(v => new Aura.Api.Models.ApiModels.V1.AzureVoiceDto(
            Id: v.Id,
            Name: v.Name,
            Locale: v.Locale,
            Gender: v.Gender.ToString(),
            VoiceType: v.VoiceType.ToString(),
            AvailableStyles: v.AvailableStyles,
            AvailableRoles: v.AvailableRoles,
            SupportedFeatures: GetFeatureNames(v.SupportedFeatures),
            Description: v.Description,
            LocalName: v.LocalName
        )).ToList();

        return Results.Ok(new { success = true, voices = voiceDtos, count = voiceDtos.Count });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error retrieving Azure voices");
        return Results.Problem("Error retrieving Azure voices", statusCode: 500);
    }
})
.WithName("GetAzureVoices")
.WithOpenApi();

apiGroup.MapGet("/tts/azure/voice/{voiceId}/capabilities", async (
    string voiceId,
    [FromServices] Aura.Providers.Tts.AzureVoiceDiscovery voiceDiscovery,
    CancellationToken ct) =>
{
    try
    {
        var voice = await voiceDiscovery.GetVoiceCapabilitiesAsync(voiceId, ct);
        
        if (voice == null)
        {
            return Results.NotFound(new { success = false, message = $"Voice '{voiceId}' not found" });
        }

        // Get style descriptions (hardcoded for now, could be enhanced)
        var styleDescriptions = GetStyleDescriptions();

        var capabilities = new Aura.Api.Models.ApiModels.V1.AzureVoiceCapabilitiesDto(
            VoiceId: voice.Id,
            Name: voice.Name,
            Locale: voice.Locale,
            Gender: voice.Gender.ToString(),
            VoiceType: voice.VoiceType.ToString(),
            AvailableStyles: voice.AvailableStyles,
            AvailableRoles: voice.AvailableRoles,
            StyleDescriptions: styleDescriptions.Where(kvp => voice.AvailableStyles.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            SupportedFeatures: GetFeatureNames(voice.SupportedFeatures)
        );

        return Results.Ok(new { success = true, capabilities });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error retrieving voice capabilities for {VoiceId}", voiceId);
        return Results.Problem("Error retrieving voice capabilities", statusCode: 500);
    }
})
.WithName("GetAzureVoiceCapabilities")
.WithOpenApi();

apiGroup.MapPost("/tts/azure/preview", async (
    [FromBody] Aura.Api.Models.ApiModels.V1.AzureTtsSynthesizeRequest request,
    [FromServices] Aura.Providers.Tts.AzureTtsProvider azureTtsProvider,
    CancellationToken ct) =>
{
    try
    {
        // Limit preview text length
        var previewText = request.Text.Length > 500 
            ? request.Text.Substring(0, 500) + "..." 
            : request.Text;

        // Convert DTO options to core options
        Aura.Core.Models.Voice.AzureTtsOptions? options = null;
        if (request.Options != null)
        {
            options = new Aura.Core.Models.Voice.AzureTtsOptions
            {
                Rate = request.Options.Rate ?? 0.0,
                Pitch = request.Options.Pitch ?? 0.0,
                Volume = request.Options.Volume ?? 1.0,
                Style = request.Options.Style,
                StyleDegree = request.Options.StyleDegree ?? 1.0,
                Role = request.Options.Role,
                AudioEffect = ParseAudioEffect(request.Options.AudioEffect),
                Emphasis = ParseEmphasis(request.Options.Emphasis)
            };
        }

        var audioPath = await azureTtsProvider.SynthesizeWithOptionsAsync(
            previewText, 
            request.VoiceId, 
            options, 
            ct);

        return Results.Ok(new { success = true, audioPath });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating Azure TTS preview");
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("AzureTtsPreview")
.WithOpenApi();

apiGroup.MapPost("/tts/azure/synthesize", async (
    [FromBody] Aura.Api.Models.ApiModels.V1.AzureTtsSynthesizeRequest request,
    [FromServices] Aura.Providers.Tts.AzureTtsProvider azureTtsProvider,
    CancellationToken ct) =>
{
    try
    {
        // Convert DTO options to core options
        Aura.Core.Models.Voice.AzureTtsOptions? options = null;
        if (request.Options != null)
        {
            options = new Aura.Core.Models.Voice.AzureTtsOptions
            {
                Rate = request.Options.Rate ?? 0.0,
                Pitch = request.Options.Pitch ?? 0.0,
                Volume = request.Options.Volume ?? 1.0,
                Style = request.Options.Style,
                StyleDegree = request.Options.StyleDegree ?? 1.0,
                Role = request.Options.Role,
                AudioEffect = ParseAudioEffect(request.Options.AudioEffect),
                Emphasis = ParseEmphasis(request.Options.Emphasis)
            };
        }

        var audioPath = await azureTtsProvider.SynthesizeWithOptionsAsync(
            request.Text, 
            request.VoiceId, 
            options, 
            ct);

        return Results.Ok(new { success = true, audioPath });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error synthesizing Azure TTS audio");
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("AzureTtsSynthesize")
.WithOpenApi();

// ML Model Training endpoint
apiGroup.MapPost("/ml/train/frame-importance", async (
    [FromBody] ApiV1.TrainFrameImportanceRequest request,
    Aura.Core.Services.ML.ModelTrainingService trainingService,
    CancellationToken ct) =>
{
    try
    {
        Log.Information("Received frame importance training request with {Count} annotations", 
            request.Annotations.Count);

        // Validate request
        if (request.Annotations == null || request.Annotations.Count == 0)
        {
            return Results.BadRequest(new ApiV1.TrainFrameImportanceResponse(
                Success: false,
                ModelPath: null,
                TrainingSamples: 0,
                TrainingDurationSeconds: 0,
                ErrorMessage: "No annotations provided"
            ));
        }

        // Convert DTOs to domain models
        var annotations = request.Annotations.Select(dto => 
            new Aura.Core.Models.FrameAnalysis.FrameAnnotation(
                FramePath: dto.FramePath,
                Rating: dto.Rating
            ));

        // Train the model
        var result = await trainingService.TrainFrameImportanceModelAsync(annotations, ct);

        // Convert result to DTO
        var response = new ApiV1.TrainFrameImportanceResponse(
            Success: result.Success,
            ModelPath: result.ModelPath,
            TrainingSamples: result.TrainingSamples,
            TrainingDurationSeconds: result.TrainingDuration.TotalSeconds,
            ErrorMessage: result.ErrorMessage
        );

        return result.Success 
            ? Results.Ok(response)
            : Results.Json(response, statusCode: 500);
    }
    catch (ArgumentException ex)
    {
        Log.Warning(ex, "Invalid training request");
        return Results.BadRequest(new ApiV1.TrainFrameImportanceResponse(
            Success: false,
            ModelPath: null,
            TrainingSamples: 0,
            TrainingDurationSeconds: 0,
            ErrorMessage: ex.Message
        ));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error training frame importance model");
        return Results.Problem("Error training model", statusCode: 500);
    }
})
.WithName("TrainFrameImportanceModel")
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

// Verify component integrity
apiGroup.MapGet("/downloads/{component}/verify", async (string component, Aura.Core.Dependencies.DependencyManager depManager) =>
{
    try
    {
        var result = await depManager.VerifyComponentAsync(component);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error verifying component");
        return Results.Problem($"Error verifying {component}", statusCode: 500);
    }
})
.WithName("VerifyComponent")
.WithOpenApi();

// Repair component
apiGroup.MapPost("/downloads/{component}/repair", async (string component, Aura.Core.Dependencies.DependencyManager depManager, CancellationToken ct) =>
{
    try
    {
        var progress = new Progress<Aura.Core.Dependencies.DownloadProgress>(p =>
        {
            Log.Information("Repair progress: {Component} - {Percent}%", component, p.PercentComplete);
        });

        await depManager.RepairComponentAsync(component, progress, ct);
        
        return Results.Ok(new { success = true, message = $"{component} repaired successfully" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error repairing component");
        return Results.Problem($"Error repairing {component}", statusCode: 500);
    }
})
.WithName("RepairComponent")
.WithOpenApi();

// Remove component
apiGroup.MapDelete("/downloads/{component}", async (string component, Aura.Core.Dependencies.DependencyManager depManager) =>
{
    try
    {
        await depManager.RemoveComponentAsync(component);
        return Results.Ok(new { success = true, message = $"{component} removed successfully" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error removing component");
        return Results.Problem($"Error removing {component}", statusCode: 500);
    }
})
.WithName("RemoveComponent")
.WithOpenApi();

// Get component directory path
apiGroup.MapGet("/downloads/{component}/folder", (string component, Aura.Core.Dependencies.DependencyManager depManager) =>
{
    try
    {
        var path = depManager.GetComponentDirectory(component);
        return Results.Ok(new { path });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting component folder");
        return Results.Problem($"Error getting folder for {component}", statusCode: 500);
    }
})
.WithName("GetComponentFolder")
.WithOpenApi();

// Get manual install instructions (for offline mode)
apiGroup.MapGet("/downloads/{component}/manual", (string component, Aura.Core.Dependencies.DependencyManager depManager) =>
{
    try
    {
        var instructions = depManager.GetManualInstallInstructions(component);
        return Results.Ok(instructions);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting manual instructions");
        return Results.Problem($"Error getting manual instructions for {component}", statusCode: 500);
    }
})
.WithName("GetManualInstructions")
.WithOpenApi();

// Dependency rescan endpoint
apiGroup.MapPost("/dependencies/rescan", async (
    Aura.Core.Dependencies.DependencyRescanService rescanService,
    CancellationToken ct) =>
{
    try
    {
        Log.Information("Starting dependency rescan");
        var report = await rescanService.RescanAllAsync(ct);
        
        return Results.Ok(new 
        { 
            success = true,
            scanTime = report.ScanTime,
            dependencies = report.Dependencies.Select(d => new
            {
                id = d.Id,
                displayName = d.DisplayName,
                status = d.Status.ToString(),
                path = d.Path,
                validationOutput = d.ValidationOutput,
                provenance = d.Provenance,
                errorMessage = d.ErrorMessage
            }).ToList()
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during dependency rescan");
        return Results.Problem("Error rescanning dependencies", statusCode: 500);
    }
})
.WithName("RescanDependencies")
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

// NOTE: Render/Job endpoints moved to JobsController and QuickController
// These stub endpoints are deprecated and redirect to proper controllers
// /api/render -> use JobsController POST /api/jobs
// /api/render/{id}/progress -> use JobsController GET /api/jobs/{id}
// /api/render/{id}/cancel -> use JobsController POST /api/jobs/{id}/cancel
// /api/queue -> use JobsController GET /api/jobs
// /api/quick/demo -> use QuickController POST /api/quick/demo


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

// SSE endpoint for job progress updates
apiGroup.MapGet("/jobs/{jobId}/stream", async (
    string jobId, 
    HttpContext context,
    JobRunner jobRunner,
    CancellationToken ct) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    
    try
    {
        Log.Information("SSE stream started for job {JobId}", jobId);
        
        // Send initial connection message
        var connectMsg = $"event: connected\ndata: {{\"jobId\":\"{jobId}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}\n\n";
        await context.Response.WriteAsync(connectMsg, ct);
        await context.Response.Body.FlushAsync(ct);
        
        // Poll job status and send updates
        var lastStatus = "";
        var lastPercent = -1;
        var lastStage = "";
        
        while (!ct.IsCancellationRequested)
        {
            var job = jobRunner.GetJob(jobId);
            if (job == null)
            {
                var errorMsg = $"event: error\ndata: {{\"error\":\"Job not found\"}}\n\n";
                await context.Response.WriteAsync(errorMsg, ct);
                await context.Response.Body.FlushAsync(ct);
                break;
            }
            
            // Send update if status changed
            if (job.Status.ToString() != lastStatus || job.Percent != lastPercent || job.Stage != lastStage)
            {
                lastStatus = job.Status.ToString();
                lastPercent = job.Percent;
                lastStage = job.Stage;
                
                var statusData = JsonSerializer.Serialize(new
                {
                    jobId = job.Id,
                    status = job.Status.ToString().ToLowerInvariant(),
                    stage = job.Stage,
                    percent = job.Percent,
                    errorMessage = job.ErrorMessage,
                    timestamp = DateTime.UtcNow
                });
                
                var updateMsg = $"event: progress\ndata: {statusData}\n\n";
                await context.Response.WriteAsync(updateMsg, ct);
                await context.Response.Body.FlushAsync(ct);
                
                Log.Information("SSE update sent for job {JobId}: {Status} {Percent}% {Stage}", 
                    jobId, job.Status, job.Percent, job.Stage);
            }
            
            // If job is done/failed/cancelled, send final message and close
            if (job.Status == JobStatus.Done || job.Status == JobStatus.Failed || job.Status == JobStatus.Canceled)
            {
                var completeData = JsonSerializer.Serialize(new
                {
                    jobId = job.Id,
                    status = job.Status.ToString().ToLowerInvariant(),
                    success = job.Status == JobStatus.Done,
                    outputPath = job.Artifacts.FirstOrDefault()?.Path,
                    errorMessage = job.ErrorMessage,
                    timestamp = DateTime.UtcNow
                });
                
                var completeMsg = $"event: complete\ndata: {completeData}\n\n";
                await context.Response.WriteAsync(completeMsg, ct);
                await context.Response.Body.FlushAsync(ct);
                
                Log.Information("SSE stream completed for job {JobId}: {Status}", jobId, job.Status);
                break;
            }
            
            // Send keepalive every 2 seconds
            await Task.Delay(2000, ct);
            
            var keepaliveMsg = $": keepalive\n\n";
            await context.Response.WriteAsync(keepaliveMsg, ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException)
    {
        Log.Information("SSE stream cancelled for job {JobId}", jobId);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error in SSE stream for job {JobId}", jobId);
        try
        {
            var errorMsg = $"event: error\ndata: {{\"error\":\"{ex.Message.Replace("\"", "\\\"")}\"}}\n\n";
            await context.Response.WriteAsync(errorMsg, ct);
            await context.Response.Body.FlushAsync(ct);
        }
        catch
        {
            // Client may have disconnected
        }
    }
})
.WithName("StreamJobProgress")
.WithOpenApi();


apiGroup.MapPost("/probes/run", async (HardwareDetector detector) =>
{
    try
    {
        await detector.RunHardwareProbeAsync();
        var profile = await detector.DetectSystemAsync();
        
        // Format response to match frontend expectations
        // Flat properties for easy frontend consumption + full profile for advanced use
        var gpuDisplay = profile.Gpu != null 
            ? $"{profile.Gpu.Vendor} {profile.Gpu.Model}" 
            : "Unable to detect GPU hardware";
        
        var response = new
        {
            success = true,
            // Flat properties for simple frontend access
            gpu = gpuDisplay,
            vramGB = profile.Gpu?.VramGB ?? 0,
            enableLocalDiffusion = profile.EnableSD,
            tier = profile.Tier.ToString(),
            ramGB = profile.RamGB,
            logicalCores = profile.LogicalCores,
            physicalCores = profile.PhysicalCores,
            enableNVENC = profile.EnableNVENC,
            detectionSuccessful = profile.Gpu != null,
            // Full profile object for advanced use cases
            profile = profile
        };
        
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error running probes");
        
        // Return graceful fallback response instead of 500 error
        return Results.Ok(new
        {
            success = true,
            gpu = "Unable to detect GPU hardware",
            vramGB = 0,
            enableLocalDiffusion = false,
            tier = "D",
            ramGB = 8,
            logicalCores = Environment.ProcessorCount,
            physicalCores = Environment.ProcessorCount / 2,
            enableNVENC = false,
            detectionSuccessful = false,
            error = "Hardware detection failed. You can continue and manually configure settings later."
        });
    }
})
.WithName("RunProbes")
.WithOpenApi();

// Diagnostics endpoints
apiGroup.MapGet("/diagnostics", async (Aura.Core.Hardware.DiagnosticsHelper diagnosticsHelper) =>
{
    try
    {
        var report = await diagnosticsHelper.GenerateDiagnosticsReportAsync();
        return Results.Ok(new { success = true, report });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating diagnostics");
        return Results.Problem("Error generating diagnostics", statusCode: 500);
    }
})
.WithName("GetDiagnostics")
.WithOpenApi();

apiGroup.MapGet("/diagnostics/json", async (Aura.Core.Hardware.DiagnosticsHelper diagnosticsHelper) =>
{
    try
    {
        var diagnostics = await diagnosticsHelper.GenerateDiagnosticsJsonAsync();
        return Results.Ok(diagnostics);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating diagnostics JSON");
        return Results.Problem("Error generating diagnostics JSON", statusCode: 500);
    }
})
.WithName("GetDiagnosticsJson")
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

// Legacy plaintext API key endpoints have been completely removed.
// All API key operations must use the secure KeyVault API at /api/keys/* (KeyVaultController)
// which provides encrypted storage using DPAPI (Windows) or AES-256 (Linux/macOS).
// Migration from legacy plaintext files happens automatically on first startup via KeyStore.

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

// Portable Mode Settings
apiGroup.MapGet("/settings/portable", () =>
{
    try
    {
        var providerSettings = app.Services.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
        var portableRoot = providerSettings.GetPortableRootPath();
        var toolsDirectory = providerSettings.GetToolsDirectory();
        var auraDataDirectory = providerSettings.GetAuraDataDirectory();
        
        return Results.Ok(new 
        { 
            portableModeEnabled = true, // Always true in portable-only mode
            portableRootPath = portableRoot,
            toolsDirectory = toolsDirectory,
            auraDataDirectory = auraDataDirectory,
            logsDirectory = providerSettings.GetLogsDirectory(),
            projectsDirectory = providerSettings.GetProjectsDirectory(),
            downloadsDirectory = providerSettings.GetDownloadsDirectory()
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error loading portable settings");
        return Results.Problem("Error loading portable settings", statusCode: 500);
    }
})
.WithName("GetPortableModeSettings")
.WithOpenApi();

// Portable mode is always enabled - this endpoint is read-only for info purposes
// Keeping for backward compatibility but it doesn't change the mode

// Open Tools Folder
apiGroup.MapPost("/settings/open-tools-folder", () =>
{
    try
    {
        var providerSettings = app.Services.GetRequiredService<Aura.Core.Configuration.ProviderSettings>();
        var toolsDirectory = providerSettings.GetToolsDirectory();
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(toolsDirectory))
        {
            Directory.CreateDirectory(toolsDirectory);
        }

        // Platform-specific logic to open folder in file explorer
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start("explorer.exe", toolsDirectory);
        }
        else if (OperatingSystem.IsMacOS())
        {
            System.Diagnostics.Process.Start("open", toolsDirectory);
        }
        else if (OperatingSystem.IsLinux())
        {
            System.Diagnostics.Process.Start("xdg-open", toolsDirectory);
        }

        return Results.Ok(new { success = true, path = toolsDirectory });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error opening tools folder");
        return Results.Problem("Error opening tools folder", statusCode: 500);
    }
})
.WithName("OpenToolsFolder")
.WithOpenApi();

// Assets search endpoint - search stock providers
apiGroup.MapPost("/assets/search", async ([FromBody] AssetSearchRequest request, HardwareDetector detector, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
{
    try
    {
        var profile = await detector.DetectSystemAsync();
        
        // Check if offline only mode
        if (profile.OfflineOnly && request.Provider != "local")
        {
            return Results.Ok(new 
            { 
                success = false, 
                gated = true,
                reason = "Offline mode enabled - only local assets are available",
                assets = Array.Empty<object>()
            });
        }

        var httpClient = httpClientFactory.CreateClient();
        var assets = new List<object>();

        switch (request.Provider.ToLowerInvariant())
        {
            case "pexels":
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.Ok(new 
                    { 
                        success = false, 
                        gated = true,
                        reason = "Pexels API key required",
                        assets = Array.Empty<object>()
                    });
                }
                var pexelsProvider = new Aura.Providers.Images.PexelsStockProvider(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.PexelsStockProvider>(),
                    httpClient,
                    request.ApiKey);
                var pexelsAssets = await pexelsProvider.SearchAsync(request.Query, request.Count, ct);
                assets.AddRange(pexelsAssets.Select(a => new { a.Kind, a.PathOrUrl, a.License, a.Attribution }));
                break;

            case "pixabay":
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.Ok(new 
                    { 
                        success = false, 
                        gated = true,
                        reason = "Pixabay API key required",
                        assets = Array.Empty<object>()
                    });
                }
                var pixabayProvider = new Aura.Providers.Images.PixabayStockProvider(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.PixabayStockProvider>(),
                    httpClient,
                    request.ApiKey);
                var pixabayAssets = await pixabayProvider.SearchAsync(request.Query, request.Count, ct);
                assets.AddRange(pixabayAssets.Select(a => new { a.Kind, a.PathOrUrl, a.License, a.Attribution }));
                break;

            case "unsplash":
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    return Results.Ok(new 
                    { 
                        success = false, 
                        gated = true,
                        reason = "Unsplash API key required",
                        assets = Array.Empty<object>()
                    });
                }
                var unsplashProvider = new Aura.Providers.Images.UnsplashStockProvider(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.UnsplashStockProvider>(),
                    httpClient,
                    request.ApiKey);
                var unsplashAssets = await unsplashProvider.SearchAsync(request.Query, request.Count, ct);
                assets.AddRange(unsplashAssets.Select(a => new { a.Kind, a.PathOrUrl, a.License, a.Attribution }));
                break;

            case "local":
                var localDirectory = request.LocalDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "Assets");
                var localProvider = new Aura.Providers.Images.LocalStockProvider(
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.LocalStockProvider>(),
                    localDirectory);
                var localAssets = await localProvider.SearchAsync(request.Query, request.Count, ct);
                assets.AddRange(localAssets.Select(a => new { a.Kind, a.PathOrUrl, a.License, a.Attribution }));
                break;

            default:
                return Results.BadRequest(new { success = false, message = $"Unknown provider: {request.Provider}" });
        }

        return Results.Ok(new { success = true, gated = false, assets });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error searching assets");
        return Results.Problem("Error searching assets", statusCode: 500);
    }
})
.WithName("SearchAssets")
.WithOpenApi();

// Stock providers status endpoint - list available providers with quota info
apiGroup.MapGet("/assets/stock/providers", (IConfiguration config) =>
{
    try
    {
        var pexelsApiKey = config["ApiKeys:Pexels"];
        var pixabayApiKey = config["ApiKeys:Pixabay"];
        var unsplashApiKey = config["ApiKeys:Unsplash"];

        var providers = new List<StockProviderDto>();

        // Pexels provider
        if (!string.IsNullOrEmpty(pexelsApiKey))
        {
            // Create instance to get quota info
            var httpClient = new HttpClient();
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.PexelsImageProvider>();
            var pexelsProvider = new Aura.Providers.Images.PexelsImageProvider(logger, httpClient, pexelsApiKey);
            var (remaining, limit, _) = pexelsProvider.GetQuotaStatus();
            
            providers.Add(new StockProviderDto(
                Name: "Pexels",
                Available: true,
                HasApiKey: true,
                QuotaRemaining: remaining,
                QuotaLimit: limit,
                Error: null
            ));
        }
        else
        {
            providers.Add(new StockProviderDto(
                Name: "Pexels",
                Available: false,
                HasApiKey: false,
                QuotaRemaining: null,
                QuotaLimit: null,
                Error: "API key not configured. Get a free key at https://www.pexels.com/api/"
            ));
        }

        // Pixabay provider
        if (!string.IsNullOrEmpty(pixabayApiKey))
        {
            providers.Add(new StockProviderDto(
                Name: "Pixabay",
                Available: true,
                HasApiKey: true,
                QuotaRemaining: null, // Pixabay doesn't provide quota info via API
                QuotaLimit: null,
                Error: null
            ));
        }
        else
        {
            providers.Add(new StockProviderDto(
                Name: "Pixabay",
                Available: false,
                HasApiKey: false,
                QuotaRemaining: null,
                QuotaLimit: null,
                Error: "API key not configured. Get a free key at https://pixabay.com/api/docs/"
            ));
        }

        // Unsplash provider
        if (!string.IsNullOrEmpty(unsplashApiKey))
        {
            var httpClient = new HttpClient();
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.UnsplashImageProvider>();
            var unsplashProvider = new Aura.Providers.Images.UnsplashImageProvider(logger, httpClient, unsplashApiKey);
            var (remaining, limit) = unsplashProvider.GetQuotaStatus();
            
            providers.Add(new StockProviderDto(
                Name: "Unsplash",
                Available: true,
                HasApiKey: true,
                QuotaRemaining: remaining,
                QuotaLimit: limit,
                Error: null
            ));
        }
        else
        {
            providers.Add(new StockProviderDto(
                Name: "Unsplash",
                Available: false,
                HasApiKey: false,
                QuotaRemaining: null,
                QuotaLimit: null,
                Error: "API key not configured. Get a free key at https://unsplash.com/developers"
            ));
        }

        return Results.Ok(new StockProvidersResponse(providers));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting stock providers status");
        return Results.Problem("Error getting stock providers status", statusCode: 500);
    }
})
.WithName("GetStockProviders")
.WithOpenApi();

// Stock quota endpoint - check remaining quota for a specific provider
apiGroup.MapGet("/assets/stock/quota/{provider}", (string provider, IConfiguration config) =>
{
    try
    {
        provider = provider.ToLowerInvariant();
        
        switch (provider)
        {
            case "pexels":
                var pexelsApiKey = config["ApiKeys:Pexels"];
                if (string.IsNullOrEmpty(pexelsApiKey))
                {
                    return Results.BadRequest(new { error = "Pexels API key not configured" });
                }
                
                var httpClient = new HttpClient();
                var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.PexelsImageProvider>();
                var pexelsProvider = new Aura.Providers.Images.PexelsImageProvider(logger, httpClient, pexelsApiKey);
                var (remaining, limit, resetTime) = pexelsProvider.GetQuotaStatus();
                
                return Results.Ok(new QuotaStatusResponse(
                    Provider: "Pexels",
                    Remaining: remaining,
                    Limit: limit,
                    ResetTime: resetTime
                ));

            case "unsplash":
                var unsplashApiKey = config["ApiKeys:Unsplash"];
                if (string.IsNullOrEmpty(unsplashApiKey))
                {
                    return Results.BadRequest(new { error = "Unsplash API key not configured" });
                }
                
                var unsplashHttpClient = new HttpClient();
                var unsplashLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.UnsplashImageProvider>();
                var unsplashProvider = new Aura.Providers.Images.UnsplashImageProvider(unsplashLogger, unsplashHttpClient, unsplashApiKey);
                var (unsplashRemaining, unsplashLimit) = unsplashProvider.GetQuotaStatus();
                
                return Results.Ok(new QuotaStatusResponse(
                    Provider: "Unsplash",
                    Remaining: unsplashRemaining,
                    Limit: unsplashLimit,
                    ResetTime: null
                ));

            case "pixabay":
                var pixabayApiKey = config["ApiKeys:Pixabay"];
                if (string.IsNullOrEmpty(pixabayApiKey))
                {
                    return Results.BadRequest(new { error = "Pixabay API key not configured" });
                }
                return Results.Ok(new { message = "Pixabay does not provide quota information via API" });

            default:
                return Results.BadRequest(new { error = $"Unknown provider: {provider}" });
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error getting quota status for provider {Provider}", provider);
        return Results.Problem("Error getting quota status", statusCode: 500);
    }
})
.WithName("GetStockQuota")
.WithOpenApi();

// Assets generate endpoint - generate with Stable Diffusion
apiGroup.MapPost("/assets/generate", async ([FromBody] AssetGenerateRequest request, HardwareDetector detector, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
{
    try
    {
        var profile = await detector.DetectSystemAsync();
        
        // NVIDIA GPU gate - can be bypassed
        if (!request.BypassHardwareChecks && (profile.Gpu == null || profile.Gpu.Vendor.ToLowerInvariant() != "nvidia"))
        {
            return Results.Ok(new 
            { 
                success = false, 
                gated = true,
                reason = "Stable Diffusion typically requires an NVIDIA GPU. Use stock visuals, Pro cloud, or set BypassHardwareChecks=true to override.",
                assets = Array.Empty<object>()
            });
        }

        // VRAM gate - can be bypassed
        if (!request.BypassHardwareChecks && profile.Gpu != null && profile.Gpu.VramGB < 6)
        {
            return Results.Ok(new 
            { 
                success = false, 
                gated = true,
                reason = $"Insufficient VRAM ({profile.Gpu.VramGB}GB). Stable Diffusion typically requires minimum 6GB VRAM. Set BypassHardwareChecks=true to override.",
                assets = Array.Empty<object>()
            });
        }

        // Offline mode gate
        if (profile.OfflineOnly)
        {
            return Results.Ok(new 
            { 
                success = false, 
                gated = true,
                reason = "Offline mode enabled - Stable Diffusion WebUI requires network access",
                assets = Array.Empty<object>()
            });
        }

        var httpClient = httpClientFactory.CreateClient();
        var sdUrl = request.StableDiffusionUrl ?? "http://127.0.0.1:7860";
        
        var sdParams = new Aura.Providers.Images.SDGenerationParams
        {
            Model = request.Model,
            Steps = request.Steps,
            CfgScale = request.CfgScale ?? 7.0,
            Seed = request.Seed ?? -1,
            Width = request.Width,
            Height = request.Height,
            Style = request.Style ?? "high quality, detailed, professional",
            SamplerName = request.SamplerName ?? "DPM++ 2M Karras"
        };

        // Determine if we're using NVIDIA GPU or bypassing checks
        bool isNvidiaGpu = profile.Gpu != null && profile.Gpu.Vendor.ToLowerInvariant() == "nvidia";
        int vramGB = profile.Gpu?.VramGB ?? 0;

        var sdProvider = new Aura.Providers.Images.StableDiffusionWebUiProvider(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Aura.Providers.Images.StableDiffusionWebUiProvider>(),
            httpClient,
            sdUrl,
            isNvidiaGpu,
            vramGB,
            sdParams,
            request.BypassHardwareChecks);

        // Create a dummy scene for generation
        var scene = new Scene(
            Index: request.SceneIndex ?? 0,
            Heading: request.Prompt,
            Script: "",
            Start: TimeSpan.Zero,
            Duration: TimeSpan.FromSeconds(5));

        var spec = new VisualSpec(
            Style: request.Style ?? "",
            Aspect: request.Aspect != null ? ApiV1.EnumMappings.ToCore(request.Aspect.Value) : Aspect.Widescreen16x9,
            Keywords: request.Keywords ?? Array.Empty<string>());

        var assets = await sdProvider.FetchOrGenerateAsync(scene, spec, sdParams, ct);

        return Results.Ok(new 
        { 
            success = true, 
            gated = false,
            model = profile.Gpu.VramGB >= 12 ? "SDXL" : "SD 1.5",
            vramGB = profile.Gpu.VramGB,
            assets = assets.Select(a => new { a.Kind, a.PathOrUrl, a.License, a.Attribution })
        });
    }
    catch (HttpRequestException ex)
    {
        Log.Warning(ex, "Failed to connect to Stable Diffusion WebUI");
        return Results.Ok(new 
        { 
            success = false, 
            gated = true,
            reason = "Failed to connect to Stable Diffusion WebUI. Is it running?",
            assets = Array.Empty<object>()
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error generating assets with Stable Diffusion");
        return Results.Problem("Error generating assets", statusCode: 500);
    }
})
.WithName("GenerateAssets")
.WithOpenApi();

// Test provider connections
apiGroup.MapPost("/providers/test/{provider}", async (string provider, [FromBody] ProviderTestRequest request, IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var httpClient = httpClientFactory.CreateClient();
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        switch (provider.ToLower())
        {
            case "stablediffusion":
                try
                {
                    var sdUrl = request.Url ?? "http://127.0.0.1:7860";
                    var response = await httpClient.GetAsync($"{sdUrl}/sdapi/v1/sd-models", cts.Token);
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
                    var response = await httpClient.GetAsync($"{ollamaUrl}/api/tags", cts.Token);
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

// Validate providers
apiGroup.MapPost("/providers/validate", async (
    [FromBody] ValidateProvidersRequest? request,
    ProviderValidationService validationService,
    CancellationToken ct) =>
{
    try
    {
        var providers = request?.Providers?.Length > 0 ? request.Providers : null;
        
        Log.Information("Validating providers: {Providers}", 
            providers != null ? string.Join(", ", providers) : "all");

        var result = await validationService.ValidateProvidersAsync(providers, ct);

        return Results.Ok(new
        {
            results = result.Results,
            ok = result.Ok
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error validating providers");
        return Results.Problem("Error validating providers", statusCode: 500);
    }
})
.WithName("ValidateProviders")
.WithOpenApi();

// Root health endpoint for startup readiness checks
app.MapGet("/healthz", () => 
{
    var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var staticReady = Directory.Exists(wwwroot) && File.Exists(Path.Combine(wwwroot, "index.html"));
    
    if (staticReady)
    {
        return Results.Ok(new 
        { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            staticHosting = "ready"
        });
    }
    else
    {
        return Results.Json(new 
        { 
            status = "degraded", 
            timestamp = DateTime.UtcNow,
            staticHosting = "unavailable",
            message = "Static UI files not found. API is functional but web UI unavailable."
        }, statusCode: 200);
    }
})
.WithName("RootHealthCheck")
.WithOpenApi();

// SPA fallback - return index.html for any non-API, non-file routes (must be LAST)
// This enables client-side routing (deep links work on refresh)
if (Directory.Exists(wwwrootPath) && File.Exists(Path.Combine(wwwrootPath, "index.html")))
{
    app.MapFallbackToFile("index.html");
    Log.Information("SPA fallback configured: All unmatched routes will serve index.html for client-side routing");
}

// Start Engine Lifecycle Manager and Provider Health Monitoring
// These are started after the application begins to ensure all services are initialized
var lifecycleManager = app.Services.GetRequiredService<Aura.Core.Runtime.EngineLifecycleManager>();
var healthMonitor = app.Services.GetRequiredService<Aura.Core.Services.Health.ProviderHealthMonitor>();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

// Track initialization state
var initializationComplete = false;
var engineManagerStarted = false;
var healthMonitorStarted = false;

lifetime.ApplicationStarted.Register(() =>
{
    Log.Information("Initialization Phase 4: Application started, beginning background service initialization");
    
    // Start Engine Lifecycle Manager first (deterministic ordering)
    _ = Task.Run(async () =>
    {
        try
        {
            Log.Information("Starting Engine Lifecycle Manager...");
            await lifecycleManager.StartAsync();
            engineManagerStarted = true;
            Log.Information("Engine Lifecycle Manager started successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start Engine Lifecycle Manager");
            // Continue even if this fails - application can still function
        }
    });
    
    // Start Provider Health Monitoring after a slight delay to ensure engine manager initializes first
    _ = Task.Run(async () =>
    {
        try
        {
            // Wait briefly for engine manager to start
            await Task.Delay(TimeSpan.FromSeconds(2));
            
            Log.Information("Starting provider health monitoring...");
            await healthMonitor.RunPeriodicHealthChecksAsync(lifetime.ApplicationStopping);
            healthMonitorStarted = true;
            Log.Information("Provider health monitoring stopped");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in provider health monitoring");
            // Restart monitoring after delay
            if (!lifetime.ApplicationStopping.IsCancellationRequested)
            {
                Log.Information("Attempting to restart provider health monitoring after error...");
                await Task.Delay(TimeSpan.FromMinutes(1));
                if (!lifetime.ApplicationStopping.IsCancellationRequested)
                {
                    Log.Information("Restarting provider health monitoring...");
                    await healthMonitor.RunPeriodicHealthChecksAsync(lifetime.ApplicationStopping);
                }
            }
        }
    });
    
    initializationComplete = true;
    Log.Information("Initialization Phase 5: Background services initialization started");
});

lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("=== Application Shutdown Initiated ===");
    Log.Information("Shutdown Phase 1: Stopping background services");
    
    // Stop health monitor first (reverse order of startup)
    if (healthMonitorStarted)
    {
        Log.Information("Stopping provider health monitoring...");
        // Health monitor stops automatically via cancellation token
    }
    
    // Stop engine lifecycle manager last
    if (engineManagerStarted)
    {
        Log.Information("Stopping Engine Lifecycle Manager...");
        try
        {
            lifecycleManager.StopAsync().GetAwaiter().GetResult();
            Log.Information("Engine Lifecycle Manager stopped successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping Engine Lifecycle Manager");
        }
    }
    
    Log.Information("Shutdown Phase 2: Background services stopped");
});

// Helper methods for Azure TTS endpoints
static string[] GetFeatureNames(Aura.Core.Models.Voice.VoiceFeatures features)
{
    var featureList = new List<string>();
    
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Rate))
        featureList.Add("Rate");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Pitch))
        featureList.Add("Pitch");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Volume))
        featureList.Add("Volume");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Emphasis))
        featureList.Add("Emphasis");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Breaks))
        featureList.Add("Breaks");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Prosody))
        featureList.Add("Prosody");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.AudioEffects))
        featureList.Add("AudioEffects");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Styles))
        featureList.Add("Styles");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Roles))
        featureList.Add("Roles");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.Phonemes))
        featureList.Add("Phonemes");
    if (features.HasFlag(Aura.Core.Models.Voice.VoiceFeatures.SayAs))
        featureList.Add("SayAs");
    
    return featureList.ToArray();
}

static Dictionary<string, string> GetStyleDescriptions()
{
    return new Dictionary<string, string>
    {
        { "advertisement_upbeat", "Upbeat and energetic style for advertisements" },
        { "affectionate", "Warm and affectionate tone" },
        { "angry", "Angry and upset tone" },
        { "assistant", "Professional assistant tone" },
        { "calm", "Calm and composed tone" },
        { "chat", "Casual conversational tone" },
        { "cheerful", "Happy and cheerful tone" },
        { "customerservice", "Professional customer service tone" },
        { "depressed", "Sad and depressed tone" },
        { "disgruntled", "Disgruntled and dissatisfied tone" },
        { "documentary-narration", "Documentary narrator tone" },
        { "embarrassed", "Embarrassed and shy tone" },
        { "empathetic", "Empathetic and understanding tone" },
        { "envious", "Envious and jealous tone" },
        { "excited", "Excited and enthusiastic tone" },
        { "fearful", "Fearful and scared tone" },
        { "friendly", "Friendly and approachable tone" },
        { "gentle", "Gentle and soft tone" },
        { "hopeful", "Hopeful and optimistic tone" },
        { "lyrical", "Musical and lyrical tone" },
        { "narration-professional", "Professional narration tone" },
        { "narration-relaxed", "Relaxed narration tone" },
        { "newscast", "News broadcaster tone" },
        { "newscast-casual", "Casual news broadcaster tone" },
        { "newscast-formal", "Formal news broadcaster tone" },
        { "poetry-reading", "Poetry reading tone" },
        { "sad", "Sad and melancholic tone" },
        { "serious", "Serious and stern tone" },
        { "shouting", "Loud and shouting tone" },
        { "sports_commentary", "Sports commentary tone" },
        { "sports_commentary_excited", "Excited sports commentary tone" },
        { "terrified", "Terrified and very scared tone" },
        { "unfriendly", "Unfriendly and cold tone" },
        { "whispering", "Whispering tone" }
    };
}

static Aura.Core.Models.Voice.AzureAudioEffect ParseAudioEffect(string? effect)
{
    return effect?.ToLowerInvariant() switch
    {
        "eq_telecom" => Aura.Core.Models.Voice.AzureAudioEffect.EqTelecom,
        "eq_car" => Aura.Core.Models.Voice.AzureAudioEffect.EqCar,
        "reverb" => Aura.Core.Models.Voice.AzureAudioEffect.Reverb,
        _ => Aura.Core.Models.Voice.AzureAudioEffect.None
    };
}

static Aura.Core.Models.Voice.EmphasisLevel ParseEmphasis(string? emphasis)
{
    return emphasis?.ToLowerInvariant() switch
    {
        "strong" => Aura.Core.Models.Voice.EmphasisLevel.Strong,
        "moderate" => Aura.Core.Models.Voice.EmphasisLevel.Moderate,
        "reduced" => Aura.Core.Models.Voice.EmphasisLevel.Reduced,
        _ => Aura.Core.Models.Voice.EmphasisLevel.None
    };
}

// Run dependency scan on startup
// This scans for dependencies on first launch and every program startup
Task.Run(async () =>
{
    try
    {
        Log.Information("Running automatic dependency scan on startup");
        
        using var scope = app.Services.CreateScope();
        var rescanService = scope.ServiceProvider.GetRequiredService<Aura.Core.Dependencies.DependencyRescanService>();
        
        var report = await rescanService.RescanAllAsync();
        
        var installedCount = report.Dependencies.Count(d => d.Status == Aura.Core.Dependencies.DependencyStatus.Installed);
        var missingCount = report.Dependencies.Count(d => d.Status == Aura.Core.Dependencies.DependencyStatus.Missing);
        var partialCount = report.Dependencies.Count(d => d.Status == Aura.Core.Dependencies.DependencyStatus.PartiallyInstalled);
        
        Log.Information("Startup dependency scan completed: {Installed} installed, {Missing} missing, {Partial} partially installed",
            installedCount, missingCount, partialCount);
            
        if (missingCount > 0 || partialCount > 0)
        {
            Log.Warning("Some dependencies are missing or incomplete. Please visit Program Dependencies page to install them.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to run startup dependency scan");
    }
});

app.Run();

// Make Program accessible for integration tests
public partial class Program { }

// Make Program accessible for integration tests
public partial class Program { }
