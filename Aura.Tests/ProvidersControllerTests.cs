using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Aura.Api.Controllers;
using Aura.Core.Configuration;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Services;
using Aura.Core.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aura.Tests;

public class ProvidersControllerTests
{
    private static Mock<ProviderSettings> CreateMockProviderSettings()
    {
        var mockLogger = new NullLogger<ProviderSettings>();
        var mockSettings = new Mock<ProviderSettings>(mockLogger);
        return mockSettings;
    }
    
    private static OpenAIKeyValidationService CreateMockOpenAIValidationService()
    {
        var mockLogger = new NullLogger<OpenAIKeyValidationService>();
        var httpClient = new HttpClient();
        return new OpenAIKeyValidationService(mockLogger, httpClient);
    }
    
    private static Mock<IKeyValidationService> CreateMockKeyValidationService()
    {
        return new Mock<IKeyValidationService>();
    }
    
    private static Mock<ISecureStorageService> CreateMockSecureStorageService()
    {
        return new Mock<ISecureStorageService>();
    }

    [Fact]
    public async Task GetCapabilities_Should_ReturnStableDiffusionUnavailable_WhenNoNvidiaGpu()
    {
        // Arrange
        var mockHardwareDetector = new Mock<IHardwareDetector>();
        var mockKeyStore = new Mock<IKeyStore>();

        var systemProfile = new SystemProfile
        {
            Gpu = new GpuInfo("AMD", "RX 6800", 16, null),
            Tier = HardwareTier.A,
            EnableNVENC = false,
            EnableSD = false
        };

        mockHardwareDetector
            .Setup(x => x.DetectSystemAsync())
            .ReturnsAsync(systemProfile);

        mockKeyStore
            .Setup(x => x.GetKey("STABLE_KEY"))
            .Returns("test-key");

        mockKeyStore
            .Setup(x => x.GetKey("stabilityai"))
            .Returns((string?)null);

        var mockSettings = CreateMockProviderSettings();
        var controller = new ProvidersController(
            mockHardwareDetector.Object, 
            mockKeyStore.Object, 
            mockSettings.Object, 
            CreateMockOpenAIValidationService(),
            CreateMockKeyValidationService().Object,
            CreateMockSecureStorageService().Object);

        // Act
        var result = await controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var capabilities = Assert.IsAssignableFrom<List<ProviderCapability>>(okResult.Value);
        
        var sdCapability = Assert.Single(capabilities, c => c.Name == "StableDiffusion");
        Assert.False(sdCapability.Available);
        Assert.Contains("RequiresNvidiaGPU", sdCapability.ReasonCodes);
    }

    [Fact]
    public async Task GetCapabilities_Should_ReturnStableDiffusionUnavailable_WhenMissingApiKey()
    {
        // Arrange
        var mockHardwareDetector = new Mock<IHardwareDetector>();
        var mockKeyStore = new Mock<IKeyStore>();

        var systemProfile = new SystemProfile
        {
            Gpu = new GpuInfo("NVIDIA", "RTX 3080", 10, "30"),
            Tier = HardwareTier.B,
            EnableNVENC = true,
            EnableSD = true
        };

        mockHardwareDetector
            .Setup(x => x.DetectSystemAsync())
            .ReturnsAsync(systemProfile);

        mockKeyStore
            .Setup(x => x.GetKey(It.IsAny<string>()))
            .Returns((string?)null);

        var mockSettings = CreateMockProviderSettings();
        var controller = new ProvidersController(
            mockHardwareDetector.Object, 
            mockKeyStore.Object, 
            mockSettings.Object, 
            CreateMockOpenAIValidationService(),
            CreateMockKeyValidationService().Object,
            CreateMockSecureStorageService().Object);

        // Act
        var result = await controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var capabilities = Assert.IsAssignableFrom<List<ProviderCapability>>(okResult.Value);
        
        var sdCapability = Assert.Single(capabilities, c => c.Name == "StableDiffusion");
        Assert.False(sdCapability.Available);
        Assert.Contains("MissingApiKey:STABLE_KEY", sdCapability.ReasonCodes);
    }

    [Fact]
    public async Task GetCapabilities_Should_ReturnStableDiffusionUnavailable_WhenInsufficientVRAM()
    {
        // Arrange
        var mockHardwareDetector = new Mock<IHardwareDetector>();
        var mockKeyStore = new Mock<IKeyStore>();

        var systemProfile = new SystemProfile
        {
            Gpu = new GpuInfo("NVIDIA", "GTX 1050", 4, "10"),
            Tier = HardwareTier.D,
            EnableNVENC = true,
            EnableSD = false
        };

        mockHardwareDetector
            .Setup(x => x.DetectSystemAsync())
            .ReturnsAsync(systemProfile);

        mockKeyStore
            .Setup(x => x.GetKey("STABLE_KEY"))
            .Returns("test-key");

        var mockSettings = CreateMockProviderSettings();
        var controller = new ProvidersController(
            mockHardwareDetector.Object, 
            mockKeyStore.Object, 
            mockSettings.Object, 
            CreateMockOpenAIValidationService(),
            CreateMockKeyValidationService().Object,
            CreateMockSecureStorageService().Object);

        // Act
        var result = await controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var capabilities = Assert.IsAssignableFrom<List<ProviderCapability>>(okResult.Value);
        
        var sdCapability = Assert.Single(capabilities, c => c.Name == "StableDiffusion");
        Assert.False(sdCapability.Available);
        Assert.Contains("InsufficientVRAM", sdCapability.ReasonCodes);
    }

    [Fact]
    public async Task GetCapabilities_Should_ReturnStableDiffusionAvailable_WhenAllRequirementsMet()
    {
        // Arrange
        var mockHardwareDetector = new Mock<IHardwareDetector>();
        var mockKeyStore = new Mock<IKeyStore>();

        var systemProfile = new SystemProfile
        {
            Gpu = new GpuInfo("NVIDIA", "RTX 4090", 24, "40"),
            Tier = HardwareTier.A,
            EnableNVENC = true,
            EnableSD = true
        };

        mockHardwareDetector
            .Setup(x => x.DetectSystemAsync())
            .ReturnsAsync(systemProfile);

        mockKeyStore
            .Setup(x => x.GetKey("STABLE_KEY"))
            .Returns("test-key-12345");

        var mockSettings = CreateMockProviderSettings();
        var controller = new ProvidersController(
            mockHardwareDetector.Object, 
            mockKeyStore.Object, 
            mockSettings.Object, 
            CreateMockOpenAIValidationService(),
            CreateMockKeyValidationService().Object,
            CreateMockSecureStorageService().Object);

        // Act
        var result = await controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var capabilities = Assert.IsAssignableFrom<List<ProviderCapability>>(okResult.Value);
        
        var sdCapability = Assert.Single(capabilities, c => c.Name == "StableDiffusion");
        Assert.True(sdCapability.Available);
        Assert.Empty(sdCapability.ReasonCodes);
    }

    [Fact]
    public async Task GetCapabilities_Should_ReturnRequirements()
    {
        // Arrange
        var mockHardwareDetector = new Mock<IHardwareDetector>();
        var mockKeyStore = new Mock<IKeyStore>();

        var systemProfile = new SystemProfile
        {
            Gpu = new GpuInfo("NVIDIA", "RTX 3080", 10, "30"),
            Tier = HardwareTier.B
        };

        mockHardwareDetector
            .Setup(x => x.DetectSystemAsync())
            .ReturnsAsync(systemProfile);

        mockKeyStore
            .Setup(x => x.GetKey(It.IsAny<string>()))
            .Returns((string?)null);

        var mockSettings = CreateMockProviderSettings();
        var controller = new ProvidersController(
            mockHardwareDetector.Object, 
            mockKeyStore.Object, 
            mockSettings.Object, 
            CreateMockOpenAIValidationService(),
            CreateMockKeyValidationService().Object,
            CreateMockSecureStorageService().Object);

        // Act
        var result = await controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var capabilities = Assert.IsAssignableFrom<List<ProviderCapability>>(okResult.Value);
        
        var sdCapability = Assert.Single(capabilities, c => c.Name == "StableDiffusion");
        Assert.NotNull(sdCapability.Requirements);
        Assert.Contains("STABLE_KEY", sdCapability.Requirements.NeedsKey);
        Assert.Equal("nvidia", sdCapability.Requirements.NeedsGPU);
        Assert.Equal(6144, sdCapability.Requirements.MinVRAMMB);
        Assert.Contains("windows", sdCapability.Requirements.Os);
        Assert.Contains("linux", sdCapability.Requirements.Os);
    }

    [Fact]
    public async Task GetCapabilities_Should_ReturnMultipleReasonCodes_WhenMultipleRequirementsUnmet()
    {
        // Arrange
        var mockHardwareDetector = new Mock<IHardwareDetector>();
        var mockKeyStore = new Mock<IKeyStore>();

        var systemProfile = new SystemProfile
        {
            Gpu = new GpuInfo("AMD", "RX 5700", 4, null),
            Tier = HardwareTier.D,
            EnableNVENC = false,
            EnableSD = false
        };

        mockHardwareDetector
            .Setup(x => x.DetectSystemAsync())
            .ReturnsAsync(systemProfile);

        mockKeyStore
            .Setup(x => x.GetKey(It.IsAny<string>()))
            .Returns((string?)null);

        var mockSettings = CreateMockProviderSettings();
        var controller = new ProvidersController(
            mockHardwareDetector.Object, 
            mockKeyStore.Object, 
            mockSettings.Object, 
            CreateMockOpenAIValidationService(),
            CreateMockKeyValidationService().Object,
            CreateMockSecureStorageService().Object);

        // Act
        var result = await controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var capabilities = Assert.IsAssignableFrom<List<ProviderCapability>>(okResult.Value);
        
        var sdCapability = Assert.Single(capabilities, c => c.Name == "StableDiffusion");
        Assert.False(sdCapability.Available);
        Assert.Contains("RequiresNvidiaGPU", sdCapability.ReasonCodes);
        Assert.Contains("MissingApiKey:STABLE_KEY", sdCapability.ReasonCodes);
        Assert.Contains("InsufficientVRAM", sdCapability.ReasonCodes);
    }
}
