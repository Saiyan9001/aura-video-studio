using Aura.Core.Configuration;
using System.IO;

namespace Aura.Api.Validation;

/// <summary>
/// Validates application configuration on startup to prevent runtime failures
/// Checks: required configuration values, API key formats, file paths, port numbers
/// </summary>
public class ConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator> _logger;
    private readonly IConfiguration _configuration;
    private readonly ProviderSettings _providerSettings;

    public ConfigurationValidator(
        ILogger<ConfigurationValidator> logger,
        IConfiguration configuration,
        ProviderSettings providerSettings)
    {
        _logger = logger;
        _configuration = configuration;
        _providerSettings = providerSettings;
    }

    /// <summary>
    /// Validates all configuration and returns true if valid
    /// Logs warnings for optional settings, errors for required settings
    /// </summary>
    public bool Validate()
    {
        _logger.LogInformation("Starting configuration validation...");
        
        var isValid = true;
        var validationErrors = new List<string>();
        var validationWarnings = new List<string>();

        // 1. Validate file paths exist and are writable
        ValidateDirectories(validationErrors, validationWarnings);

        // 2. Validate port configuration
        ValidatePortConfiguration(validationErrors, validationWarnings);

        // 3. Validate API keys format (optional but warn if malformed)
        ValidateApiKeys(validationWarnings);

        // 4. Validate database configuration
        ValidateDatabaseConfiguration(validationErrors, validationWarnings);

        // 5. Validate logging configuration
        ValidateLoggingConfiguration(validationWarnings);

        // Log results
        if (validationWarnings.Count > 0)
        {
            _logger.LogWarning("Configuration validation completed with {Count} warnings:", validationWarnings.Count);
            foreach (var warning in validationWarnings)
            {
                _logger.LogWarning("  - {Warning}", warning);
            }
        }

        if (validationErrors.Count > 0)
        {
            isValid = false;
            _logger.LogError("Configuration validation failed with {Count} errors:", validationErrors.Count);
            foreach (var error in validationErrors)
            {
                _logger.LogError("  - {Error}", error);
            }
        }
        else
        {
            _logger.LogInformation("Configuration validation completed successfully");
        }

        return isValid;
    }

    private void ValidateDirectories(List<string> errors, List<string> warnings)
    {
        try
        {
            // Get critical directories
            var outputDir = _providerSettings.GetOutputDirectory();
            var toolsDir = _providerSettings.GetToolsDirectory();
            var dataDir = _providerSettings.GetAuraDataDirectory();
            var logsDir = _providerSettings.GetLogsDirectory();

            // Validate each directory
            ValidateDirectory(outputDir, "Output", errors, warnings, createIfMissing: true);
            ValidateDirectory(toolsDir, "Tools", errors, warnings, createIfMissing: true);
            ValidateDirectory(dataDir, "Data", errors, warnings, createIfMissing: true);
            ValidateDirectory(logsDir, "Logs", errors, warnings, createIfMissing: true);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to validate directories: {ex.Message}");
        }
    }

    private void ValidateDirectory(string path, string name, List<string> errors, List<string> warnings, bool createIfMissing)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                if (createIfMissing)
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation("Created missing {Name} directory: {Path}", name, path);
                }
                else
                {
                    warnings.Add($"{name} directory does not exist: {path}");
                }
            }

            // Test write permissions
            var testFile = Path.Combine(path, $".write_test_{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                errors.Add($"{name} directory is not writable: {path}");
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not verify write permissions for {name} directory: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating {name} directory: {ex.Message}");
        }
    }

    private void ValidatePortConfiguration(List<string> errors, List<string> warnings)
    {
        // Get configured URLs
        var urls = _configuration["AURA_API_URL"] 
                   ?? _configuration["ASPNETCORE_URLS"] 
                   ?? "http://127.0.0.1:5005";

        try
        {
            var uriStrings = urls.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var uriString in uriStrings)
            {
                if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                {
                    // Validate port is in valid range
                    if (uri.Port < 1 || uri.Port > 65535)
                    {
                        errors.Add($"Invalid port number in URL: {uriString}");
                    }

                    // Warn about privileged ports
                    if (uri.Port < 1024 && uri.Port != 80 && uri.Port != 443)
                    {
                        warnings.Add($"Using privileged port {uri.Port} may require elevated permissions");
                    }
                }
                else
                {
                    errors.Add($"Invalid URL format: {uriString}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse URL configuration: {ex.Message}");
        }
    }

    private void ValidateApiKeys(List<string> warnings)
    {
        // Check for API keys in configuration (these are optional but validate format if present)
        var apiKeysToCheck = new Dictionary<string, string>
        {
            ["OpenAI"] = _configuration["ApiKeys:OpenAI"] ?? "",
            ["AzureSpeech"] = _providerSettings.GetAzureSpeechKey(),
            ["Pexels"] = _configuration["StockImages:PexelsApiKey"] ?? "",
            ["Pixabay"] = _configuration["StockImages:PixabayApiKey"] ?? ""
        };

        foreach (var (name, key) in apiKeysToCheck)
        {
            if (!string.IsNullOrEmpty(key))
            {
                // Basic validation - API keys should be at least 20 characters
                if (key.Length < 20)
                {
                    warnings.Add($"{name} API key appears to be too short (length: {key.Length})");
                }

                // Check for placeholder values
                if (key.Contains("your-key-here", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
                    key == "sk-xxx")
                {
                    warnings.Add($"{name} API key appears to be a placeholder value");
                }
            }
        }
    }

    private void ValidateDatabaseConfiguration(List<string> errors, List<string> warnings)
    {
        try
        {
            // Database path is constructed in Program.cs as {BaseDirectory}/aura.db
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aura.db");
            var dbDirectory = Path.GetDirectoryName(dbPath);

            if (string.IsNullOrEmpty(dbDirectory))
            {
                errors.Add("Could not determine database directory");
                return;
            }

            // Ensure directory exists and is writable
            if (!Directory.Exists(dbDirectory))
            {
                try
                {
                    Directory.CreateDirectory(dbDirectory);
                }
                catch (Exception ex)
                {
                    errors.Add($"Cannot create database directory: {ex.Message}");
                    return;
                }
            }

            // Test write permissions (database will be created if it doesn't exist)
            if (File.Exists(dbPath))
            {
                // Test by opening for read/write
                try
                {
                    using var fs = File.Open(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add($"Database file is not writable: {dbPath}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not verify database write permissions: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating database configuration: {ex.Message}");
        }
    }

    private void ValidateLoggingConfiguration(List<string> warnings)
    {
        try
        {
            var logsDir = _providerSettings.GetLogsDirectory();
            
            // Ensure logs directory exists
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            // Warn if logs directory is getting large
            if (Directory.Exists(logsDir))
            {
                var logFiles = Directory.GetFiles(logsDir, "*.log");
                var totalSizeMb = logFiles.Sum(f => new FileInfo(f).Length) / 1024.0 / 1024.0;

                if (totalSizeMb > 500) // > 500 MB
                {
                    warnings.Add($"Log directory is large ({totalSizeMb:F1} MB). Consider cleaning up old log files.");
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not validate logging configuration: {ex.Message}");
        }
    }
}
