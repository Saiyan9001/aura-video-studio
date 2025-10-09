using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aura.Core.Models;

namespace Aura.Api.Serialization;

/// <summary>
/// Tolerant JSON converter for Density enum that accepts both canonical names and legacy aliases.
/// Canonical: Sparse, Balanced, Dense
/// Alias: Normal -> Balanced
/// </summary>
public class TolerantDensityConverter : JsonConverter<Density>
{
    public override Density Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException(CreateErrorMessage(value ?? ""));
            }

            // Try canonical values (case-insensitive)
            if (Enum.TryParse<Density>(value, ignoreCase: true, out var density))
            {
                return density;
            }

            // Try aliases
            if (value.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            {
                return Density.Balanced;
            }

            throw new JsonException(CreateErrorMessage(value));
        }

        throw new JsonException("Expected string value for Density");
    }

    public override void Write(Utf8JsonWriter writer, Density value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    private static string CreateErrorMessage(string value)
    {
        return $"Invalid Density value '{value}'. Valid values are: Sparse, Balanced, Dense (or alias: Normal for Balanced).";
    }

    public static string GetValidValuesMessage()
    {
        return "Valid values: Sparse, Balanced, Dense. Alias: Normal (for Balanced).";
    }
}
