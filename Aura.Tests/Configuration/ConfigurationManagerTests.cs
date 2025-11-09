using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Data;
using Aura.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests.Configuration;

public class ConfigurationManagerTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AuraDbContext _context;
    private readonly ConfigurationManager _configManager;

    public ConfigurationManagerTests()
    {
        var services = new ServiceCollection();

        // Setup in-memory database
        services.AddDbContext<AuraDbContext>(options =>
            options.UseInMemoryDatabase($"ConfigTest_{Guid.NewGuid()}"));

        // Add required services
        services.AddLogging(builder => builder.AddConsole());
        services.AddMemoryCache();
        services.AddScoped<ConfigurationRepository>();
        services.AddSingleton<ConfigurationManager>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<AuraDbContext>();
        _configManager = _serviceProvider.GetRequiredService<ConfigurationManager>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_Should_CreateDefaultConfigurations()
    {
        // Act
        await _configManager.InitializeAsync();

        // Assert
        var configs = await _context.Configurations.ToListAsync();
        Assert.NotEmpty(configs);
        Assert.Contains(configs, c => c.Key == "General.AutosaveEnabled");
        Assert.Contains(configs, c => c.Key == "VideoDefaults.DefaultResolution");
    }

    [Fact]
    public async Task GetStringAsync_Should_ReturnValue_WhenExists()
    {
        // Arrange
        await _configManager.SetAsync("TestKey", "TestValue", "TestCategory");

        // Act
        var value = await _configManager.GetStringAsync("TestKey");

        // Assert
        Assert.Equal("TestValue", value);
    }

    [Fact]
    public async Task GetStringAsync_Should_ReturnDefault_WhenNotExists()
    {
        // Act
        var value = await _configManager.GetStringAsync("NonExistentKey", "DefaultValue");

        // Assert
        Assert.Equal("DefaultValue", value);
    }

    [Fact]
    public async Task GetIntAsync_Should_ReturnValue_WhenExists()
    {
        // Arrange
        await _configManager.SetAsync("IntKey", 42, "TestCategory");

        // Act
        var value = await _configManager.GetIntAsync("IntKey");

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task GetBoolAsync_Should_ReturnValue_WhenExists()
    {
        // Arrange
        await _configManager.SetAsync("BoolKey", true, "TestCategory");

        // Act
        var value = await _configManager.GetBoolAsync("BoolKey");

        // Assert
        Assert.True(value);
    }

    [Fact]
    public async Task SetAsync_Should_CreateNewConfiguration()
    {
        // Act
        await _configManager.SetAsync("NewKey", "NewValue", "NewCategory");

        // Assert
        var config = await _context.Configurations.FindAsync("NewKey");
        Assert.NotNull(config);
        Assert.Equal("NewValue", config.Value);
        Assert.Equal("NewCategory", config.Category);
    }

    [Fact]
    public async Task SetAsync_Should_UpdateExistingConfiguration()
    {
        // Arrange
        await _configManager.SetAsync("UpdateKey", "OldValue", "TestCategory");
        
        // Act
        await _configManager.SetAsync("UpdateKey", "NewValue", "TestCategory");

        // Assert
        var config = await _context.Configurations.FindAsync("UpdateKey");
        Assert.NotNull(config);
        Assert.Equal("NewValue", config.Value);
        Assert.Equal(2, config.Version); // Version should increment
    }

    [Fact]
    public async Task GetCategoryAsync_Should_ReturnAllConfigsInCategory()
    {
        // Arrange
        await _configManager.SetAsync("Cat1.Key1", "Value1", "Category1");
        await _configManager.SetAsync("Cat1.Key2", "Value2", "Category1");
        await _configManager.SetAsync("Cat2.Key1", "Value3", "Category2");

        // Act
        var configs = await _configManager.GetCategoryAsync("Category1");

        // Assert
        Assert.Equal(2, configs.Count);
        Assert.True(configs.ContainsKey("Cat1.Key1"));
        Assert.True(configs.ContainsKey("Cat1.Key2"));
        Assert.False(configs.ContainsKey("Cat2.Key1"));
    }

    [Fact]
    public async Task SetManyAsync_Should_SetMultipleConfigurations()
    {
        // Arrange
        var configs = new Dictionary<string, (object value, string category, string? description)>
        {
            { "Bulk1", ("Value1", "BulkCategory", null) },
            { "Bulk2", ("Value2", "BulkCategory", null) },
            { "Bulk3", ("Value3", "BulkCategory", null) }
        };

        // Act
        await _configManager.SetManyAsync(configs);

        // Assert
        var config1 = await _context.Configurations.FindAsync("Bulk1");
        var config2 = await _context.Configurations.FindAsync("Bulk2");
        var config3 = await _context.Configurations.FindAsync("Bulk3");

        Assert.NotNull(config1);
        Assert.NotNull(config2);
        Assert.NotNull(config3);
    }

    [Fact]
    public async Task DeleteAsync_Should_SoftDeleteConfiguration()
    {
        // Arrange
        await _configManager.SetAsync("DeleteKey", "DeleteValue", "TestCategory");

        // Act
        var result = await _configManager.DeleteAsync("DeleteKey");

        // Assert
        Assert.True(result);
        var config = await _context.Configurations.FindAsync("DeleteKey");
        Assert.NotNull(config);
        Assert.False(config.IsActive); // Soft delete
    }

    [Fact]
    public async Task GetAsync_Should_UseCacheOnSecondCall()
    {
        // Arrange
        await _configManager.SetAsync("CacheKey", "CacheValue", "TestCategory");

        // Act - First call (should hit database)
        var value1 = await _configManager.GetStringAsync("CacheKey");
        
        // Change value in database directly (bypassing cache)
        var config = await _context.Configurations.FindAsync("CacheKey");
        config!.Value = "ModifiedValue";
        await _context.SaveChangesAsync();

        // Second call (should hit cache)
        var value2 = await _configManager.GetStringAsync("CacheKey");

        // Assert
        Assert.Equal("CacheValue", value1);
        Assert.Equal("CacheValue", value2); // Should be cached value, not modified
    }

    [Fact]
    public async Task SetAsync_Should_InvalidateCache()
    {
        // Arrange
        await _configManager.SetAsync("InvalidateKey", "OldValue", "TestCategory");
        await _configManager.GetStringAsync("InvalidateKey"); // Cache it

        // Act
        await _configManager.SetAsync("InvalidateKey", "NewValue", "TestCategory");
        var value = await _configManager.GetStringAsync("InvalidateKey");

        // Assert
        Assert.Equal("NewValue", value); // Should get new value, not cached
    }

    [Fact]
    public async Task GetAllConfigurationsAsync_Should_ReturnAllConfigurations()
    {
        // Arrange
        await _configManager.SetAsync("All1", "Value1", "Cat1");
        await _configManager.SetAsync("All2", "Value2", "Cat2");
        await _configManager.DeleteAsync("All2"); // Soft delete one

        // Act
        var allConfigs = await _configManager.GetAllConfigurationsAsync(false);
        var allConfigsIncludeInactive = await _configManager.GetAllConfigurationsAsync(true);

        // Assert
        Assert.Equal(1, allConfigs.Count); // Only active
        Assert.Equal(2, allConfigsIncludeInactive.Count); // All including inactive
    }
}
