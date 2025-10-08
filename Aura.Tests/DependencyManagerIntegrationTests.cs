using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Integration tests for manifest-driven download flow with a local stub HTTP server
/// </summary>
public class DependencyManagerIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _manifestPath;
    private readonly Mock<ILogger<DependencyManager>> _mockLogger;
    private readonly HttpListener _httpListener;
    private readonly int _testPort = 8899;
    private readonly string _testBaseUrl;
    private bool _serverRunning;

    public DependencyManagerIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "AuraIntegrationTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        _manifestPath = Path.Combine(_testDirectory, "manifest.json");
        _mockLogger = new Mock<ILogger<DependencyManager>>();
        
        _testBaseUrl = $"http://localhost:{_testPort}/";
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add(_testBaseUrl);
    }

    public void Dispose()
    {
        StopTestServer();
        
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

        _httpListener?.Close();
    }

    private void StartTestServer()
    {
        if (_serverRunning) return;
        
        _httpListener.Start();
        _serverRunning = true;
        
        Task.Run(async () =>
        {
            while (_serverRunning && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    await HandleRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Server stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Test server error: {ex.Message}");
                }
            }
        });
    }

    private void StopTestServer()
    {
        _serverRunning = false;
        if (_httpListener.IsListening)
        {
            _httpListener.Stop();
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.Url?.LocalPath == "/test-file.txt")
            {
                var content = "Test file content from local server";
                var buffer = Encoding.UTF8.GetBytes(content);

                // Support range requests for resume testing
                if (request.Headers["Range"] != null)
                {
                    var rangeHeader = request.Headers["Range"];
                    if (rangeHeader.StartsWith("bytes="))
                    {
                        var rangeParts = rangeHeader.Substring(6).Split('-');
                        if (long.TryParse(rangeParts[0], out long start))
                        {
                            response.StatusCode = 206; // Partial Content
                            response.Headers.Add("Content-Range", $"bytes {start}-{buffer.Length - 1}/{buffer.Length}");
                            
                            var partialBuffer = new byte[buffer.Length - start];
                            Array.Copy(buffer, start, partialBuffer, 0, partialBuffer.Length);
                            buffer = partialBuffer;
                        }
                    }
                }
                else
                {
                    response.StatusCode = 200;
                }

                response.ContentType = "text/plain";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else if (request.Url?.LocalPath == "/test-binary.bin")
            {
                // Generate a small binary file for testing
                var buffer = new byte[1024];
                new Random(42).NextBytes(buffer); // Use seed for reproducibility

                response.StatusCode = 200;
                response.ContentType = "application/octet-stream";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else if (request.Url?.LocalPath == "/large-file.bin")
            {
                // Simulate a larger file for progress testing
                var buffer = new byte[10240]; // 10KB
                new Random(123).NextBytes(buffer);

                response.StatusCode = 200;
                response.ContentType = "application/octet-stream";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private string ComputeSha256(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private string ComputeSha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    [Fact]
    public async Task IntegrationTest_FullDownloadFlow_WithStubServer()
    {
        // Arrange
        StartTestServer();

        var testContent = "Test file content from local server";
        var expectedChecksum = ComputeSha256(testContent);

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "TestComponent",
                    Version = "1.0.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "test-file.txt",
                            Url = $"{_testBaseUrl}test-file.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = testContent.Length
                        }
                    }
                }
            }
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        using var httpClient = new HttpClient();
        var manager = new DependencyManager(_mockLogger.Object, httpClient, _manifestPath, _testDirectory);

        // Act - Download
        var progress = new Progress<DownloadProgress>(p =>
        {
            Assert.True(p.PercentComplete >= 0 && p.PercentComplete <= 100);
        });

        await manager.DownloadComponentAsync("TestComponent", progress, CancellationToken.None);

        // Assert - File downloaded and verified
        var downloadedFile = Path.Combine(_testDirectory, "test-file.txt");
        Assert.True(File.Exists(downloadedFile));

        var downloadedContent = await File.ReadAllTextAsync(downloadedFile);
        Assert.Equal(testContent, downloadedContent);

        var status = await manager.GetComponentStatusAsync("TestComponent");
        Assert.True(status.IsInstalled);
        Assert.False(status.NeedsRepair);
    }

    [Fact]
    public async Task IntegrationTest_ResumeDownload_WithStubServer()
    {
        // Arrange
        StartTestServer();

        var testContent = "Test file content from local server";
        var expectedChecksum = ComputeSha256(testContent);
        var partialContent = testContent.Substring(0, 10);

        // Create partial file
        var partialFile = Path.Combine(_testDirectory, "test-file.txt");
        await File.WriteAllTextAsync(partialFile, partialContent);

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "ResumeTest",
                    Version = "1.0.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "test-file.txt",
                            Url = $"{_testBaseUrl}test-file.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = testContent.Length
                        }
                    }
                }
            }
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        using var httpClient = new HttpClient();
        var manager = new DependencyManager(_mockLogger.Object, httpClient, _manifestPath, _testDirectory);

        // Act - Resume download
        var progress = new Progress<DownloadProgress>();
        await manager.DownloadComponentAsync("ResumeTest", progress, CancellationToken.None);

        // Assert - File completed and verified
        var downloadedContent = await File.ReadAllTextAsync(partialFile);
        Assert.Equal(testContent, downloadedContent);
    }

    [Fact]
    public async Task IntegrationTest_RepairComponent_WithStubServer()
    {
        // Arrange
        StartTestServer();

        var testContent = "Test file content from local server";
        var expectedChecksum = ComputeSha256(testContent);
        var corruptedContent = "Corrupted content";

        // Create corrupted file
        var corruptedFile = Path.Combine(_testDirectory, "test-file.txt");
        await File.WriteAllTextAsync(corruptedFile, corruptedContent);

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "RepairTest",
                    Version = "1.0.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "test-file.txt",
                            Url = $"{_testBaseUrl}test-file.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = testContent.Length
                        }
                    }
                }
            }
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        using var httpClient = new HttpClient();
        var manager = new DependencyManager(_mockLogger.Object, httpClient, _manifestPath, _testDirectory);

        // Verify component needs repair
        var statusBefore = await manager.GetComponentStatusAsync("RepairTest");
        Assert.True(statusBefore.NeedsRepair);

        // Act - Repair
        var progress = new Progress<DownloadProgress>();
        await manager.RepairComponentAsync("RepairTest", progress, CancellationToken.None);

        // Assert - File repaired and verified
        var repairedContent = await File.ReadAllTextAsync(corruptedFile);
        Assert.Equal(testContent, repairedContent);

        var statusAfter = await manager.GetComponentStatusAsync("RepairTest");
        Assert.True(statusAfter.IsInstalled);
        Assert.False(statusAfter.NeedsRepair);
    }

    [Fact]
    public async Task IntegrationTest_ProgressReporting_WithStubServer()
    {
        // Arrange
        StartTestServer();

        var buffer = new byte[10240]; // 10KB
        new Random(123).NextBytes(buffer);
        var expectedChecksum = ComputeSha256(buffer);

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "ProgressTest",
                    Version = "1.0.0",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "large-file.bin",
                            Url = $"{_testBaseUrl}large-file.bin",
                            Sha256 = expectedChecksum,
                            SizeBytes = buffer.Length
                        }
                    }
                }
            }
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        using var httpClient = new HttpClient();
        var manager = new DependencyManager(_mockLogger.Object, httpClient, _manifestPath, _testDirectory);

        // Act - Download with progress tracking
        var progressReports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p =>
        {
            progressReports.Add(p);
        });

        await manager.DownloadComponentAsync("ProgressTest", progress, CancellationToken.None);

        // Assert - Progress was reported
        Assert.NotEmpty(progressReports);
        
        // Verify progress increased monotonically
        for (int i = 1; i < progressReports.Count; i++)
        {
            Assert.True(progressReports[i].BytesDownloaded >= progressReports[i - 1].BytesDownloaded);
        }

        // Verify final progress is 100%
        var lastProgress = progressReports[^1];
        Assert.Equal(100, lastProgress.PercentComplete);
        Assert.Equal(buffer.Length, lastProgress.BytesDownloaded);
    }

    [Fact]
    public async Task IntegrationTest_ManualInstallWorkflow_WithVerification()
    {
        // Arrange
        var testContent = "Manually installed content";
        var expectedChecksum = ComputeSha256(testContent);

        var manifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "ManualInstallTest",
                    Version = "1.0.0",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "manual-file.txt",
                            Url = "https://example.com/manual-file.txt",
                            Sha256 = expectedChecksum,
                            SizeBytes = testContent.Length,
                            InstallPath = _testDirectory
                        }
                    }
                }
            }
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_manifestPath, manifestJson);

        using var httpClient = new HttpClient();
        var manager = new DependencyManager(_mockLogger.Object, httpClient, _manifestPath, _testDirectory);

        // Act - Get manual instructions
        var instructions = await manager.GetManualInstallInstructionsAsync("ManualInstallTest");

        // Assert - Instructions are correct
        Assert.Equal("ManualInstallTest", instructions.ComponentName);
        Assert.Equal("1.0.0", instructions.Version);
        Assert.Single(instructions.Files);
        Assert.Contains("Download the following files manually", instructions.Instructions);

        // Simulate manual file placement
        var manualFile = Path.Combine(_testDirectory, "manual-file.txt");
        await File.WriteAllTextAsync(manualFile, testContent);

        // Act - Verify manual install
        var verificationResult = await manager.VerifyManualInstallAsync("ManualInstallTest");

        // Assert - Verification passes
        Assert.True(verificationResult.IsValid);
        Assert.Single(verificationResult.Files);
        Assert.True(verificationResult.Files[0].IsValid);
    }
}
