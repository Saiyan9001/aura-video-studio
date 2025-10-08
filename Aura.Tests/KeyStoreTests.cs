using System;
using System.IO;
using System.Threading.Tasks;
using Aura.Core.Providers;
using Xunit;

namespace Aura.Tests;

public class KeyStoreTests
{
    [Fact]
    public async Task FileKeyStore_Should_StoreAndRetrieveKey()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        var keyStore = new FileKeyStore(tempPath);

        try
        {
            // Act
            await keyStore.SetKeyAsync("openai", "sk-test-key-123");
            var retrievedKey = await keyStore.GetKeyAsync("openai");

            // Assert
            Assert.Equal("sk-test-key-123", retrievedKey);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileKeyStore_Should_ReturnNullForMissingKey()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        var keyStore = new FileKeyStore(tempPath);

        try
        {
            // Act
            var retrievedKey = await keyStore.GetKeyAsync("nonexistent");

            // Assert
            Assert.Null(retrievedKey);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileKeyStore_Should_BeCaseInsensitive()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        var keyStore = new FileKeyStore(tempPath);

        try
        {
            // Act
            await keyStore.SetKeyAsync("OpenAI", "sk-test-key-123");
            var retrievedKey1 = await keyStore.GetKeyAsync("openai");
            var retrievedKey2 = await keyStore.GetKeyAsync("OPENAI");

            // Assert
            Assert.Equal("sk-test-key-123", retrievedKey1);
            Assert.Equal("sk-test-key-123", retrievedKey2);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileKeyStore_Should_UpdateExistingKey()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        var keyStore = new FileKeyStore(tempPath);

        try
        {
            // Act
            await keyStore.SetKeyAsync("openai", "old-key");
            await keyStore.SetKeyAsync("openai", "new-key");
            var retrievedKey = await keyStore.GetKeyAsync("openai");

            // Assert
            Assert.Equal("new-key", retrievedKey);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileKeyStore_Should_PersistAcrossInstances()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        
        try
        {
            // Act - Store with first instance
            var keyStore1 = new FileKeyStore(tempPath);
            await keyStore1.SetKeyAsync("openai", "sk-test-key-123");

            // Act - Retrieve with second instance
            var keyStore2 = new FileKeyStore(tempPath);
            var retrievedKey = await keyStore2.GetKeyAsync("openai");

            // Assert
            Assert.Equal("sk-test-key-123", retrievedKey);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileKeyStore_Should_ReportKeyExistence()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        var keyStore = new FileKeyStore(tempPath);

        try
        {
            // Act
            await keyStore.SetKeyAsync("openai", "sk-test-key-123");
            var hasOpenAI = await keyStore.HasKeyAsync("openai");
            var hasAzure = await keyStore.HasKeyAsync("azure");

            // Assert
            Assert.True(hasOpenAI);
            Assert.False(hasAzure);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileKeyStore_Should_HandleMultipleKeys()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-keys-{Guid.NewGuid()}.json");
        var keyStore = new FileKeyStore(tempPath);

        try
        {
            // Act
            await keyStore.SetKeyAsync("openai", "sk-openai-key");
            await keyStore.SetKeyAsync("azure", "sk-azure-key");
            await keyStore.SetKeyAsync("gemini", "sk-gemini-key");

            var openaiKey = await keyStore.GetKeyAsync("openai");
            var azureKey = await keyStore.GetKeyAsync("azure");
            var geminiKey = await keyStore.GetKeyAsync("gemini");

            // Assert
            Assert.Equal("sk-openai-key", openaiKey);
            Assert.Equal("sk-azure-key", azureKey);
            Assert.Equal("sk-gemini-key", geminiKey);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
