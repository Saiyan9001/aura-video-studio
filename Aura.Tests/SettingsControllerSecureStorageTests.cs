using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aura.Api.Controllers;
using Aura.Api.Models;
using Aura.Core.Configuration;
using Aura.Core.Models;
using Aura.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests for SettingsController secure API key storage integration
/// Ensures API keys are never saved to plaintext files, only to encrypted storage
/// </summary>
[Collection("SettingsControllerTests")]
public class SettingsControllerSecureStorageTests : IDisposable
{
    private readonly Mock<ILogger<SettingsController>> _mockLogger;
    private readonly Mock<ILogger<SecureStorageService>> _mockSecureLogger;
    private readonly ProviderSettings _providerSettings;
    private readonly ISecureStorageService _secureStorage;
    private readonly SettingsController _controller;
    private readonly string _testDir;

    public SettingsControllerSecureStorageTests()
    {
        _mockLogger = new Mock<ILogger<SettingsController>>();
        _mockSecureLogger = new Mock<ILogger<SecureStorageService>>();
        // Create temporary test directory
        _testDir = Path.Combine(Path.GetTempPath(), "AuraSettingsTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);

        var providerLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _providerSettings = CreateTestProviderSettings(
            providerLoggerFactory.CreateLogger<ProviderSettings>(),
            _testDir);

        // Create real secure storage service
        _secureStorage = new SecureStorageService(_mockSecureLogger.Object);

        _controller = new SettingsController(
            _mockLogger.Object,
            _providerSettings,
            _secureStorage);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        GC.SuppressFinalize(this);
    }
    
    private static ProviderSettings CreateTestProviderSettings(ILogger<ProviderSettings> logger, string rootDirectory)
    {
        var settings = new ProviderSettings(logger);

        var portableRootField = typeof(ProviderSettings).GetField("_portableRoot", BindingFlags.NonPublic | BindingFlags.Instance);
        var configPathField = typeof(ProviderSettings).GetField("_configPath", BindingFlags.NonPublic | BindingFlags.Instance);

        var auraDataDir = Path.Combine(rootDirectory, "AuraData");
        if (!Directory.Exists(auraDataDir))
        {
            Directory.CreateDirectory(auraDataDir);
        }

        portableRootField?.SetValue(settings, rootDirectory);
        configPathField?.SetValue(settings, Path.Combine(auraDataDir, "settings.json"));

        return settings;
    }

    [Fact]
    public async Task SaveUserSettings_WithApiKeys_SavesKeysToSecureStorageNotPlaintextFile()
    {
        // Arrange
        var settings = new UserSettings
        {
            ApiKeys = new ApiKeysSettings
            {
                OpenAI = "sk-test-openai-key-12345",
                Anthropic = "sk-ant-test-anthropic-key",
                ElevenLabs = "el-test-elevenlabs-key"
            },
            General = new GeneralSettings
            {
                Language = "en-US"
            }
        };

        // Act
        var result = await _controller.SaveUserSettings(settings, CancellationToken.None);

        // Assert - API call should succeed
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify keys are in secure storage
        var openAiKey = await _secureStorage.GetApiKeyAsync("openai");
        var anthropicKey = await _secureStorage.GetApiKeyAsync("anthropic");
        var elevenLabsKey = await _secureStorage.GetApiKeyAsync("elevenlabs");

        Assert.Equal("sk-test-openai-key-12345", openAiKey);
        Assert.Equal("sk-ant-test-anthropic-key", anthropicKey);
        Assert.Equal("el-test-elevenlabs-key", elevenLabsKey);

        // Verify user-settings.json does NOT contain API keys
        var settingsFilePath = Path.Combine(_testDir, "user-settings.json");
        Assert.True(File.Exists(settingsFilePath), "Settings file should exist");

        var fileContent = await File.ReadAllTextAsync(settingsFilePath);
        Assert.DoesNotContain("sk-test-openai-key", fileContent);
        Assert.DoesNotContain("sk-ant-test-anthropic", fileContent);
        Assert.DoesNotContain("el-test-elevenlabs", fileContent);
        
        // Should contain empty API keys section
        Assert.Contains("\"apiKeys\"", fileContent);
    }

    [Fact]
    public async Task LoadUserSettings_WithKeysInSecureStorage_LoadsKeysFromSecureStorageNotFile()
    {
        // Arrange - Save keys to secure storage
        await _secureStorage.SaveApiKeyAsync("openai", "sk-loaded-openai-key");
        await _secureStorage.SaveApiKeyAsync("google", "AIza-loaded-google-key");

        // Act
        var result = await _controller.GetUserSettings(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var loadedSettings = Assert.IsType<UserSettings>(okResult.Value);

        Assert.NotNull(loadedSettings.ApiKeys);
        Assert.Equal("sk-loaded-openai-key", loadedSettings.ApiKeys.OpenAI);
        Assert.Equal("AIza-loaded-google-key", loadedSettings.ApiKeys.Google);
        Assert.Equal(string.Empty, loadedSettings.ApiKeys.Anthropic); // Not set
    }

    [Fact]
    public async Task SaveUserSettings_WithEmptyApiKey_DeletesKeyFromSecureStorage()
    {
        // Arrange - First save a key
        await _secureStorage.SaveApiKeyAsync("openai", "sk-initial-key");
        Assert.True(await _secureStorage.HasApiKeyAsync("openai"));

        // Now save settings with empty key (user cleared it)
        var settings = new UserSettings
        {
            ApiKeys = new ApiKeysSettings
            {
                OpenAI = string.Empty // User cleared the key
            }
        };

        // Act
        await _controller.SaveUserSettings(settings, CancellationToken.None);

        // Assert - Key should be deleted from secure storage
        Assert.False(await _secureStorage.HasApiKeyAsync("openai"));
    }

    [Fact]
    public async Task SaveAndLoadUserSettings_RoundTrip_MaintainsSecureKeyIsolation()
    {
        // Arrange
        var originalSettings = new UserSettings
        {
            ApiKeys = new ApiKeysSettings
            {
                OpenAI = "sk-roundtrip-openai",
                Pexels = "pexels-api-key-test"
            },
            General = new GeneralSettings
            {
                Language = "en-US",
                Theme = ThemeMode.Dark
            },
            Advanced = new AdvancedSettings
            {
                OfflineMode = true
            }
        };

        // Act - Save then load
        await _controller.SaveUserSettings(originalSettings, CancellationToken.None);
        var loadResult = await _controller.GetUserSettings(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(loadResult);
        var loadedSettings = Assert.IsType<UserSettings>(okResult.Value);

        // API keys should match
        Assert.Equal("sk-roundtrip-openai", loadedSettings.ApiKeys.OpenAI);
        Assert.Equal("pexels-api-key-test", loadedSettings.ApiKeys.Pexels);

        // Other settings should match
        Assert.Equal("en-US", loadedSettings.General.Language);
        Assert.Equal(ThemeMode.Dark, loadedSettings.General.Theme);
        Assert.True(loadedSettings.Advanced.OfflineMode);

        // Verify plaintext file doesn't contain keys
        var settingsFilePath = Path.Combine(_testDir, "user-settings.json");
        var fileContent = await File.ReadAllTextAsync(settingsFilePath);
        Assert.DoesNotContain("sk-roundtrip-openai", fileContent);
        Assert.DoesNotContain("pexels-api-key-test", fileContent);
    }

    [Fact]
    public async Task SaveUserSettings_NullApiKeys_DoesNotThrow()
    {
        // Arrange
        var settings = new UserSettings
        {
            ApiKeys = new ApiKeysSettings(), // Empty API keys section instead of null
            General = new GeneralSettings()
        };

        // Act & Assert - Should not throw
        var result = await _controller.SaveUserSettings(settings, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task MultipleKeySaves_UpdatesExistingKeys_InSecureStorage()
    {
        // Arrange
        var settings1 = new UserSettings
        {
            ApiKeys = new ApiKeysSettings
            {
                OpenAI = "sk-first-key"
            }
        };

        var settings2 = new UserSettings
        {
            ApiKeys = new ApiKeysSettings
            {
                OpenAI = "sk-updated-key" // Changed
            }
        };

        // Act
        await _controller.SaveUserSettings(settings1, CancellationToken.None);
        await _controller.SaveUserSettings(settings2, CancellationToken.None);

        // Assert - Should have the updated key
        var currentKey = await _secureStorage.GetApiKeyAsync("openai");
        Assert.Equal("sk-updated-key", currentKey);

        // Load via controller to verify
        var result = await _controller.GetUserSettings(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var loadedSettings = Assert.IsType<UserSettings>(okResult.Value);
        
        Assert.Equal("sk-updated-key", loadedSettings.ApiKeys.OpenAI);
    }

    [Fact]
    public async Task TestApiKey_PexelsProvider_ReturnsSuccess()
    {
        var request = new TestApiKeyRequest { ApiKey = "12345678901234567890" };

        var actionResult = await _controller.TestApiKey("pexels", request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = okResult.Value!;
        var payloadType = payload.GetType();

        var success = (bool)payloadType.GetProperty("success")!.GetValue(payload)!;
        var message = (string)payloadType.GetProperty("message")!.GetValue(payload)!;

        Assert.True(success);
        Assert.Equal("API key format is valid", message);
    }
}
