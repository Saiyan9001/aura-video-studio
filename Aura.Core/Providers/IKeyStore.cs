using System.Threading.Tasks;

namespace Aura.Core.Providers;

/// <summary>
/// Interface for securely storing and retrieving API keys
/// </summary>
public interface IKeyStore
{
    /// <summary>
    /// Retrieves an API key for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "OpenAI", "Azure", "Gemini")</param>
    /// <returns>The API key, or null if not found</returns>
    Task<string?> GetKeyAsync(string providerName);
    
    /// <summary>
    /// Stores an API key for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the provider</param>
    /// <param name="key">The API key to store</param>
    Task SetKeyAsync(string providerName, string key);
    
    /// <summary>
    /// Checks if a key exists for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the provider</param>
    /// <returns>True if a key is stored, false otherwise</returns>
    Task<bool> HasKeyAsync(string providerName);
}
