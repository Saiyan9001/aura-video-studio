namespace Aura.Core.Configuration;

/// <summary>
/// Configuration for OpenAI API provider
/// </summary>
public class OpenAIConfiguration
{
    /// <summary>
    /// OpenAI API key (starting with sk-, sk-proj-, or sk-live-)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for script generation (e.g., gpt-4o-mini, gpt-4o, o1-preview)
    /// Leave empty to let user select from available models
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens for completion requests
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// Temperature for creative control (0.0 to 1.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Optional base URL for custom OpenAI endpoints or proxies
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Optional organization ID for team/organization accounts
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Optional project ID for project-scoped keys
    /// </summary>
    public string? ProjectId { get; set; }
}
