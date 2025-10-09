using System;
using System.Collections.Generic;

namespace Aura.Core.Models;

/// <summary>
/// Represents a single preflight check result
/// </summary>
public record PreflightCheckResult
{
    public string Name { get; init; } = string.Empty;
    public bool Ok { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? FixHint { get; init; }
    public string? Link { get; init; }
    public string? Severity { get; init; } = "error"; // error, warning, info
}

/// <summary>
/// Overall preflight result containing all checks
/// </summary>
public record PreflightResult
{
    public bool Ok { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public List<PreflightCheckResult> Checks { get; init; } = new();
    public bool CanAutoSwitchToFree { get; init; }
}
