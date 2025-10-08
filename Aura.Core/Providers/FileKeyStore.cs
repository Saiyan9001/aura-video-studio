using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aura.Core.Providers;

/// <summary>
/// Simple file-based key store. In production, keys should be encrypted using DPAPI or similar.
/// </summary>
public class FileKeyStore : IKeyStore
{
    private readonly string _keysFilePath;
    private Dictionary<string, string> _cache = new();
    private bool _loaded = false;

    public FileKeyStore(string? keysFilePath = null)
    {
        _keysFilePath = keysFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "apikeys.json");
    }

    public async Task<string?> GetKeyAsync(string providerName)
    {
        await EnsureLoadedAsync();
        return _cache.TryGetValue(providerName.ToLowerInvariant(), out var key) ? key : null;
    }

    public async Task SetKeyAsync(string providerName, string key)
    {
        await EnsureLoadedAsync();
        _cache[providerName.ToLowerInvariant()] = key;
        await SaveAsync();
    }

    public async Task<bool> HasKeyAsync(string providerName)
    {
        await EnsureLoadedAsync();
        var key = await GetKeyAsync(providerName);
        return !string.IsNullOrEmpty(key);
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;

        if (File.Exists(_keysFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_keysFilePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                    ?? new Dictionary<string, string>();
            }
            catch
            {
                _cache = new Dictionary<string, string>();
            }
        }

        _loaded = true;
    }

    private async Task SaveAsync()
    {
        var directory = Path.GetDirectoryName(_keysFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_keysFilePath, json);
    }
}
