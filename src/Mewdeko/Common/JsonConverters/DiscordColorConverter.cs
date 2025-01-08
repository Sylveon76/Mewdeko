using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Common.JsonConverters;

/// <summary>
///     Provides a converter for the Discord Color type for JSON operations.
/// </summary>
public class DiscordColorConverter : JsonConverter<Color?>
{
    /// <summary>
    ///     Reads and converts the JSON to type Color.
    /// </summary>
    public override Color? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        try
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var colorString = reader.GetString();
                if (string.IsNullOrWhiteSpace(colorString))
                    return null;

                if (colorString.StartsWith("#"))
                    return new Color(Convert.ToUInt32(colorString.Replace("#", ""), 16));

                if (colorString.StartsWith("0x") && colorString.Length == 8)
                    return new Color(Convert.ToUInt32(colorString.Replace("0x", ""), 16));

                if (uint.TryParse(colorString, out var numFromString))
                    return new Color(numFromString);

                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                return new Color(reader.GetUInt32());
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Writes a Color value to JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Color? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value.RawValue);
        else
            writer.WriteNullValue();
    }
}