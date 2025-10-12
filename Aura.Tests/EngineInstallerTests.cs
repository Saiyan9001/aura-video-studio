using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Aura.Core.Downloads;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class EngineInstallerTests : IDisposable
{
    private readonly ILogger<EngineInstaller> _logger;
    private readonly string _testDirectory;
    private readonly string _installRoot;
    private readonly HttpClient _httpClient;

    public EngineInstallerTests()
    {
        _logger = NullLogger<EngineInstaller>.Instance;
        _testDirectory = Path.Combine(Path.GetTempPath(), "aura-engine-tests-" + Guid.NewGuid().ToString());
        _installRoot = Path.Combine(_testDirectory, "engines");
        _httpClient = new HttpClient();
        
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_installRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _httpClient.Dispose();
    }

    [Fact]
    public void GetInstallPath_Should_ReturnCorrectPath()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);

        // Act
        var path = installer.GetInstallPath("test-engine");

        // Assert
        Assert.Equal(Path.Combine(_installRoot, "test-engine"), path);
    }

    [Fact]
    public void IsInstalled_Should_ReturnFalse_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);

        // Act
        var isInstalled = installer.IsInstalled("nonexistent-engine");

        // Assert
        Assert.False(isInstalled);
    }

    [Fact]
    public async Task ReadProvenanceAsync_Should_ReturnProvenance_WhenFileExists()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var enginePath = installer.GetInstallPath("test-engine");
        Directory.CreateDirectory(enginePath);
        
        var provenance = new InstallProvenance(
            "test-engine",
            "1.0.0",
            DateTime.UtcNow.ToString("o"),
            enginePath,
            "Mirror",
            "http://test.com/file.zip",
            "abc123"
        );
        
        var provenanceFile = Path.Combine(enginePath, "install.json");
        var json = System.Text.Json.JsonSerializer.Serialize(provenance, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(provenanceFile, json);

        // Act
        var result = await installer.ReadProvenanceAsync("test-engine");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-engine", result.EngineId);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal("Mirror", result.Source);
    }

    [Fact]
    public async Task ReadProvenanceAsync_Should_ReturnNull_WhenFileDoesNotExist()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);

        // Act
        var result = await installer.ReadProvenanceAsync("nonexistent-engine");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsInstalled_Should_ReturnFalse_WhenDirectoryIsEmpty()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var enginePath = installer.GetInstallPath("empty-engine");
        Directory.CreateDirectory(enginePath);

        // Act
        var isInstalled = installer.IsInstalled("empty-engine");

        // Assert
        Assert.False(isInstalled);
    }

    [Fact]
    public void IsInstalled_Should_ReturnTrue_WhenDirectoryHasFiles()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var enginePath = installer.GetInstallPath("installed-engine");
        Directory.CreateDirectory(enginePath);
        File.WriteAllText(Path.Combine(enginePath, "test.txt"), "test content");

        // Act
        var isInstalled = installer.IsInstalled("installed-engine");

        // Assert
        Assert.True(isInstalled);
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnNotInstalled_WhenEngineDoesNotExist()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var engine = new EngineManifestEntry
        {
            Id = "test-engine",
            Name = "Test Engine",
            Version = "1.0",
            Entrypoint = "test.exe"
        };

        // Act
        var result = await installer.VerifyAsync(engine);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Not installed", result.Status);
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnInvalid_WhenEntrypointMissing()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var engine = new EngineManifestEntry
        {
            Id = "test-engine",
            Name = "Test Engine",
            Version = "1.0",
            Entrypoint = "missing.exe"
        };
        
        var enginePath = installer.GetInstallPath(engine.Id);
        Directory.CreateDirectory(enginePath);
        File.WriteAllText(Path.Combine(enginePath, "other.txt"), "content");

        // Act
        var result = await installer.VerifyAsync(engine);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("missing.exe", result.MissingFiles);
        Assert.Contains("Entrypoint file not found", result.Issues[0]);
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnValid_WhenAllFilesPresent()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var engine = new EngineManifestEntry
        {
            Id = "test-engine",
            Name = "Test Engine",
            Version = "1.0",
            Entrypoint = "test.exe"
        };
        
        var enginePath = installer.GetInstallPath(engine.Id);
        Directory.CreateDirectory(enginePath);
        File.WriteAllText(Path.Combine(enginePath, "test.exe"), "executable content");

        // Act
        var result = await installer.VerifyAsync(engine);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Valid", result.Status);
        Assert.Empty(result.MissingFiles);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task RemoveAsync_Should_DeleteEngineDirectory()
    {
        // Arrange
        var installer = new EngineInstaller(_logger, _httpClient, _installRoot);
        var engine = new EngineManifestEntry
        {
            Id = "test-engine",
            Name = "Test Engine",
            Version = "1.0",
            Entrypoint = "test.exe"
        };
        
        var enginePath = installer.GetInstallPath(engine.Id);
        Directory.CreateDirectory(enginePath);
        File.WriteAllText(Path.Combine(enginePath, "test.txt"), "content");

        // Act
        await installer.RemoveAsync(engine);

        // Assert
        Assert.False(Directory.Exists(enginePath));
    }
}
