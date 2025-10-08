using System.Threading;
using System.Threading.Tasks;

namespace Aura.Providers.Validation;

/// <summary>
/// Interface for validating provider connectivity and API keys
/// </summary>
public interface IProviderValidator
{
    /// <summary>
    /// Name of the provider being validated
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Validates the provider's connectivity and configuration
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with status and details</returns>
    Task<ValidationResult> ValidateAsync(CancellationToken ct = default);
}
