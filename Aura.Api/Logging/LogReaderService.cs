using System.Text.Json;

namespace Aura.Api.Logging;

/// <summary>
/// Service to read and parse log files for the log viewer.
/// Reads JSON-formatted log files and provides filtering capabilities.
/// </summary>
public class LogReaderService
{
    private readonly string _logsDirectory;

    public LogReaderService(string? logsDirectory = null)
    {
        _logsDirectory = logsDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "logs");
    }

    /// <summary>
    /// Get recent log entries with optional filtering
    /// </summary>
    public async Task<List<LogEntry>> GetLogsAsync(
        int maxEntries = 500,
        string? level = null,
        string? search = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var logs = new List<LogEntry>();

        if (!Directory.Exists(_logsDirectory))
        {
            return logs;
        }

        // Get all JSON log files, most recent first
        var logFiles = Directory.GetFiles(_logsDirectory, "aura-api-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(7) // Last 7 days
            .ToList();

        foreach (var file in logFiles)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(file);
                foreach (var line in lines.AsEnumerable().Reverse()) // Most recent first
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                        if (logEntry == null)
                            continue;

                        // Apply filters
                        if (!string.IsNullOrEmpty(level) && 
                            !logEntry.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (startDate.HasValue && logEntry.Timestamp < startDate.Value)
                            continue;

                        if (endDate.HasValue && logEntry.Timestamp > endDate.Value)
                            continue;

                        if (!string.IsNullOrEmpty(search) && 
                            !logEntry.Message.Contains(search, StringComparison.OrdinalIgnoreCase))
                            continue;

                        logs.Add(logEntry);

                        if (logs.Count >= maxEntries)
                            break;
                    }
                    catch (JsonException)
                    {
                        // Skip malformed JSON lines
                        continue;
                    }
                }

                if (logs.Count >= maxEntries)
                    break;
            }
            catch (IOException)
            {
                // Skip files that can't be read
                continue;
            }
        }

        return logs;
    }

    /// <summary>
    /// Get log statistics
    /// </summary>
    public async Task<LogStats> GetStatsAsync()
    {
        var stats = new LogStats
        {
            TotalFiles = 0,
            TotalSizeBytes = 0,
            OldestLogDate = null,
            NewestLogDate = null
        };

        if (!Directory.Exists(_logsDirectory))
        {
            return stats;
        }

        var logFiles = Directory.GetFiles(_logsDirectory, "aura-api-*.json");
        stats.TotalFiles = logFiles.Length;
        stats.TotalSizeBytes = logFiles.Sum(f => new FileInfo(f).Length);

        if (logFiles.Length > 0)
        {
            stats.OldestLogDate = logFiles.Min(f => File.GetCreationTime(f));
            stats.NewestLogDate = logFiles.Max(f => File.GetLastWriteTime(f));
        }

        return stats;
    }
}

/// <summary>
/// Log entry model matching Serilog's JSON output
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string? RenderedMessage { get; set; }
    public string Message => RenderedMessage ?? MessageTemplate;
    public Dictionary<string, object>? Properties { get; set; }
    public string? Exception { get; set; }
    
    // Correlation ID from properties
    public string? CorrelationId => 
        Properties?.TryGetValue("CorrelationId", out var id) == true 
            ? id?.ToString() 
            : null;
}

/// <summary>
/// Log statistics
/// </summary>
public class LogStats
{
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime? OldestLogDate { get; set; }
    public DateTime? NewestLogDate { get; set; }
}
