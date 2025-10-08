using Xunit;
using System.Text.Json;
using Aura.Core.Models;
using Aura.Api.Serialization;

namespace Aura.Tests;

/// <summary>
/// Integration tests for script/plan request DTOs with enum aliases
/// </summary>
public class EnumAliasIntegrationTests
{
    private readonly JsonSerializerOptions _options;

    public EnumAliasIntegrationTests()
    {
        _options = new JsonSerializerOptions
        {
            Converters = { 
                new TolerantDensityConverter(), 
                new TolerantAspectConverter(),
                new System.Text.Json.Serialization.JsonStringEnumConverter()
            },
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public void ScriptRequest_Should_DeserializeWithCanonicalEnums()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""audience"": ""Beginners"",
            ""goal"": ""Educational"",
            ""tone"": ""Informative"",
            ""language"": ""en-US"",
            ""aspect"": ""Widescreen16x9"",
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Balanced"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<ScriptRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("AI Basics", dto.Topic);
        Assert.Equal(Aspect.Widescreen16x9, dto.Aspect);
        Assert.Equal(Density.Balanced, dto.Density);
    }

    [Fact]
    public void ScriptRequest_Should_DeserializeWithDensityAlias_Normal()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""audience"": ""Beginners"",
            ""goal"": ""Educational"",
            ""tone"": ""Informative"",
            ""language"": ""en-US"",
            ""aspect"": ""Widescreen16x9"",
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Normal"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<ScriptRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(Density.Balanced, dto.Density); // Normal -> Balanced
    }

    [Fact]
    public void ScriptRequest_Should_DeserializeWithAspectAlias_16x9()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""audience"": ""Beginners"",
            ""goal"": ""Educational"",
            ""tone"": ""Informative"",
            ""language"": ""en-US"",
            ""aspect"": ""16:9"",
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Balanced"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<ScriptRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(Aspect.Widescreen16x9, dto.Aspect); // 16:9 -> Widescreen16x9
    }

    [Fact]
    public void ScriptRequest_Should_DeserializeWithAspectAlias_9x16()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""audience"": ""Beginners"",
            ""goal"": ""Educational"",
            ""tone"": ""Informative"",
            ""language"": ""en-US"",
            ""aspect"": ""9:16"",
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Balanced"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<ScriptRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(Aspect.Vertical9x16, dto.Aspect); // 9:16 -> Vertical9x16
    }

    [Fact]
    public void ScriptRequest_Should_DeserializeWithAspectAlias_1x1()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""audience"": ""Beginners"",
            ""goal"": ""Educational"",
            ""tone"": ""Informative"",
            ""language"": ""en-US"",
            ""aspect"": ""1:1"",
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Balanced"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<ScriptRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(Aspect.Square1x1, dto.Aspect); // 1:1 -> Square1x1
    }

    [Fact]
    public void ScriptRequest_Should_DeserializeWithAllAliases()
    {
        // Arrange - using both Normal and 16:9 aliases
        var json = @"{
            ""topic"": ""AI Basics"",
            ""audience"": ""Beginners"",
            ""goal"": ""Educational"",
            ""tone"": ""Informative"",
            ""language"": ""en-US"",
            ""aspect"": ""16:9"",
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Normal"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<ScriptRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(Aspect.Widescreen16x9, dto.Aspect);
        Assert.Equal(Density.Balanced, dto.Density);
    }

    [Fact]
    public void ScriptRequest_Should_ThrowOnInvalidDensity()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""density"": ""InvalidDensity""
        }";

        // Act & Assert
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ScriptRequestDto>(json, _options));
        Assert.Contains("Invalid Density value", ex.Message);
    }

    [Fact]
    public void ScriptRequest_Should_ThrowOnInvalidAspect()
    {
        // Arrange
        var json = @"{
            ""topic"": ""AI Basics"",
            ""aspect"": ""InvalidAspect""
        }";

        // Act & Assert
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ScriptRequestDto>(json, _options));
        Assert.Contains("Invalid Aspect value", ex.Message);
    }

    [Fact]
    public void PlanRequest_Should_DeserializeWithDensityAlias()
    {
        // Arrange
        var json = @"{
            ""targetDurationMinutes"": 5.0,
            ""pacing"": ""Conversational"",
            ""density"": ""Normal"",
            ""style"": ""Standard""
        }";

        // Act
        var dto = JsonSerializer.Deserialize<PlanRequestDto>(json, _options);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(Density.Balanced, dto.Density);
    }

    // DTO classes for testing (matching Program.cs DTOs)
    private record ScriptRequestDto(
        string Topic,
        string Audience,
        string Goal,
        string Tone,
        string Language,
        Aspect Aspect,
        double TargetDurationMinutes,
        Pacing Pacing,
        Density Density,
        string Style);

    private record PlanRequestDto(
        double TargetDurationMinutes,
        Pacing Pacing,
        Density Density,
        string Style);
}
