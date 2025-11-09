using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aura.Core.Services.Assets;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Assets;

public class SampleAssetsServiceTests : IDisposable
{
    private readonly Mock<ILogger<SampleAssetsService>> _loggerMock;
    private readonly Mock<ILogger<AssetLibraryService>> _libraryLoggerMock;
    private readonly Mock<ILogger<ThumbnailGenerator>> _thumbnailLoggerMock;
    private readonly string _testSamplesPath;
    private readonly string _testLibraryPath;

    public SampleAssetsServiceTests()
    {
        _loggerMock = new Mock<ILogger<SampleAssetsService>>();
        _libraryLoggerMock = new Mock<ILogger<AssetLibraryService>>();
        _thumbnailLoggerMock = new Mock<ILogger<ThumbnailGenerator>>();
        
        _testSamplesPath = Path.Combine(Path.GetTempPath(), "aura-test-samples-" + Guid.NewGuid());
        _testLibraryPath = Path.Combine(Path.GetTempPath(), "aura-test-library-" + Guid.NewGuid());
        
        Directory.CreateDirectory(_testSamplesPath);
        Directory.CreateDirectory(_testLibraryPath);
    }

    [Fact]
    public async Task GetBriefTemplatesAsync_WhenNoTemplatesFile_ReturnsEmptyList()
    {
        // Arrange
        var thumbnailGenerator = new ThumbnailGenerator(_thumbnailLoggerMock.Object);
        var assetLibrary = new AssetLibraryService(_libraryLoggerMock.Object, _testLibraryPath, thumbnailGenerator);
        var sampleAssets = new SampleAssetsService(_loggerMock.Object, _testSamplesPath, assetLibrary);

        // Act
        var templates = await sampleAssets.GetBriefTemplatesAsync();

        // Assert
        Assert.NotNull(templates);
        Assert.Empty(templates);
    }

    [Fact]
    public async Task GetBriefTemplatesAsync_WhenTemplatesFileExists_ReturnsTemplates()
    {
        // Arrange
        var templatesDir = Path.Combine(_testSamplesPath, "Templates");
        Directory.CreateDirectory(templatesDir);

        var templatesFile = Path.Combine(templatesDir, "brief-templates.json");
        var testData = @"{
  ""version"": ""1.0"",
  ""templates"": [
    {
      ""id"": ""test-template"",
      ""name"": ""Test Template"",
      ""category"": ""Testing"",
      ""description"": ""A test template"",
      ""icon"": ""Lightbulb"",
      ""brief"": {
        ""topic"": ""Test Topic"",
        ""audience"": ""Test Audience"",
        ""goal"": ""Testing"",
        ""tone"": ""Neutral"",
        ""language"": ""English"",
        ""duration"": 30,
        ""keyPoints"": [""Point 1"", ""Point 2""]
      },
      ""settings"": {
        ""aspect"": ""16:9"",
        ""quality"": ""high""
      }
    }
  ]
}";
        await File.WriteAllTextAsync(templatesFile, testData);

        var thumbnailGenerator = new ThumbnailGenerator(_thumbnailLoggerMock.Object);
        var assetLibrary = new AssetLibraryService(_libraryLoggerMock.Object, _testLibraryPath, thumbnailGenerator);
        var sampleAssets = new SampleAssetsService(_loggerMock.Object, _testSamplesPath, assetLibrary);

        // Act
        var templates = await sampleAssets.GetBriefTemplatesAsync();

        // Assert
        Assert.NotNull(templates);
        Assert.Single(templates);
        Assert.Equal("test-template", templates[0].Id);
        Assert.Equal("Test Template", templates[0].Name);
        Assert.Equal("Testing", templates[0].Category);
        Assert.Equal("Test Topic", templates[0].Brief.Topic);
    }

    [Fact]
    public async Task GetBriefTemplateAsync_WhenTemplateExists_ReturnsTemplate()
    {
        // Arrange
        var templatesDir = Path.Combine(_testSamplesPath, "Templates");
        Directory.CreateDirectory(templatesDir);

        var templatesFile = Path.Combine(templatesDir, "brief-templates.json");
        var testData = @"{
  ""version"": ""1.0"",
  ""templates"": [
    {
      ""id"": ""template-1"",
      ""name"": ""Template 1"",
      ""category"": ""Testing"",
      ""description"": ""First template"",
      ""icon"": ""Lightbulb"",
      ""brief"": {
        ""topic"": ""Topic 1"",
        ""audience"": ""Audience 1"",
        ""goal"": ""Goal 1"",
        ""tone"": ""Neutral"",
        ""language"": ""English"",
        ""duration"": 30,
        ""keyPoints"": []
      },
      ""settings"": {
        ""aspect"": ""16:9"",
        ""quality"": ""high""
      }
    },
    {
      ""id"": ""template-2"",
      ""name"": ""Template 2"",
      ""category"": ""Testing"",
      ""description"": ""Second template"",
      ""icon"": ""BookOpen"",
      ""brief"": {
        ""topic"": ""Topic 2"",
        ""audience"": ""Audience 2"",
        ""goal"": ""Goal 2"",
        ""tone"": ""Professional"",
        ""language"": ""English"",
        ""duration"": 60,
        ""keyPoints"": []
      },
      ""settings"": {
        ""aspect"": ""16:9"",
        ""quality"": ""high""
      }
    }
  ]
}";
        await File.WriteAllTextAsync(templatesFile, testData);

        var thumbnailGenerator = new ThumbnailGenerator(_thumbnailLoggerMock.Object);
        var assetLibrary = new AssetLibraryService(_libraryLoggerMock.Object, _testLibraryPath, thumbnailGenerator);
        var sampleAssets = new SampleAssetsService(_loggerMock.Object, _testSamplesPath, assetLibrary);

        // Act
        var template = await sampleAssets.GetBriefTemplateAsync("template-2");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("template-2", template.Id);
        Assert.Equal("Template 2", template.Name);
        Assert.Equal("Topic 2", template.Brief.Topic);
    }

    [Fact]
    public async Task GetBriefTemplateAsync_WhenTemplateDoesNotExist_ReturnsNull()
    {
        // Arrange
        var templatesDir = Path.Combine(_testSamplesPath, "Templates");
        Directory.CreateDirectory(templatesDir);

        var templatesFile = Path.Combine(templatesDir, "brief-templates.json");
        var testData = @"{""version"": ""1.0"", ""templates"": []}";
        await File.WriteAllTextAsync(templatesFile, testData);

        var thumbnailGenerator = new ThumbnailGenerator(_thumbnailLoggerMock.Object);
        var assetLibrary = new AssetLibraryService(_libraryLoggerMock.Object, _testLibraryPath, thumbnailGenerator);
        var sampleAssets = new SampleAssetsService(_loggerMock.Object, _testSamplesPath, assetLibrary);

        // Act
        var template = await sampleAssets.GetBriefTemplateAsync("nonexistent");

        // Assert
        Assert.Null(template);
    }

    [Fact]
    public async Task GetVoiceConfigurationsAsync_WhenNoConfigFile_ReturnsEmptyList()
    {
        // Arrange
        var thumbnailGenerator = new ThumbnailGenerator(_thumbnailLoggerMock.Object);
        var assetLibrary = new AssetLibraryService(_libraryLoggerMock.Object, _testLibraryPath, thumbnailGenerator);
        var sampleAssets = new SampleAssetsService(_loggerMock.Object, _testSamplesPath, assetLibrary);

        // Act
        var configs = await sampleAssets.GetVoiceConfigurationsAsync();

        // Assert
        Assert.NotNull(configs);
        Assert.Empty(configs);
    }

    [Fact]
    public async Task GetVoiceConfigurationsByProviderAsync_FiltersCorrectly()
    {
        // Arrange
        var templatesDir = Path.Combine(_testSamplesPath, "Templates");
        Directory.CreateDirectory(templatesDir);

        var configFile = Path.Combine(templatesDir, "voice-configs.json");
        var testData = @"{
  ""version"": ""1.0"",
  ""configurations"": [
    {
      ""provider"": ""ElevenLabs"",
      ""name"": ""Voice 1"",
      ""description"": ""Test voice"",
      ""voiceId"": ""voice1"",
      ""settings"": {},
      ""sampleText"": ""Test"",
      ""tags"": []
    },
    {
      ""provider"": ""PlayHT"",
      ""name"": ""Voice 2"",
      ""description"": ""Test voice 2"",
      ""voiceId"": ""voice2"",
      ""settings"": {},
      ""sampleText"": ""Test"",
      ""tags"": []
    },
    {
      ""provider"": ""ElevenLabs"",
      ""name"": ""Voice 3"",
      ""description"": ""Test voice 3"",
      ""voiceId"": ""voice3"",
      ""settings"": {},
      ""sampleText"": ""Test"",
      ""tags"": []
    }
  ]
}";
        await File.WriteAllTextAsync(configFile, testData);

        var thumbnailGenerator = new ThumbnailGenerator(_thumbnailLoggerMock.Object);
        var assetLibrary = new AssetLibraryService(_libraryLoggerMock.Object, _testLibraryPath, thumbnailGenerator);
        var sampleAssets = new SampleAssetsService(_loggerMock.Object, _testSamplesPath, assetLibrary);

        // Act
        var allConfigs = await sampleAssets.GetVoiceConfigurationsAsync();
        var elevenLabsConfigs = await sampleAssets.GetVoiceConfigurationsByProviderAsync("ElevenLabs");
        var playHTConfigs = await sampleAssets.GetVoiceConfigurationsByProviderAsync("PlayHT");

        // Assert
        Assert.Equal(3, allConfigs.Count);
        Assert.Equal(2, elevenLabsConfigs.Count);
        Assert.Single(playHTConfigs);
        Assert.All(elevenLabsConfigs, c => Assert.Equal("ElevenLabs", c.Provider));
        Assert.All(playHTConfigs, c => Assert.Equal("PlayHT", c.Provider));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSamplesPath))
                Directory.Delete(_testSamplesPath, true);
            if (Directory.Exists(_testLibraryPath))
                Directory.Delete(_testLibraryPath, true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
