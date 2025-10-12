using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aura.Core.Downloads;

/// <summary>
/// Represents an engine that can be installed and managed by Aura
/// </summary>
public class EngineManifestEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("archiveType")]
    public string ArchiveType { get; set; } = "zip"; // zip, tar.gz, etc.

    [JsonPropertyName("urls")]
    public Dictionary<string, string> Urls { get; set; } = new(); // platform -> url

    [JsonPropertyName("mirrors")]
    public Dictionary<string, List<string>>? Mirrors { get; set; } // platform -> list of mirror URLs

    [JsonPropertyName("extractDir")]
    public string? ExtractDir { get; set; }

    [JsonPropertyName("entrypoint")]
    public string Entrypoint { get; set; } = string.Empty;

    [JsonPropertyName("defaultPort")]
    public int? DefaultPort { get; set; }

    [JsonPropertyName("argsTemplate")]
    public string? ArgsTemplate { get; set; }

    [JsonPropertyName("healthCheck")]
    public HealthCheckConfig? HealthCheck { get; set; }

    [JsonPropertyName("licenseUrl")]
    public string? LicenseUrl { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("requiredVRAMGB")]
    public int? RequiredVRAMGB { get; set; }

    [JsonPropertyName("vramTooltip")]
    public string? VramTooltip { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("models")]
    public List<ModelEntry>? Models { get; set; }
}

public class HealthCheckConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 30;
}

public class ModelEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

public class EngineManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("engines")]
    public List<EngineManifestEntry> Engines { get; set; } = new();
}
