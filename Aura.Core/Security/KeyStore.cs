using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Security;

/// <summary>
/// Key storage implementation that uses DPAPI on Windows and plaintext dev storage on Linux
/// </summary>
public class KeyStore : IKeyStore
{
    private readonly ILogger<KeyStore> _logger;
    private readonly string _storePath;
    private readonly bool _useEncryption;

    public KeyStore(ILogger<KeyStore> logger)
    {
        _logger = logger;
        _useEncryption = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (_useEncryption)
        {
            // Windows: Use DPAPI with storage in LocalApplicationData
            _storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "apikeys.dat");
        }
        else
        {
            // Linux/Mac: Use plaintext dev storage in home directory
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _storePath = Path.Combine(homeDir, ".aura-dev", "apikeys.json");
            _logger.LogWarning("Using plaintext key storage for development. Not suitable for production.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
    }

    public async Task<string?> GetKeyAsync(string keyName)
    {
        try
        {
            var keys = await LoadKeysAsync();
            return keys.TryGetValue(keyName, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get key {KeyName}", MaskKeyName(keyName));
            return null;
        }
    }

    public async Task SetKeyAsync(string keyName, string keyValue)
    {
        try
        {
            var keys = await LoadKeysAsync();
            keys[keyName] = keyValue;
            await SaveKeysAsync(keys);
            _logger.LogInformation("Key {KeyName} updated successfully", MaskKeyName(keyName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set key {KeyName}", MaskKeyName(keyName));
            throw;
        }
    }

    public async Task<bool> HasKeyAsync(string keyName)
    {
        var key = await GetKeyAsync(keyName);
        return !string.IsNullOrWhiteSpace(key);
    }

    public async Task<string[]> GetKeyNamesAsync()
    {
        try
        {
            var keys = await LoadKeysAsync();
            return keys.Keys.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get key names");
            return Array.Empty<string>();
        }
    }

    public string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "";

        var visibleChars = Math.Min(8, key.Length);
        return key.Substring(0, visibleChars) + "...";
    }

    private async Task<Dictionary<string, string>> LoadKeysAsync()
    {
        if (!File.Exists(_storePath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            if (_useEncryption)
            {
                // Windows: Decrypt with DPAPI
                var encryptedBytes = await File.ReadAllBytesAsync(_storePath);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            else
            {
                // Linux/Mac: Read plaintext JSON
                var json = await File.ReadAllTextAsync(_storePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load keys from {Path}", _storePath);
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveKeysAsync(Dictionary<string, string> keys)
    {
        try
        {
            var json = JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true });

            if (_useEncryption)
            {
                // Windows: Encrypt with DPAPI
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
                await File.WriteAllBytesAsync(_storePath, encryptedBytes);
            }
            else
            {
                // Linux/Mac: Write plaintext JSON
                await File.WriteAllTextAsync(_storePath, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save keys to {Path}", _storePath);
            throw;
        }
    }

    private string MaskKeyName(string keyName)
    {
        // Don't fully mask key names, just indicate we're working with keys
        return $"***{keyName}***";
    }
}
