using System;
using System.Threading.Tasks;
using Aura.Providers.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class ValidationTests
{
    [Fact]
    public void ValidationResult_Success_CreatesValidResult()
    {
        var result = ValidationResult.Success("TestProvider", "All OK", 100);
        
        Assert.Equal("TestProvider", result.Name);
        Assert.True(result.Ok);
        Assert.Equal("All OK", result.Details);
        Assert.Equal(100, result.ElapsedMs);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ValidationResult_Failure_CreatesInvalidResult()
    {
        var result = ValidationResult.Failure("TestProvider", "Error occurred", 50, "E307");
        
        Assert.Equal("TestProvider", result.Name);
        Assert.False(result.Ok);
        Assert.Equal("Error occurred", result.Details);
        Assert.Equal(50, result.ElapsedMs);
        Assert.Equal("E307", result.ErrorCode);
    }

    [Fact]
    public void KeyStore_MaskKey_MasksCorrectly()
    {
        var logger = NullLogger<KeyStore>.Instance;
        var keyStore = new KeyStore(logger);

        var key = "sk-1234567890abcdefghij";
        var masked = keyStore.MaskKey(key);
        
        Assert.Equal("sk-12345...", masked);
    }

    [Fact]
    public void KeyStore_MaskKey_HandlesShortKey()
    {
        var logger = NullLogger<KeyStore>.Instance;
        var keyStore = new KeyStore(logger);

        var key = "short";
        var masked = keyStore.MaskKey(key);
        
        Assert.Equal("short...", masked);
    }

    [Fact]
    public void KeyStore_MaskKey_HandlesEmptyKey()
    {
        var logger = NullLogger<KeyStore>.Instance;
        var keyStore = new KeyStore(logger);

        var masked = keyStore.MaskKey("");
        
        Assert.Equal("", masked);
    }

    [Fact]
    public async Task KeyStore_GetSetKey_WorksCorrectly()
    {
        var logger = NullLogger<KeyStore>.Instance;
        var keyStore = new KeyStore(logger);

        var testKey = "test-api-key-12345";
        await keyStore.SetKeyAsync("testprovider", testKey);
        
        var retrieved = await keyStore.GetKeyAsync("testprovider");
        
        Assert.Equal(testKey, retrieved);
    }

    [Fact]
    public async Task KeyStore_GetKey_ReturnsNullForNonExistentKey()
    {
        var logger = NullLogger<KeyStore>.Instance;
        var keyStore = new KeyStore(logger);

        var retrieved = await keyStore.GetKeyAsync("nonexistent-provider");
        
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task KeyStore_GetAllKeys_ReturnsAllStoredKeys()
    {
        var logger = NullLogger<KeyStore>.Instance;
        var keyStore = new KeyStore(logger);

        await keyStore.SetKeyAsync("provider1", "key1");
        await keyStore.SetKeyAsync("provider2", "key2");
        
        var allKeys = await keyStore.GetAllKeysAsync();
        
        Assert.True(allKeys.ContainsKey("provider1"));
        Assert.True(allKeys.ContainsKey("provider2"));
        Assert.Equal("key1", allKeys["provider1"]);
        Assert.Equal("key2", allKeys["provider2"]);
    }
}
