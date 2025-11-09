using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.AI;
using Aura.Core.AI.Adapters;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Models.Diagnostics;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Core.Services.HealthChecks;
using Aura.Core.Services.Orchestration;
using Aura.Core.Services.Validation;
using Aura.Core.Services.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Diagnostics and system health endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly ILogger<DiagnosticsController> _logger;
    private readonly SystemHealthService? _healthService;
    private readonly SystemIntegrityValidator? _integrityValidator;
    private readonly PromptTemplateValidator? _promptValidator;
    private readonly LlmProviderFactory? _llmProviderFactory;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IntelligentContentAdvisor? _contentAdvisor;
    private readonly IHardwareDetector? _hardwareDetector;
    private readonly PipelineHealthCheck? _pipelineHealthCheck;
    private readonly IResourceTracker? _resourceTracker;
    private readonly ErrorAggregationService? _errorAggregation;
    private readonly PerformanceTrackingService? _performanceTracking;
    private readonly DiagnosticReportGenerator? _reportGenerator;
    private readonly ModelCatalog? _modelCatalog;
    private readonly DiagnosticBundleService? _bundleService;
    private readonly FailureAnalysisService? _failureAnalysisService;

    public DiagnosticsController(
        ILogger<DiagnosticsController> logger,
        SystemHealthService? healthService = null,
        SystemIntegrityValidator? integrityValidator = null,
        PromptTemplateValidator? promptValidator = null,
        LlmProviderFactory? llmProviderFactory = null,
        ILoggerFactory? loggerFactory = null,
        IntelligentContentAdvisor? contentAdvisor = null,
        IHardwareDetector? hardwareDetector = null,
        PipelineHealthCheck? pipelineHealthCheck = null,
        IResourceTracker? resourceTracker = null,
        ErrorAggregationService? errorAggregation = null,
        PerformanceTrackingService? performanceTracking = null,
        DiagnosticReportGenerator? reportGenerator = null,
        ModelCatalog? modelCatalog = null,
        DiagnosticBundleService? bundleService = null,
        FailureAnalysisService? failureAnalysisService = null)
    {
        _logger = logger;
        _healthService = healthService;
        _integrityValidator = integrityValidator;
        _promptValidator = promptValidator;
        _llmProviderFactory = llmProviderFactory;
        _loggerFactory = loggerFactory;
        _contentAdvisor = contentAdvisor;
        _hardwareDetector = hardwareDetector;
        _pipelineHealthCheck = pipelineHealthCheck;
        _resourceTracker = resourceTracker;
        _errorAggregation = errorAggregation;
        _performanceTracking = performanceTracking;
        _reportGenerator = reportGenerator;
        _modelCatalog = modelCatalog;
        _bundleService = bundleService;
        _failureAnalysisService = failureAnalysisService;
    }

    /// <summary>
    /// Get overall system health status
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthReport>> GetHealth(CancellationToken ct = default)
    {
        _logger.LogInformation("Health check requested");

        if (_healthService == null)
        {
            return Ok(new
            {
                Status = "Degraded",
                Message = "Health service not configured",
                Timestamp = DateTime.UtcNow
            });
        }

        try
        {
            var report = await _healthService.CheckHealthAsync(ct);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            return StatusCode(500, new
            {
                Status = "Unhealthy",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Test specific LLM provider
    /// </summary>
    [HttpPost("test-provider")]
    public async Task<ActionResult<object>> TestProvider(
        [FromBody] ProviderTestRequest request,
        CancellationToken ct = default)
    {
        // Sanitize input for logging to prevent log forging
        var sanitizedProviderName = request.ProviderName?.Replace("\n", "").Replace("\r", "") ?? "null";
        _logger.LogInformation("Provider test requested: {Provider}", sanitizedProviderName);

        var providers = GetAvailableLlmProviders();
        if (providers.Count == 0)
        {
            return BadRequest(new { Error = "No LLM providers configured" });
        }

        if (!TryResolveLlmProvider(request.ProviderName, providers, out var resolvedKey, out var provider))
        {
            return NotFound(new { Error = $"Provider '{request.ProviderName}' not found" });
        }

        try
        {
            var testBrief = new Brief(
                Topic: "Diagnostic Test",
                Audience: null,
                Goal: null,
                Tone: "informative",
                Language: "en",
                Aspect: Aspect.Widescreen16x9
            );

            var testSpec = new PlanSpec(
                TargetDuration: TimeSpan.FromSeconds(30),
                Pacing: Pacing.Conversational,
                Density: Density.Balanced,
                Style: "diagnostic test"
            );

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var startTime = DateTime.UtcNow;
            var response = await provider.DraftScriptAsync(testBrief, testSpec, cts.Token);
            var duration = DateTime.UtcNow - startTime;

            return Ok(new
            {
                Provider = resolvedKey,
                Implementation = provider.GetType().Name,
                Status = "Success",
                ResponseLength = response?.Length ?? 0,
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, new
            {
                Provider = resolvedKey,
                Status = "Timeout",
                Error = "Provider test timed out after 30 seconds"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider test failed: {Provider}", sanitizedProviderName);
            return StatusCode(500, new
            {
                Provider = resolvedKey,
                Status = "Failed",
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Run quality analysis test
    /// </summary>
    [HttpPost("test-quality-analysis")]
    public async Task<ActionResult<ContentQualityAnalysis>> TestQualityAnalysis(
        [FromBody] QualityTestRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Quality analysis test requested");

        if (_contentAdvisor == null)
        {
            return BadRequest(new { Error = "Content advisor not configured" });
        }

        try
        {
            var testBrief = new Brief(
                Topic: request.Topic ?? "Test Topic",
                Audience: request.Audience,
                Goal: request.Goal,
                Tone: request.Tone ?? "informative",
                Language: "en",
                Aspect: Aspect.Widescreen16x9
            );

            var testSpec = new PlanSpec(
                TargetDuration: TimeSpan.FromMinutes(2),
                Pacing: Pacing.Conversational,
                Density: Density.Balanced,
                Style: "test"
            );

            var analysis = await _contentAdvisor.AnalyzeContentQualityAsync(
                request.Script,
                testBrief,
                testSpec,
                ct
            );

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quality analysis test failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Verify system configuration
    /// </summary>
    [HttpGet("configuration")]
    public async Task<ActionResult<object>> GetConfiguration(CancellationToken ct = default)
    {
        _logger.LogInformation("Configuration check requested");

        var llmProviders = GetAvailableLlmProviders();

        var config = new
        {
            HealthServiceConfigured = _healthService != null,
            IntegrityValidatorConfigured = _integrityValidator != null,
            PromptValidatorConfigured = _promptValidator != null,
            ContentAdvisorConfigured = _contentAdvisor != null,
            LlmProvidersCount = llmProviders.Count,
            LlmProviders = llmProviders.Keys.ToList(),
            Timestamp = DateTime.UtcNow
        };

        await Task.CompletedTask;
        return Ok(config);
    }

    /// <summary>
    /// Run integration tests
    /// </summary>
    [HttpPost("run-integration-tests")]
    public async Task<ActionResult<object>> RunIntegrationTests(CancellationToken ct = default)
    {
        _logger.LogInformation("Integration tests requested");

        var results = new List<object>();

        // Test 1: System Integrity
        if (_integrityValidator != null)
        {
            try
            {
                var integrityResult = await _integrityValidator.ValidateAsync(ct);
                results.Add(new
                {
                    Test = "System Integrity",
                    Status = integrityResult.IsValid ? "Passed" : "Failed",
                    Details = integrityResult
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    Test = "System Integrity",
                    Status = "Error",
                    Error = ex.Message
                });
            }
        }

        // Test 2: Prompt Templates
        if (_promptValidator != null)
        {
            try
            {
                var promptResult = await _promptValidator.ValidateAsync(ct);
                results.Add(new
                {
                    Test = "Prompt Templates",
                    Status = promptResult.IsValid ? "Passed" : "Failed",
                    Details = promptResult
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    Test = "Prompt Templates",
                    Status = "Error",
                    Error = ex.Message
                });
            }
        }

        // Test 3: Health Checks
        if (_healthService != null)
        {
            try
            {
                var healthResult = await _healthService.CheckHealthAsync(ct);
                results.Add(new
                {
                    Test = "Health Checks",
                    Status = healthResult.Status == HealthStatus.Healthy ? "Passed" : "Degraded",
                    Details = healthResult
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    Test = "Health Checks",
                    Status = "Error",
                    Error = ex.Message
                });
            }
        }

        return Ok(new
        {
            TotalTests = results.Count,
            PassedTests = results.Count(r => ((dynamic)r).Status == "Passed"),
            Results = results,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get hardware information for export acceleration detection
    /// </summary>
    [HttpGet("hardware")]
    public async Task<ActionResult<Aura.Core.Models.Diagnostics.SystemProfile>> GetHardware()
    {
        _logger.LogInformation("Hardware detection requested");

        if (_hardwareDetector == null)
        {
            return Ok(new
            {
                LogicalCores = 4,
                PhysicalCores = 2,
                RamGB = 8,
                Gpu = (object?)null,
                Tier = "Budget",
                EnableNVENC = false,
                EnableSD = false,
                Message = "Hardware detector not configured"
            });
        }

        try
        {
            var profile = await _hardwareDetector.DetectSystemAsync();
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hardware detection");
            return StatusCode(500, new
            {
                Error = ex.Message,
                Message = "Hardware detection failed"
            });
        }
    }

    /// <summary>
    /// Get pipeline orchestration status and health
    /// </summary>
    [HttpGet("pipeline-status")]
    public async Task<ActionResult<object>> GetPipelineStatus(CancellationToken ct = default)
    {
        _logger.LogInformation("Pipeline status requested");

        if (_pipelineHealthCheck == null)
        {
            return Ok(new
            {
                Status = "NotConfigured",
                Message = "Pipeline health check service not configured",
                Timestamp = DateTime.UtcNow
            });
        }

        try
        {
            var healthResult = await _pipelineHealthCheck.CheckHealthAsync(ct);

            return Ok(new
            {
                Status = healthResult.IsHealthy ? "Healthy" : "Degraded",
                IsHealthy = healthResult.IsHealthy,
                ServiceAvailability = healthResult.ServiceAvailability,
                AvailableServices = healthResult.ServiceAvailability.Count(kvp => kvp.Value),
                TotalServices = healthResult.ServiceAvailability.Count,
                MissingRequiredServices = healthResult.MissingRequiredServices,
                Warnings = healthResult.Warnings,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pipeline status");
            return StatusCode(500, new
            {
                Status = "Error",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get resource usage metrics (memory, file handles, processes)
    /// </summary>
    [HttpGet("resources")]
    public async Task<ActionResult<ResourceMetrics>> GetResources(CancellationToken ct = default)
    {
        _logger.LogInformation("Resource metrics requested");

        if (_resourceTracker == null)
        {
            return Ok(new
            {
                OpenFileHandles = -1,
                ActiveProcesses = -1,
                AllocatedMemoryBytes = -1,
                WorkingSetBytes = -1,
                ThreadCount = -1,
                Timestamp = DateTime.UtcNow,
                Warnings = new[] { "Resource tracker not configured" }
            });
        }

        try
        {
            var metrics = await _resourceTracker.GetMetricsAsync(ct);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource metrics");
            return StatusCode(500, new
            {
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Force resource cleanup (garbage collection, etc.)
    /// </summary>
    [HttpPost("resources/cleanup")]
    public async Task<ActionResult<object>> CleanupResources(CancellationToken ct = default)
    {
        _logger.LogInformation("Resource cleanup requested");

        if (_resourceTracker == null)
        {
            return BadRequest(new { Error = "Resource tracker not configured" });
        }

        try
        {
            await _resourceTracker.CleanupAsync(ct);
            var metricsAfter = await _resourceTracker.GetMetricsAsync(ct);
            
            return Ok(new
            {
                Status = "Cleanup completed",
                MetricsAfter = metricsAfter,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resource cleanup");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get aggregated error information
    /// </summary>
    [HttpGet("errors")]
    public ActionResult<object> GetErrors([FromQuery] string? since = null)
    {
        _logger.LogInformation("Error aggregation requested with since: {Since}", since ?? "all time");

        if (_errorAggregation == null)
        {
            return Ok(new
            {
                Message = "Error aggregation service not configured",
                Errors = new List<object>()
            });
        }

        try
        {
            TimeSpan? timeSpan = null;
            if (!string.IsNullOrEmpty(since))
            {
                // Parse time span (e.g., "24h", "7d", "1h")
                timeSpan = ParseTimeSpan(since);
            }

            var errors = _errorAggregation.GetAggregatedErrors(timeSpan, limit: 10);
            var statistics = _errorAggregation.GetStatistics(timeSpan);

            return Ok(new
            {
                Statistics = statistics,
                TopErrors = errors,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting aggregated errors");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get performance metrics
    /// </summary>
    [HttpGet("performance")]
    public ActionResult<object> GetPerformanceMetrics()
    {
        _logger.LogInformation("Performance metrics requested");

        if (_performanceTracking == null)
        {
            return Ok(new
            {
                Message = "Performance tracking service not configured",
                Metrics = new List<object>()
            });
        }

        try
        {
            var metrics = _performanceTracking.GetMetrics();
            var slowOps = _performanceTracking.GetSlowOperations(limit: 20);

            return Ok(new
            {
                Metrics = metrics,
                SlowOperations = slowOps,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Generate comprehensive diagnostic report as ZIP file
    /// </summary>
    [HttpPost("report")]
    public async Task<ActionResult<object>> GenerateDiagnosticReport(CancellationToken ct = default)
    {
        _logger.LogInformation("Diagnostic report generation requested");

        if (_reportGenerator == null)
        {
            return BadRequest(new { Error = "Diagnostic report generator not configured" });
        }

        try
        {
            var result = await _reportGenerator.GenerateReportAsync(ct);

            return Ok(new
            {
                ReportId = result.ReportId,
                FileName = result.FileName,
                GeneratedAt = result.GeneratedAt,
                ExpiresAt = result.ExpiresAt,
                SizeBytes = result.SizeBytes,
                DownloadUrl = $"/api/diagnostics/report/{result.ReportId}/download"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating diagnostic report");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Download diagnostic report by ID
    /// </summary>
    [HttpGet("report/{reportId}/download")]
    public ActionResult DownloadDiagnosticReport(string reportId)
    {
        _logger.LogInformation("Diagnostic report download requested: {ReportId}", reportId);

        if (_reportGenerator == null)
        {
            return BadRequest(new { Error = "Diagnostic report generator not configured" });
        }

        try
        {
            var reportPath = _reportGenerator.GetReportPath(reportId);

            if (reportPath == null || !System.IO.File.Exists(reportPath))
            {
                return NotFound(new { Error = "Report not found or has expired" });
            }

            var fileBytes = System.IO.File.ReadAllBytes(reportPath);
            var fileName = Path.GetFileName(reportPath);

            return File(fileBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading diagnostic report");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get model catalog diagnostics including available models and refresh status
    /// </summary>
    [HttpGet("models")]
    public ActionResult<object> GetModelDiagnostics()
    {
        _logger.LogInformation("Model diagnostics requested");

        try
        {
            if (_modelCatalog == null)
            {
                return Ok(new
                {
                    Status = "NotConfigured",
                    Message = "Model catalog service not configured",
                    UsingStaticRegistry = true,
                    Timestamp = DateTime.UtcNow
                });
            }

            var providers = new[] { "OpenAI", "Anthropic", "Gemini", "Azure", "Ollama" };
            var modelsByProvider = providers.ToDictionary(
                p => p,
                p => _modelCatalog.GetAllModels(p).Select(m => new
                {
                    modelId = m.ModelId,
                    maxTokens = m.MaxTokens,
                    contextWindow = m.ContextWindow,
                    aliases = m.Aliases ?? Array.Empty<string>(),
                    isDeprecated = m.DeprecationDate.HasValue && m.DeprecationDate.Value <= DateTime.UtcNow,
                    deprecationDate = m.DeprecationDate,
                    replacementModel = m.ReplacementModel
                }).ToList()
            );

            var totalModels = modelsByProvider.Values.Sum(list => list.Count);

            return Ok(new
            {
                Status = "Configured",
                TotalModels = totalModels,
                ModelsByProvider = modelsByProvider,
                NeedsRefresh = _modelCatalog.NeedsRefresh(),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model diagnostics");
            return StatusCode(500, new
            {
                Status = "Error",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Generate comprehensive diagnostic bundle for a specific job
    /// </summary>
    [HttpPost("bundle/{jobId}")]
    public async Task<ActionResult<object>> GenerateDiagnosticBundle(
        string jobId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Diagnostic bundle generation requested for job {JobId}", jobId);

        if (_bundleService == null)
        {
            return BadRequest(new { Error = "Diagnostic bundle service not configured" });
        }

        try
        {
            // Note: In production, retrieve the actual Job from job store/repository
            // For now, using minimal stub - the bundle service will collect logs and system info
            var job = new Job { Id = jobId, Status = JobStatus.Failed, Stage = "Unknown" };
            
            var bundle = await _bundleService.GenerateBundleAsync(job, null, null, null, ct);

            return Ok(new
            {
                BundleId = bundle.BundleId,
                JobId = bundle.JobId,
                FileName = bundle.FileName,
                CreatedAt = bundle.CreatedAt,
                ExpiresAt = bundle.ExpiresAt,
                SizeBytes = bundle.SizeBytes,
                DownloadUrl = $"/api/diagnostics/bundle/{bundle.BundleId}/download"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating diagnostic bundle for job {JobId}", jobId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Download diagnostic bundle by ID
    /// </summary>
    [HttpGet("bundle/{bundleId}/download")]
    public ActionResult DownloadDiagnosticBundle(string bundleId)
    {
        _logger.LogInformation("Diagnostic bundle download requested: {BundleId}", bundleId);

        if (_bundleService == null)
        {
            return BadRequest(new { Error = "Diagnostic bundle service not configured" });
        }

        try
        {
            var bundlePath = _bundleService.GetBundlePath(bundleId);

            if (bundlePath == null || !System.IO.File.Exists(bundlePath))
            {
                return NotFound(new { Error = "Bundle not found or has expired" });
            }

            var fileBytes = System.IO.File.ReadAllBytes(bundlePath);
            var fileName = Path.GetFileName(bundlePath);

            return File(fileBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading diagnostic bundle");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Analyze job failure and provide AI-powered recommendations
    /// </summary>
    [HttpPost("explain-failure")]
    public async Task<ActionResult<object>> ExplainFailure(
        [FromBody] ExplainFailureRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Failure analysis requested for job {JobId}", request.JobId);

        if (_failureAnalysisService == null)
        {
            return BadRequest(new { Error = "Failure analysis service not configured" });
        }

        try
        {
            // Note: In production, retrieve the actual Job from job store/repository
            // The request provides minimal info; full job context would improve analysis accuracy
            var job = new Job 
            { 
                Id = request.JobId, 
                Status = JobStatus.Failed, 
                Stage = request.Stage ?? "Unknown",
                ErrorMessage = request.ErrorMessage ?? string.Empty,
                FailureDetails = !string.IsNullOrEmpty(request.ErrorCode) 
                    ? new JobFailure 
                    { 
                        ErrorCode = request.ErrorCode,
                        Message = request.ErrorMessage ?? string.Empty,
                        Stage = request.Stage ?? "Unknown"
                    } 
                    : null
            };
            
            var analysis = await _failureAnalysisService.AnalyzeFailureAsync(job, null, ct);

            return Ok(new
            {
                JobId = analysis.JobId,
                AnalyzedAt = analysis.AnalyzedAt,
                Summary = analysis.Summary,
                PrimaryRootCause = new
                {
                    Type = analysis.PrimaryRootCause.Type.ToString(),
                    Description = analysis.PrimaryRootCause.Description,
                    Confidence = analysis.PrimaryRootCause.Confidence,
                    Evidence = analysis.PrimaryRootCause.Evidence,
                    Stage = analysis.PrimaryRootCause.Stage,
                    Provider = analysis.PrimaryRootCause.Provider
                },
                SecondaryRootCauses = analysis.SecondaryRootCauses.Select(rc => new
                {
                    Type = rc.Type.ToString(),
                    Description = rc.Description,
                    Confidence = rc.Confidence,
                    Evidence = rc.Evidence,
                    Stage = rc.Stage,
                    Provider = rc.Provider
                }).ToList(),
                RecommendedActions = analysis.RecommendedActions.Select(ra => new
                {
                    Priority = ra.Priority,
                    Title = ra.Title,
                    Description = ra.Description,
                    Steps = ra.Steps,
                    CanAutomate = ra.CanAutomate,
                    EstimatedMinutes = ra.EstimatedMinutes,
                    Type = ra.Type.ToString()
                }).ToList(),
                DocumentationLinks = analysis.DocumentationLinks,
                ConfidenceScore = analysis.ConfidenceScore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing failure for job {JobId}", request.JobId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    private Dictionary<string, ILlmProvider> GetAvailableLlmProviders()
    {
        if (_llmProviderFactory == null || _loggerFactory == null)
        {
            return new Dictionary<string, ILlmProvider>();
        }

        try
        {
            return _llmProviderFactory.CreateAvailableProviders(_loggerFactory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate LLM providers via factory");
            return new Dictionary<string, ILlmProvider>();
        }
    }

    private static bool TryResolveLlmProvider(
        string? requestedName,
        Dictionary<string, ILlmProvider> providers,
        out string providerKey,
        out ILlmProvider provider)
    {
        providerKey = string.Empty;
        provider = null!;

        if (providers.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            providerKey = providers.Keys.First();
            provider = providers[providerKey];
            return true;
        }

        foreach (var kvp in providers)
        {
            if (string.Equals(kvp.Key, requestedName, StringComparison.OrdinalIgnoreCase))
            {
                providerKey = kvp.Key;
                provider = kvp.Value;
                return true;
            }
        }

        foreach (var kvp in providers)
        {
            if (kvp.Value.GetType().Name.Contains(requestedName, StringComparison.OrdinalIgnoreCase))
            {
                providerKey = kvp.Key;
                provider = kvp.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parse time span from string (e.g., "24h", "7d", "1h")
    /// </summary>
    private TimeSpan ParseTimeSpan(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, @"^(\d+)([hdm])$");
        if (!match.Success)
        {
            return TimeSpan.FromHours(24); // Default to 24 hours
        }

        var number = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value;

        return unit switch
        {
            "h" => TimeSpan.FromHours(number),
            "d" => TimeSpan.FromDays(number),
            "m" => TimeSpan.FromMinutes(number),
            _ => TimeSpan.FromHours(24)
        };
    }

    /// <summary>
    /// Check provider availability (Ollama, Stable Diffusion, etc.)
    /// </summary>
    [HttpGet("providers/availability")]
    public async Task<IActionResult> CheckProviderAvailability(CancellationToken ct = default)
    {
        _logger.LogInformation("Checking provider availability");

        try
        {
            var service = new Aura.Core.Services.Setup.ProviderAvailabilityService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Aura.Core.Services.Setup.ProviderAvailabilityService>.Instance,
                new System.Net.Http.HttpClient());

            var report = await service.CheckAllProvidersAsync(ct);

            return Ok(new
            {
                success = true,
                timestamp = report.Timestamp,
                providers = report.Providers,
                ollamaAvailable = report.OllamaAvailable,
                stableDiffusionAvailable = report.StableDiffusionAvailable,
                databaseAvailable = report.DatabaseAvailable,
                networkConnected = report.NetworkConnected
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check provider availability");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get auto-configuration recommendations based on system capabilities
    /// </summary>
    [HttpGet("auto-config")]
    public async Task<IActionResult> GetAutoConfiguration(CancellationToken ct = default)
    {
        _logger.LogInformation("Getting auto-configuration recommendations");

        try
        {
            var dependencyDetector = new Aura.Core.Services.Setup.DependencyDetector(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Aura.Core.Services.Setup.DependencyDetector>.Instance,
                null,
                new System.Net.Http.HttpClient());

            var service = new Aura.Core.Services.Setup.AutoConfigurationService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Aura.Core.Services.Setup.AutoConfigurationService>.Instance,
                _hardwareDetector,
                dependencyDetector);

            var config = await service.DetectOptimalSettingsAsync(ct);

            return Ok(new
            {
                success = true,
                recommendedThreadCount = config.RecommendedThreadCount,
                recommendedMemoryLimitMB = config.RecommendedMemoryLimitMB,
                recommendedQualityPreset = config.RecommendedQualityPreset,
                useHardwareAcceleration = config.UseHardwareAcceleration,
                hardwareAccelerationMethod = config.HardwareAccelerationMethod,
                enableLocalProviders = config.EnableLocalProviders,
                recommendedTier = config.RecommendedTier,
                configuredProviders = config.ConfiguredProviders
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get auto-configuration");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

/// <summary>
/// Request model for failure analysis
/// </summary>
public class ExplainFailureRequest
{
    public string JobId { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Request model for provider testing
/// </summary>
public class ProviderTestRequest
{
    public string ProviderName { get; set; } = string.Empty;
}

/// <summary>
/// Request model for quality analysis testing
/// </summary>
public class QualityTestRequest
{
    public string Script { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Audience { get; set; }
    public string? Goal { get; set; }
    public string? Tone { get; set; }
}
