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

namespace Aura.Providers.Validation;

/// <summary>
/// Key store implementation with DPAPI on Windows and plaintext on Linux/Mac
/// </summary>
public class KeyStore : IKeyStore
{
    private readonly ILogger<KeyStore> _logger;
    private readonly string _keyFilePath;
    private readonly bool _isWindows;

    public KeyStore(ILogger<KeyStore> logger)
    {
        _logger = logger;
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var auraDir = Path.Combine(appDataPath, "Aura");
        
        if (_isWindows)
        {
            _keyFilePath = Path.Combine(auraDir, "apikeys.json");
        }
        else
        {
            // On Linux/Mac, use ~/.aura-dev for development
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var devDir = Path.Combine(homeDir, ".aura-dev");
            Directory.CreateDirectory(devDir);
            _keyFilePath = Path.Combine(devDir, "apikeys.json");
            _logger.LogWarning("Running on non-Windows platform. API keys will be stored in plaintext at {Path}", _keyFilePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_keyFilePath)!);
    }

    public async Task<string?> GetKeyAsync(string providerName)
    {
        try
        {
            var allKeys = await GetAllKeysAsync();
            return allKeys.TryGetValue(providerName.ToLowerInvariant(), out var key) ? key : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API key for {Provider}", providerName);
            return null;
        }
    }

    public async Task SetKeyAsync(string providerName, string key)
    {
        try
        {
            var allKeys = await GetAllKeysAsync();
            allKeys[providerName.ToLowerInvariant()] = key;
            await SaveKeysAsync(allKeys);
            _logger.LogInformation("API key for {Provider} updated (masked: {MaskedKey})", 
                providerName, MaskKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing API key for {Provider}", providerName);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetAllKeysAsync()
    {
        try
        {
            if (!File.Exists(_keyFilePath))
            {
                return new Dictionary<string, string>();
            }

            var encryptedData = await File.ReadAllTextAsync(_keyFilePath);
            var keysDict = JsonSerializer.Deserialize<Dictionary<string, string>>(encryptedData);
            
            if (keysDict == null)
            {
                return new Dictionary<string, string>();
            }

            // On Windows, decrypt values using DPAPI
            if (_isWindows)
            {
                var decryptedKeys = new Dictionary<string, string>();
                foreach (var kvp in keysDict)
                {
                    if (string.IsNullOrEmpty(kvp.Value))
                    {
                        decryptedKeys[kvp.Key] = string.Empty;
                        continue;
                    }

                    try
                    {
                        var encryptedBytes = Convert.FromBase64String(kvp.Value);
                        var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                        decryptedKeys[kvp.Key] = Encoding.UTF8.GetString(decryptedBytes);
                    }
                    catch
                    {
                        // If decryption fails, assume it's already plaintext (for migration)
                        decryptedKeys[kvp.Key] = kvp.Value;
                    }
                }
                return decryptedKeys;
            }

            // On Linux/Mac, keys are stored in plaintext
            return keysDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading API keys from {Path}", _keyFilePath);
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveKeysAsync(Dictionary<string, string> keys)
    {
        try
        {
            var keysToSave = new Dictionary<string, string>();

            // On Windows, encrypt values using DPAPI
            if (_isWindows)
            {
                foreach (var kvp in keys)
                {
                    if (string.IsNullOrEmpty(kvp.Value))
                    {
                        keysToSave[kvp.Key] = string.Empty;
                        continue;
                    }

                    var plainBytes = Encoding.UTF8.GetBytes(kvp.Value);
                    var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                    keysToSave[kvp.Key] = Convert.ToBase64String(encryptedBytes);
                }
            }
            else
            {
                // On Linux/Mac, store in plaintext
                keysToSave = keys;
            }

            var json = JsonSerializer.Serialize(keysToSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_keyFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving API keys to {Path}", _keyFilePath);
            throw;
        }
    }

    public string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        var visibleChars = Math.Min(8, key.Length);
        return key.Substring(0, visibleChars) + "...";
    }
}
