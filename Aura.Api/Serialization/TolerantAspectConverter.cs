using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aura.Core.Models;

namespace Aura.Api.Serialization;

/// <summary>
/// Tolerant JSON converter for Aspect enum that accepts both canonical names and legacy aliases.
/// Canonical: Widescreen16x9, Vertical9x16, Square1x1
/// Aliases: 16:9 -> Widescreen16x9, 9:16 -> Vertical9x16, 1:1 -> Square1x1
/// </summary>
public class TolerantAspectConverter : JsonConverter<Aspect>
{
    public override Aspect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException(CreateErrorMessage(value ?? ""));
            }

            // Try canonical values (case-insensitive)
            if (Enum.TryParse<Aspect>(value, ignoreCase: true, out var aspect))
            {
                return aspect;
            }

            // Try aliases
            return value switch
            {
                "16:9" => Aspect.Widescreen16x9,
                "9:16" => Aspect.Vertical9x16,
                "1:1" => Aspect.Square1x1,
                _ => throw new JsonException(CreateErrorMessage(value))
            };
        }

        throw new JsonException("Expected string value for Aspect");
    }

    public override void Write(Utf8JsonWriter writer, Aspect value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    private static string CreateErrorMessage(string value)
    {
        return $"Invalid Aspect value '{value}'. Valid values are: Widescreen16x9, Vertical9x16, Square1x1 (or aliases: 16:9, 9:16, 1:1).";
    }

    public static string GetValidValuesMessage()
    {
        return "Valid values: Widescreen16x9, Vertical9x16, Square1x1. Aliases: 16:9, 9:16, 1:1.";
    }
}
