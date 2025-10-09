using Xunit;
using System.Text.Json;
using Aura.Core.Models;
using Aura.Api.Serialization;

namespace Aura.Tests;

/// <summary>
/// Tests for tolerant enum converters that support canonical names and legacy aliases
/// </summary>
public class EnumConverterTests
{
    private readonly JsonSerializerOptions _options;

    public EnumConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new TolerantDensityConverter());
        _options.Converters.Add(new TolerantAspectConverter());
    }

    #region Density Tests

    [Theory]
    [InlineData("Sparse", Density.Sparse)]
    [InlineData("Balanced", Density.Balanced)]
    [InlineData("Dense", Density.Dense)]
    [InlineData("sparse", Density.Sparse)] // Case insensitive
    [InlineData("BALANCED", Density.Balanced)]
    public void TolerantDensityConverter_Should_ParseCanonicalValues(string json, Density expected)
    {
        // Arrange
        var jsonString = $"\"{json}\"";

        // Act
        var result = JsonSerializer.Deserialize<Density>(jsonString, _options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Normal", Density.Balanced)]
    [InlineData("normal", Density.Balanced)]
    [InlineData("NORMAL", Density.Balanced)]
    public void TolerantDensityConverter_Should_ParseAliases(string json, Density expected)
    {
        // Arrange
        var jsonString = $"\"{json}\"";

        // Act
        var result = JsonSerializer.Deserialize<Density>(jsonString, _options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Medium")]
    [InlineData("")]
    public void TolerantDensityConverter_Should_ThrowForInvalidValues(string json)
    {
        // Arrange
        var jsonString = $"\"{json}\"";

        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<Density>(jsonString, _options));
        
        Assert.Contains("Invalid Density value", ex.Message);
        Assert.Contains("Valid values are: Sparse, Balanced, Dense", ex.Message);
    }

    [Fact]
    public void TolerantDensityConverter_Should_SerializeToCanonicalValue()
    {
        // Arrange
        var density = Density.Balanced;

        // Act
        var json = JsonSerializer.Serialize(density, _options);

        // Assert
        Assert.Equal("\"Balanced\"", json);
    }

    [Fact]
    public void TolerantDensityConverter_Should_RoundTrip()
    {
        // Arrange
        var original = Density.Dense;

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<Density>(json, _options);

        // Assert
        Assert.Equal(original, deserialized);
    }

    #endregion

    #region Aspect Tests

    [Theory]
    [InlineData("Widescreen16x9", Aspect.Widescreen16x9)]
    [InlineData("Vertical9x16", Aspect.Vertical9x16)]
    [InlineData("Square1x1", Aspect.Square1x1)]
    [InlineData("widescreen16x9", Aspect.Widescreen16x9)] // Case insensitive
    [InlineData("VERTICAL9X16", Aspect.Vertical9x16)]
    public void TolerantAspectConverter_Should_ParseCanonicalValues(string json, Aspect expected)
    {
        // Arrange
        var jsonString = $"\"{json}\"";

        // Act
        var result = JsonSerializer.Deserialize<Aspect>(jsonString, _options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("16:9", Aspect.Widescreen16x9)]
    [InlineData("9:16", Aspect.Vertical9x16)]
    [InlineData("1:1", Aspect.Square1x1)]
    public void TolerantAspectConverter_Should_ParseAliases(string json, Aspect expected)
    {
        // Arrange
        var jsonString = $"\"{json}\"";

        // Act
        var result = JsonSerializer.Deserialize<Aspect>(jsonString, _options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("4:3")]
    [InlineData("")]
    public void TolerantAspectConverter_Should_ThrowForInvalidValues(string json)
    {
        // Arrange
        var jsonString = $"\"{json}\"";

        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<Aspect>(jsonString, _options));
        
        Assert.Contains("Invalid Aspect value", ex.Message);
        Assert.Contains("Valid values are: Widescreen16x9, Vertical9x16, Square1x1", ex.Message);
    }

    [Fact]
    public void TolerantAspectConverter_Should_SerializeToCanonicalValue()
    {
        // Arrange
        var aspect = Aspect.Widescreen16x9;

        // Act
        var json = JsonSerializer.Serialize(aspect, _options);

        // Assert
        Assert.Equal("\"Widescreen16x9\"", json);
    }

    [Fact]
    public void TolerantAspectConverter_Should_RoundTrip()
    {
        // Arrange
        var original = Aspect.Vertical9x16;

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<Aspect>(json, _options);

        // Assert
        Assert.Equal(original, deserialized);
    }

    #endregion
}
