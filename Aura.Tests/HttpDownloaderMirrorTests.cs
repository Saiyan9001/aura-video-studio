using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Downloads;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aura.Tests;

public class HttpDownloaderMirrorTests : IDisposable
{
    private readonly ILogger<HttpDownloader> _logger;
    private readonly string _testDirectory;

    public HttpDownloaderMirrorTests()
    {
        _logger = NullLogger<HttpDownloader>.Instance;
        _testDirectory = Path.Combine(Path.GetTempPath(), "aura-mirror-tests-" + Guid.NewGuid().ToString());
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
    public async Task DownloadFileWithMirrorsAsync_Should_FallbackToSecondMirror_When_FirstReturns404()
    {
        // Arrange
        var testContent = new byte[1024];
        new Random().NextBytes(testContent);

        var handlerMock = new Mock<HttpMessageHandler>();
        
        // First mirror returns 404
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("mirror1")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Second mirror succeeds
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("mirror2")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(testContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var downloader = new HttpDownloader(_logger, httpClient);
        var outputPath = Path.Combine(_testDirectory, "test-file.bin");

        var urls = new List<string>
        {
            "http://mirror1.com/file.bin",
            "http://mirror2.com/file.bin"
        };

        // Act
        var success = await downloader.DownloadFileWithMirrorsAsync(urls, outputPath);

        // Assert
        Assert.True(success);
        Assert.True(File.Exists(outputPath));
        var downloadedContent = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal(testContent.Length, downloadedContent.Length);
    }

    [Fact]
    public async Task DownloadFileWithMirrorsAsync_Should_TryAllMirrors_WhenAllFail()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        
        // All mirrors return 404
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var downloader = new HttpDownloader(_logger, httpClient);
        var outputPath = Path.Combine(_testDirectory, "test-file.bin");

        var urls = new List<string>
        {
            "http://mirror1.com/file.bin",
            "http://mirror2.com/file.bin",
            "http://mirror3.com/file.bin"
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await downloader.DownloadFileWithMirrorsAsync(urls, outputPath);
        });
    }

    [Fact]
    public async Task DownloadFileWithMirrorsAsync_Should_FailOnChecksumMismatch_AndTryNextMirror()
    {
        // Arrange
        var correctContent = new byte[1024];
        new Random().NextBytes(correctContent);
        var correctHash = Convert.ToHexString(SHA256.HashData(correctContent)).ToLowerInvariant();

        var wrongContent = new byte[1024];
        new Random().NextBytes(wrongContent);

        var handlerMock = new Mock<HttpMessageHandler>();
        
        // First mirror returns wrong content
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("mirror1")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(wrongContent)
            });

        // Second mirror returns correct content
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("mirror2")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(correctContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var downloader = new HttpDownloader(_logger, httpClient);
        var outputPath = Path.Combine(_testDirectory, "test-file.bin");

        var urls = new List<string>
        {
            "http://mirror1.com/file.bin",
            "http://mirror2.com/file.bin"
        };

        // Act
        var success = await downloader.DownloadFileWithMirrorsAsync(urls, outputPath, correctHash);

        // Assert
        Assert.True(success);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ImportLocalFileAsync_Should_CopyFile_Successfully()
    {
        // Arrange
        var testContent = new byte[1024];
        new Random().NextBytes(testContent);
        
        var sourceFile = Path.Combine(_testDirectory, "source.bin");
        await File.WriteAllBytesAsync(sourceFile, testContent);

        var outputPath = Path.Combine(_testDirectory, "output.bin");

        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_logger, httpClient);

        // Act
        var success = await downloader.ImportLocalFileAsync(sourceFile, outputPath);

        // Assert
        Assert.True(success);
        Assert.True(File.Exists(outputPath));
        var copiedContent = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal(testContent, copiedContent);
    }

    [Fact]
    public async Task ImportLocalFileAsync_Should_VerifyChecksum_WhenProvided()
    {
        // Arrange
        var testContent = new byte[1024];
        new Random().NextBytes(testContent);
        var correctHash = Convert.ToHexString(SHA256.HashData(testContent)).ToLowerInvariant();
        
        var sourceFile = Path.Combine(_testDirectory, "source.bin");
        await File.WriteAllBytesAsync(sourceFile, testContent);

        var outputPath = Path.Combine(_testDirectory, "output.bin");

        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_logger, httpClient);

        // Act
        var success = await downloader.ImportLocalFileAsync(sourceFile, outputPath, correctHash);

        // Assert
        Assert.True(success);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ImportLocalFileAsync_Should_FailOnChecksumMismatch()
    {
        // Arrange
        var testContent = new byte[1024];
        new Random().NextBytes(testContent);
        var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";
        
        var sourceFile = Path.Combine(_testDirectory, "source.bin");
        await File.WriteAllBytesAsync(sourceFile, testContent);

        var outputPath = Path.Combine(_testDirectory, "output.bin");

        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_logger, httpClient);

        // Act
        var success = await downloader.ImportLocalFileAsync(sourceFile, outputPath, wrongHash);

        // Assert
        Assert.False(success);
        Assert.True(File.Exists(outputPath)); // File still exists, but verification failed
    }

    [Fact]
    public async Task ImportLocalFileAsync_Should_ThrowException_WhenFileNotFound()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.bin");
        var outputPath = Path.Combine(_testDirectory, "output.bin");

        var httpClient = new HttpClient();
        var downloader = new HttpDownloader(_logger, httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await downloader.ImportLocalFileAsync(nonExistentFile, outputPath);
        });
    }

    [Fact]
    public async Task DownloadFileWithMirrorsAsync_Should_ReportActiveMirror_InProgress()
    {
        // Arrange
        var testContent = new byte[1024];
        new Random().NextBytes(testContent);
        string? reportedMirror = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(testContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var downloader = new HttpDownloader(_logger, httpClient);
        var outputPath = Path.Combine(_testDirectory, "test-file.bin");

        var progress = new Progress<HttpDownloadProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.ActiveMirror))
            {
                reportedMirror = p.ActiveMirror;
            }
        });

        var urls = new List<string>
        {
            "http://mirror1.com/file.bin"
        };

        // Act
        await downloader.DownloadFileWithMirrorsAsync(urls, outputPath, null, progress);

        // Assert
        Assert.NotNull(reportedMirror);
        Assert.Contains("mirror1", reportedMirror);
    }
}
