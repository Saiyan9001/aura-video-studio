using Xunit;
using System.Text.Json;
using Aura.Core.Models;
using Aura.Api.Serialization;

namespace Aura.Tests;

public class EnumConverterTests
{
    private readonly JsonSerializerOptions _options;

    public EnumConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            Converters = { new TolerantDensityConverter(), new TolerantAspectConverter() }
        };
    }

    #region Density Converter Tests

    [Fact]
    public void DensityConverter_Should_ParseCanonicalValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(Density.Sparse, JsonSerializer.Deserialize<Density>("\"Sparse\"", _options));
        Assert.Equal(Density.Balanced, JsonSerializer.Deserialize<Density>("\"Balanced\"", _options));
        Assert.Equal(Density.Dense, JsonSerializer.Deserialize<Density>("\"Dense\"", _options));
    }

    [Fact]
    public void DensityConverter_Should_ParseCanonicalValuesCaseInsensitive()
    {
        // Arrange & Act & Assert
        Assert.Equal(Density.Sparse, JsonSerializer.Deserialize<Density>("\"sparse\"", _options));
        Assert.Equal(Density.Balanced, JsonSerializer.Deserialize<Density>("\"balanced\"", _options));
        Assert.Equal(Density.Dense, JsonSerializer.Deserialize<Density>("\"DENSE\"", _options));
    }

    [Fact]
    public void DensityConverter_Should_ParseAlias_Normal()
    {
        // Arrange & Act
        var result = JsonSerializer.Deserialize<Density>("\"Normal\"", _options);

        // Assert
        Assert.Equal(Density.Balanced, result);
    }

    [Fact]
    public void DensityConverter_Should_ParseAlias_NormalCaseInsensitive()
    {
        // Arrange & Act
        var result1 = JsonSerializer.Deserialize<Density>("\"normal\"", _options);
        var result2 = JsonSerializer.Deserialize<Density>("\"NORMAL\"", _options);

        // Assert
        Assert.Equal(Density.Balanced, result1);
        Assert.Equal(Density.Balanced, result2);
    }

    [Fact]
    public void DensityConverter_Should_ThrowOnInvalidValue()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Density>("\"Invalid\"", _options));
        Assert.Contains("Invalid Density value", ex.Message);
        Assert.Contains("Sparse", ex.Message);
        Assert.Contains("Balanced", ex.Message);
        Assert.Contains("Dense", ex.Message);
    }

    [Fact]
    public void DensityConverter_Should_SerializeToCanonicalValue()
    {
        // Arrange & Act
        var json = JsonSerializer.Serialize(Density.Balanced, _options);

        // Assert
        Assert.Equal("\"Balanced\"", json);
    }

    [Fact]
    public void DensityConverter_Should_RoundTrip()
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

    #region Aspect Converter Tests

    [Fact]
    public void AspectConverter_Should_ParseCanonicalValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(Aspect.Widescreen16x9, JsonSerializer.Deserialize<Aspect>("\"Widescreen16x9\"", _options));
        Assert.Equal(Aspect.Vertical9x16, JsonSerializer.Deserialize<Aspect>("\"Vertical9x16\"", _options));
        Assert.Equal(Aspect.Square1x1, JsonSerializer.Deserialize<Aspect>("\"Square1x1\"", _options));
    }

    [Fact]
    public void AspectConverter_Should_ParseCanonicalValuesCaseInsensitive()
    {
        // Arrange & Act & Assert
        Assert.Equal(Aspect.Widescreen16x9, JsonSerializer.Deserialize<Aspect>("\"widescreen16x9\"", _options));
        Assert.Equal(Aspect.Vertical9x16, JsonSerializer.Deserialize<Aspect>("\"VERTICAL9X16\"", _options));
        Assert.Equal(Aspect.Square1x1, JsonSerializer.Deserialize<Aspect>("\"square1x1\"", _options));
    }

    [Fact]
    public void AspectConverter_Should_ParseAlias_16x9()
    {
        // Arrange & Act
        var result = JsonSerializer.Deserialize<Aspect>("\"16:9\"", _options);

        // Assert
        Assert.Equal(Aspect.Widescreen16x9, result);
    }

    [Fact]
    public void AspectConverter_Should_ParseAlias_9x16()
    {
        // Arrange & Act
        var result = JsonSerializer.Deserialize<Aspect>("\"9:16\"", _options);

        // Assert
        Assert.Equal(Aspect.Vertical9x16, result);
    }

    [Fact]
    public void AspectConverter_Should_ParseAlias_1x1()
    {
        // Arrange & Act
        var result = JsonSerializer.Deserialize<Aspect>("\"1:1\"", _options);

        // Assert
        Assert.Equal(Aspect.Square1x1, result);
    }

    [Fact]
    public void AspectConverter_Should_ThrowOnInvalidValue()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Aspect>("\"Invalid\"", _options));
        Assert.Contains("Invalid Aspect value", ex.Message);
        Assert.Contains("Widescreen16x9", ex.Message);
        Assert.Contains("16:9", ex.Message);
    }

    [Fact]
    public void AspectConverter_Should_SerializeToCanonicalValue()
    {
        // Arrange & Act
        var json = JsonSerializer.Serialize(Aspect.Vertical9x16, _options);

        // Assert
        Assert.Equal("\"Vertical9x16\"", json);
    }

    [Fact]
    public void AspectConverter_Should_RoundTrip()
    {
        // Arrange
        var original = Aspect.Square1x1;

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<Aspect>(json, _options);

        // Assert
        Assert.Equal(original, deserialized);
    }

    #endregion
}
