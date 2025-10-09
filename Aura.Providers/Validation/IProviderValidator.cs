using System.Threading;
using System.Threading.Tasks;

namespace Aura.Providers.Validation;

/// <summary>
/// Result of a provider validation check
/// </summary>
public record ValidationResult
{
    public required string Name { get; init; }
    public required bool Ok { get; init; }
    public required string Details { get; init; }
    public required long ElapsedMs { get; init; }
}

/// <summary>
/// Interface for provider validators
/// </summary>
public interface IProviderValidator
{
    /// <summary>
    /// Name of the provider being validated
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this is a cloud provider (vs local)
    /// </summary>
    bool IsCloudProvider { get; }

    /// <summary>
    /// Validate provider connectivity and configuration
    /// </summary>
    Task<ValidationResult> ValidateAsync(CancellationToken ct = default);
}
