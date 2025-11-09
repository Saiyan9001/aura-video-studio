using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Aura.Core.Services.Caching;

/// <summary>
/// Utility for building consistent cache keys
/// </summary>
public static class CacheKeyBuilder
{
    /// <summary>
    /// Builds a cache key for provider responses
    /// </summary>
    public static string ProviderResponse(string providerName, string operation, params string[] parameters)
    {
        var parts = new[] { providerName, operation }.Concat(parameters).ToArray();
        return BuildKey("provider", parts);
    }

    /// <summary>
    /// Builds a cache key for generated scripts
    /// </summary>
    public static string GeneratedScript(string brief, string? style = null)
    {
        return BuildKey("script", new[] { HashString(brief), style ?? "default" });
    }

    /// <summary>
    /// Builds a cache key for audio files
    /// </summary>
    public static string AudioFile(string text, string voice, string provider)
    {
        return BuildKey("audio", new[] { provider, voice, HashString(text) });
    }

    /// <summary>
    /// Builds a cache key for images
    /// </summary>
    public static string Image(string prompt, string style)
    {
        return BuildKey("image", new[] { style, HashString(prompt) });
    }

    /// <summary>
    /// Builds a cache key for user sessions
    /// </summary>
    public static string UserSession(string userId)
    {
        return BuildKey("session", new[] { userId });
    }

    /// <summary>
    /// Builds a cache key for query results
    /// </summary>
    public static string QueryResult(string queryName, params string[] parameters)
    {
        var parts = new[] { queryName }.Concat(parameters).ToArray();
        return BuildKey("query", parts);
    }

    private static string BuildKey(string prefix, string?[] parts)
    {
        var cleanParts = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            cleanParts[i] = parts[i]?.Replace(":", "_").Replace(" ", "_") ?? "null";
        }
        return $"{prefix}:{string.Join(":", cleanParts)}";
    }

    private static string HashString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
