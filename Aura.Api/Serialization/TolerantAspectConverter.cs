using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aura.Core.Models;

namespace Aura.Api.Serialization;

/// <summary>
/// Tolerant JSON converter for Aspect enum that accepts both canonical names and aliases.
/// Canonical: Widescreen16x9, Vertical9x16, Square1x1
/// Aliases: "16:9" -> Widescreen16x9, "9:16" -> Vertical9x16, "1:1" -> Square1x1
/// </summary>
public class TolerantAspectConverter : JsonConverter<Aspect>
{
    public override Aspect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string value for Aspect, got {reader.TokenType}");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Aspect value cannot be empty");
        }

        // Try canonical names (case-insensitive)
        if (Enum.TryParse<Aspect>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        // Try aliases
        switch (value.ToLowerInvariant())
        {
            case "16:9":
                return Aspect.Widescreen16x9;
            case "9:16":
                return Aspect.Vertical9x16;
            case "1:1":
                return Aspect.Square1x1;
            default:
                throw new JsonException(CreateInvalidValueMessage(value));
        }
    }

    public override void Write(Utf8JsonWriter writer, Aspect value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    private static string CreateInvalidValueMessage(string value)
    {
        return $"Invalid Aspect value: '{value}'. Valid values are: 'Widescreen16x9' (or '16:9'), 'Vertical9x16' (or '9:16'), 'Square1x1' (or '1:1').";
    }
}
