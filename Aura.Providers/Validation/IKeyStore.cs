using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aura.Providers.Validation;

/// <summary>
/// Interface for secure storage and retrieval of API keys
/// </summary>
public interface IKeyStore
{
    /// <summary>
    /// Retrieves an API key for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "openai", "elevenlabs")</param>
    /// <returns>API key or null if not found</returns>
    Task<string?> GetKeyAsync(string providerName);

    /// <summary>
    /// Stores an API key for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the provider</param>
    /// <param name="key">API key to store</param>
    Task SetKeyAsync(string providerName, string key);

    /// <summary>
    /// Retrieves all stored API keys
    /// </summary>
    /// <returns>Dictionary of provider names to API keys</returns>
    Task<Dictionary<string, string>> GetAllKeysAsync();

    /// <summary>
    /// Masks an API key for safe logging (shows first 8 chars + "...")
    /// </summary>
    /// <param name="key">API key to mask</param>
    /// <returns>Masked key</returns>
    string MaskKey(string key);
}
