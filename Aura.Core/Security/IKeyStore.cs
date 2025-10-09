using System.Threading.Tasks;

namespace Aura.Core.Security;

/// <summary>
/// Interface for secure API key storage
/// </summary>
public interface IKeyStore
{
    /// <summary>
    /// Get an API key by name
    /// </summary>
    Task<string?> GetKeyAsync(string keyName);

    /// <summary>
    /// Set an API key by name
    /// </summary>
    Task SetKeyAsync(string keyName, string keyValue);

    /// <summary>
    /// Check if a key exists and is not empty
    /// </summary>
    Task<bool> HasKeyAsync(string keyName);

    /// <summary>
    /// Get all available key names
    /// </summary>
    Task<string[]> GetKeyNamesAsync();

    /// <summary>
    /// Mask a key for logging (show only first 8 characters)
    /// </summary>
    string MaskKey(string key);
}
