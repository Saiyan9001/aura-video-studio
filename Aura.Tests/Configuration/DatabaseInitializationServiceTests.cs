using System;
using System.IO;
using System.Threading.Tasks;
using Aura.Core.Data;
using Aura.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests.Configuration;

public class DatabaseInitializationServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly ServiceProvider _serviceProvider;

    public DatabaseInitializationServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test-aura-{Guid.NewGuid()}.db");

        var services = new ServiceCollection();
        
        // Use real SQLite database for initialization tests
        services.AddDbContext<AuraDbContext>(options =>
            options.UseSqlite($"Data Source={_testDbPath}"));

        services.AddLogging();
        services.AddSingleton<DatabaseInitializationService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_Should_CreateDatabase_WhenNotExists()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<DatabaseInitializationService>();
        Assert.False(File.Exists(_testDbPath));

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.True(result.DatabaseExists || File.Exists(_testDbPath)); // Database created
        Assert.True(result.PathWritable);
        Assert.True(result.MigrationsApplied);
    }

    [Fact]
    public async Task InitializeAsync_Should_ApplyMigrations()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<DatabaseInitializationService>();

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result.MigrationsApplied);
        Assert.True(result.Success);
        
        // Verify tables exist
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuraDbContext>();
        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect);
    }

    [Fact]
    public async Task InitializeAsync_Should_EnableWalMode()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<DatabaseInitializationService>();

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result.WalModeEnabled);
    }

    [Fact]
    public async Task InitializeAsync_Should_CheckIntegrity()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<DatabaseInitializationService>();

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result.IntegrityCheck);
        Assert.False(result.RepairAttempted); // Should not need repair for new DB
    }

    [Fact]
    public async Task InitializeAsync_Should_ReturnSuccess_ForHealthyDatabase()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<DatabaseInitializationService>();
        
        // Create database first
        await service.InitializeAsync();

        // Act - Initialize again
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task InitializeAsync_Should_MeasureDuration()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<DatabaseInitializationService>();

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result.DurationMs > 0);
        Assert.NotNull(result.EndTime);
    }
}
