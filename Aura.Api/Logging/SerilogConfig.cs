using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Aura.Api.Logging;

/// <summary>
/// Centralized Serilog configuration for structured logging with correlation IDs.
/// Provides rolling file logs with JSON formatting for easy parsing and filtering.
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// Configure Serilog with structured logging, JSON formatting, and rolling files.
    /// Log files are stored in logs/ directory with daily rolling and 30 days retention.
    /// </summary>
    public static LoggerConfiguration ConfigureLogger(LoggerConfiguration config, IConfiguration configuration)
    {
        return config
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Aura.Api")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/aura-api-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                new JsonFormatter(),
                path: "logs/aura-api-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning);
    }
}
