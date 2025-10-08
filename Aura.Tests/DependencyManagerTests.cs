using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aura.Tests;

public class DependencyManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _manifestPath;
    private readonly Mock<ILogger<DependencyManager>> _mockLogger;
    private readonly DependencyManager _dependencyManager;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public DependencyManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "AuraTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        _manifestPath = Path.Combine(_testDirectory, "manifest.json");
        _mockLogger = new Mock<ILogger<DependencyManager>>();
        
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        
        _dependencyManager = new DependencyManager(
            _mockLogger.Object,
            _httpClient,
            _manifestPath,
            _testDirectory
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task LoadManifestAsync_CreatesDefaultManifest_WhenFileDoesNotExist()
    {
        // Act
        var manifest = await _dependencyManager.LoadManifestAsync();

        // Assert
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.Components);
        Assert.Contains(manifest.Components, c => c.Name == "FFmpeg");
    }

    [Fact]
    public async Task GetComponentStatusAsync_ReturnsNotInstalled_WhenFilesDoNotExist()
    {
        // Arrange
        var manifest = await _dependencyManager.LoadManifestAsync();
        var componentName = manifest.Components[0].Name;

        // Act
        var status = await _dependencyManager.GetComponentStatusAsync(componentName);

        // Assert
        Assert.False(status.IsInstalled);
        Assert.False(status.NeedsRepair);
    }

    [Fact]
    public async Task VerifyChecksumAsync_PassesWithCorrectChecksum()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Test content for checksum";
        await File.WriteAllTextAsync(testFile, content);

        // Calculate expected SHA-256
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var expectedChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        // Create a simple manifest with this file
        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "TestComponent",
                    Version = "1.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "test.txt",
                            Url = "http://example.com/test.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = content.Length
                        }
                    }
                }
            }
        };

        // Save manifest
        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        // Create new manager to load this manifest
        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var status = await manager.GetComponentStatusAsync("TestComponent");

        // Assert
        Assert.True(status.IsInstalled);
        Assert.False(status.NeedsRepair);
    }

    [Fact]
    public async Task VerifyChecksumAsync_FailsWithIncorrectChecksum()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Test content for checksum";
        await File.WriteAllTextAsync(testFile, content);

        var wrongChecksum = "0000000000000000000000000000000000000000000000000000000000000000";

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "TestComponent",
                    Version = "1.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "test.txt",
                            Url = "http://example.com/test.txt",
                            Sha256 = wrongChecksum,
                            SizeBytes = content.Length
                        }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var status = await manager.GetComponentStatusAsync("TestComponent");

        // Assert
        Assert.False(status.IsInstalled);
        Assert.True(status.NeedsRepair);
    }

    [Fact]
    public async Task RepairComponentAsync_RedownloadsCorruptedFiles()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "corrupted.txt");
        var correctContent = "Correct content";
        
        // Write corrupted content
        await File.WriteAllTextAsync(testFile, "Corrupted content");

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(correctContent));
        var expectedChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "TestComponent",
                    Version = "1.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "corrupted.txt",
                            Url = "http://example.com/corrupted.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = correctContent.Length
                        }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        // Setup mock HTTP response with correct content
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(correctContent)
            });

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var progress = new Progress<DownloadProgress>();
        await manager.RepairComponentAsync("TestComponent", progress, CancellationToken.None);

        // Assert
        var repairedContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(correctContent, repairedContent);
    }

    [Fact]
    public async Task RemoveComponentAsync_DeletesAllFiles()
    {
        // Arrange
        var testFile1 = Path.Combine(_testDirectory, "file1.txt");
        var testFile2 = Path.Combine(_testDirectory, "file2.txt");
        
        await File.WriteAllTextAsync(testFile1, "Content 1");
        await File.WriteAllTextAsync(testFile2, "Content 2");

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "TestComponent",
                    Version = "1.0",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile { Filename = "file1.txt", Url = "http://example.com/file1.txt", Sha256 = "", SizeBytes = 0 },
                        new DependencyFile { Filename = "file2.txt", Url = "http://example.com/file2.txt", Sha256 = "", SizeBytes = 0 }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var success = await manager.RemoveComponentAsync("TestComponent");

        // Assert
        Assert.True(success);
        Assert.False(File.Exists(testFile1));
        Assert.False(File.Exists(testFile2));
    }

    [Fact]
    public async Task GetManualInstallInstructionsAsync_ReturnsCorrectInstructions()
    {
        // Arrange
        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "TestComponent",
                    Version = "2.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "test.exe",
                            Url = "http://example.com/test.exe",
                            Sha256 = "abc123",
                            SizeBytes = 1024,
                            InstallPath = _testDirectory
                        }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var instructions = await manager.GetManualInstallInstructionsAsync("TestComponent");

        // Assert
        Assert.Equal("TestComponent", instructions.ComponentName);
        Assert.Equal("2.0", instructions.Version);
        Assert.Equal(_testDirectory, instructions.TargetDirectory);
        Assert.Single(instructions.Files);
        Assert.Equal("test.exe", instructions.Files[0].Filename);
        Assert.Contains("Download the following files manually", instructions.Instructions);
    }

    [Fact]
    public async Task VerifyManualInstallAsync_ReturnsValidResult_WhenFilesAreCorrect()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "manual.txt");
        var content = "Manual install content";
        await File.WriteAllTextAsync(testFile, content);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var expectedChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "ManualComponent",
                    Version = "1.0",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "manual.txt",
                            Url = "http://example.com/manual.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = content.Length
                        }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var result = await manager.VerifyManualInstallAsync("ManualComponent");

        // Assert
        Assert.True(result.IsValid);
        Assert.Single(result.Files);
        Assert.True(result.Files[0].IsValid);
    }

    [Fact]
    public async Task VerifyManualInstallAsync_ReturnsInvalidResult_WhenFileIsMissing()
    {
        // Arrange
        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "MissingComponent",
                    Version = "1.0",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "missing.txt",
                            Url = "http://example.com/missing.txt",
                            Sha256 = "abc123",
                            SizeBytes = 100
                        }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var result = await manager.VerifyManualInstallAsync("MissingComponent");

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Files);
        Assert.False(result.Files[0].IsValid);
        Assert.Equal("File not found", result.Files[0].ErrorMessage);
    }

    [Fact]
    public async Task DownloadFileAsync_SupportsResume()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "resume.txt");
        var partialContent = "Partial";
        var fullContent = "Partial content";
        var remainingContent = " content";

        // Write partial file
        await File.WriteAllTextAsync(testFile, partialContent);

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "ResumeComponent",
                    Version = "1.0",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "resume.txt",
                            Url = "http://example.com/resume.txt",
                            Sha256 = "",
                            SizeBytes = fullContent.Length
                        }
                    }
                }
            }
        };

        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        // Setup mock HTTP response for resume (206 Partial Content)
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Headers.Range != null && 
                    req.Headers.Range.Ranges.Count > 0 &&
                    req.Headers.Range.Ranges.First().From == partialContent.Length),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.PartialContent,
                Content = new StringContent(remainingContent)
            });

        var manager = new DependencyManager(_mockLogger.Object, _httpClient, _manifestPath, _testDirectory);

        // Act
        var progress = new Progress<DownloadProgress>();
        await manager.DownloadComponentAsync("ResumeComponent", progress, CancellationToken.None);

        // Assert
        var finalContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("Partial", finalContent);
    }

    [Fact]
    public void GetComponentDirectory_ReturnsCorrectPath()
    {
        // Act
        var directory = _dependencyManager.GetComponentDirectory();

        // Assert
        Assert.Equal(_testDirectory, directory);
    }
}
