namespace Aura.Core.Preflight;

/// <summary>
/// Represents a single preflight check result
/// </summary>
public class PreflightCheck
{
    /// <summary>
    /// Name of the check (e.g., "API Keys Validation", "FFmpeg Availability")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the check passed
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Detailed message about the check result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Hint on how to fix the issue (if check failed)
    /// </summary>
    public string? FixHint { get; set; }

    /// <summary>
    /// Link to documentation or download page
    /// </summary>
    public string? Link { get; set; }
}

/// <summary>
/// Result of a preflight run
/// </summary>
public class PreflightResult
{
    /// <summary>
    /// Overall status - true if all checks passed
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// List of individual checks performed
    /// </summary>
    public List<PreflightCheck> Checks { get; set; } = new();

    /// <summary>
    /// Correlation ID for logging and tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}
