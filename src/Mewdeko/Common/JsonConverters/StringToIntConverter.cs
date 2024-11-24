using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Common.JsonConverters;

/// <summary>
///     Provides a converter for converting string to int and vice versa for JSON operations.
/// </summary>
/// <summary>
///     Provides a converter for converting between string and integer values during JSON serialization.
/// </summary>
public class StringToIntConverter : JsonConverter<string>
{
    /// <summary>
    ///     Reads and converts the JSON to a string.
    /// </summary>
    /// <param name="reader">The reader to get the value from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">Options for reading the JSON.</param>
    /// <returns>A string representation of the value.</returns>
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString();
    }

    /// <summary>
    ///     Writes a string value as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">Options for writing the JSON.</param>
    /// <remarks>
    ///     If the string can be parsed as an integer, writes it as a number.
    ///     Otherwise, writes a null value.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (int.TryParse(value, out var intValue))
        {
            writer.WriteNumberValue(intValue);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}