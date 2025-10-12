using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Downloads;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aura.Tests;

public class ModelInstallerTests : IDisposable
{
    private readonly ILogger<ModelInstaller> _logger;
    private readonly ILogger<HttpDownloader> _downloaderLogger;
    private readonly string _testDirectory;

    public ModelInstallerTests()
    {
        _logger = NullLogger<ModelInstaller>.Instance;
        _downloaderLogger = NullLogger<HttpDownloader>.Instance;
        _testDirectory = Path.Combine(Path.GetTempPath(), "aura-model-installer-tests-" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
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
    }

    [Fact]
    public async Task GetModelsAsync_Should_DiscoverModelsInDefaultDirectory()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        // Create a test model file in default SD directory
        var sdDir = Path.Combine(_testDirectory, "stable-diffusion-webui", "models", "Stable-diffusion");
        Directory.CreateDirectory(sdDir);
        var testModelPath = Path.Combine(sdDir, "test-model.safetensors");
        await File.WriteAllTextAsync(testModelPath, "test model content");

        // Act
        var models = await installer.GetModelsAsync(ModelKind.SD_BASE);

        // Assert
        Assert.Single(models);
        Assert.Equal("test-model", models[0].Id);
        Assert.Equal("test-model", models[0].Name);
        Assert.Equal(ModelKind.SD_BASE, models[0].Kind);
        Assert.False(models[0].IsExternal);
        Assert.Equal(testModelPath, models[0].FilePath);
    }

    [Fact]
    public async Task GetModelsAsync_Should_DiscoverPiperVoices()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        // Create test Piper voice files
        var piperDir = Path.Combine(_testDirectory, "piper", "voices");
        Directory.CreateDirectory(piperDir);
        var voice1Path = Path.Combine(piperDir, "en_US-lessac-medium.onnx");
        var voice2Path = Path.Combine(piperDir, "en_US-amy-medium.onnx");
        await File.WriteAllTextAsync(voice1Path, "voice1 content");
        await File.WriteAllTextAsync(voice2Path, "voice2 content");

        // Act
        var voices = await installer.GetModelsAsync(ModelKind.PIPER_VOICE);

        // Assert
        Assert.Equal(2, voices.Count);
        Assert.Contains(voices, v => v.Id == "en_US-lessac-medium");
        Assert.Contains(voices, v => v.Id == "en_US-amy-medium");
        Assert.All(voices, v => Assert.Equal(ModelKind.PIPER_VOICE, v.Kind));
        Assert.All(voices, v => Assert.False(v.IsExternal));
    }

    [Fact]
    public async Task AddExternalDirectoryAsync_Should_IndexExternalModels()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        // Create external directory with models
        var externalDir = Path.Combine(_testDirectory, "external-models");
        Directory.CreateDirectory(externalDir);
        var model1Path = Path.Combine(externalDir, "custom-model-1.safetensors");
        var model2Path = Path.Combine(externalDir, "custom-model-2.ckpt");
        await File.WriteAllTextAsync(model1Path, "model1 content");
        await File.WriteAllTextAsync(model2Path, "model2 content");

        // Act
        var discoveredModels = await installer.AddExternalDirectoryAsync(ModelKind.SD_BASE, externalDir);

        // Assert
        Assert.Equal(2, discoveredModels.Count);
        Assert.Contains(discoveredModels, m => m.Id == "custom-model-1");
        Assert.Contains(discoveredModels, m => m.Id == "custom-model-2");
        Assert.All(discoveredModels, m => Assert.True(m.IsExternal));
        Assert.All(discoveredModels, m => Assert.Contains("External:", m.Provenance));
    }

    [Fact]
    public async Task AddExternalDirectoryAsync_Should_ThrowException_WhenDirectoryNotExists()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);
        var nonExistentDir = Path.Combine(_testDirectory, "non-existent");

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            await installer.AddExternalDirectoryAsync(ModelKind.SD_BASE, nonExistentDir));
    }

    [Fact]
    public async Task GetModelsAsync_Should_ReturnBothDefaultAndExternalModels()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        // Create default model
        var sdDir = Path.Combine(_testDirectory, "stable-diffusion-webui", "models", "Stable-diffusion");
        Directory.CreateDirectory(sdDir);
        await File.WriteAllTextAsync(Path.Combine(sdDir, "default-model.safetensors"), "default");

        // Add external directory with model
        var externalDir = Path.Combine(_testDirectory, "external");
        Directory.CreateDirectory(externalDir);
        await File.WriteAllTextAsync(Path.Combine(externalDir, "external-model.safetensors"), "external");
        await installer.AddExternalDirectoryAsync(ModelKind.SD_BASE, externalDir);

        // Act
        var models = await installer.GetModelsAsync(ModelKind.SD_BASE);

        // Assert
        Assert.Equal(2, models.Count);
        Assert.Single(models.Where(m => !m.IsExternal));
        Assert.Single(models.Where(m => m.IsExternal));
    }

    [Fact]
    public async Task RemoveAsync_Should_DeleteModelFile()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var modelPath = Path.Combine(_testDirectory, "test-model.safetensors");
        await File.WriteAllTextAsync(modelPath, "test content");

        // Act
        await installer.RemoveAsync("test-model", modelPath);

        // Assert
        Assert.False(File.Exists(modelPath));
    }

    [Fact]
    public async Task RemoveAsync_Should_ThrowException_WhenModelInReadOnlyExternalDirectory()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var externalDir = Path.Combine(_testDirectory, "readonly-external");
        Directory.CreateDirectory(externalDir);
        var modelPath = Path.Combine(externalDir, "readonly-model.safetensors");
        await File.WriteAllTextAsync(modelPath, "readonly content");
        await installer.AddExternalDirectoryAsync(ModelKind.SD_BASE, externalDir, isReadOnly: true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await installer.RemoveAsync("readonly-model", modelPath));
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnValid_WhenChecksumMatches()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var testContent = "test content";
        var modelPath = Path.Combine(_testDirectory, "test-model.safetensors");
        await File.WriteAllTextAsync(modelPath, testContent);

        // Calculate expected checksum
        var expectedSha256 = BitConverter.ToString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(testContent))
        ).Replace("-", "").ToLowerInvariant();

        // Act
        var result = await installer.VerifyAsync(modelPath, expectedSha256);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Valid", result.Status);
        Assert.Equal(expectedSha256, result.ExpectedSha256);
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnInvalid_WhenChecksumDoesNotMatch()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var modelPath = Path.Combine(_testDirectory, "test-model.safetensors");
        await File.WriteAllTextAsync(modelPath, "test content");
        var wrongChecksum = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = await installer.VerifyAsync(modelPath, wrongChecksum);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Checksum mismatch", result.Status);
        Assert.Contains("Checksum does not match expected value", result.Issues);
    }

    [Fact]
    public async Task VerifyAsync_Should_ReturnUnknownChecksum_WhenNoChecksumProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var modelPath = Path.Combine(_testDirectory, "test-model.safetensors");
        await File.WriteAllTextAsync(modelPath, "test content");

        // Act
        var result = await installer.VerifyAsync(modelPath, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Unknown checksum (user-supplied)", result.Status);
    }

    [Fact]
    public void GetExternalDirectories_Should_ReturnConfiguredDirectories()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var externalDir1 = Path.Combine(_testDirectory, "external1");
        var externalDir2 = Path.Combine(_testDirectory, "external2");
        Directory.CreateDirectory(externalDir1);
        Directory.CreateDirectory(externalDir2);

        // Act
        installer.AddExternalDirectoryAsync(ModelKind.SD_BASE, externalDir1).GetAwaiter().GetResult();
        installer.AddExternalDirectoryAsync(ModelKind.PIPER_VOICE, externalDir2, isReadOnly: false).GetAwaiter().GetResult();
        var directories = installer.GetExternalDirectories();

        // Assert
        Assert.Equal(2, directories.Count);
        Assert.Contains(directories, d => d.Path == externalDir1 && d.Kind == ModelKind.SD_BASE && d.IsReadOnly);
        Assert.Contains(directories, d => d.Path == externalDir2 && d.Kind == ModelKind.PIPER_VOICE && !d.IsReadOnly);
    }

    [Fact]
    public void RemoveExternalDirectory_Should_RemoveConfiguration()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        var externalDir = Path.Combine(_testDirectory, "external");
        Directory.CreateDirectory(externalDir);
        installer.AddExternalDirectoryAsync(ModelKind.SD_BASE, externalDir).GetAwaiter().GetResult();

        // Act
        installer.RemoveExternalDirectory(externalDir);
        var directories = installer.GetExternalDirectories();

        // Assert
        Assert.Empty(directories);
    }

    [Fact]
    public async Task GetModelsAsync_Should_OnlyReturnModelsOfSpecifiedKind()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_downloaderLogger, httpClient);
        var installer = new ModelInstaller(_logger, downloader, _testDirectory);

        // Create VAE and LoRA models
        var vaeDir = Path.Combine(_testDirectory, "stable-diffusion-webui", "models", "VAE");
        var loraDir = Path.Combine(_testDirectory, "stable-diffusion-webui", "models", "Lora");
        Directory.CreateDirectory(vaeDir);
        Directory.CreateDirectory(loraDir);
        await File.WriteAllTextAsync(Path.Combine(vaeDir, "vae-model.safetensors"), "vae");
        await File.WriteAllTextAsync(Path.Combine(loraDir, "lora-model.safetensors"), "lora");

        // Act
        var vaeModels = await installer.GetModelsAsync(ModelKind.VAE);
        var loraModels = await installer.GetModelsAsync(ModelKind.LORA);

        // Assert
        Assert.Single(vaeModels);
        Assert.Equal(ModelKind.VAE, vaeModels[0].Kind);
        Assert.Single(loraModels);
        Assert.Equal(ModelKind.LORA, loraModels[0].Kind);
    }
}
