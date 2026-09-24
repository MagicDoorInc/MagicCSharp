using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicCSharp.Events.Events;

/// <summary>
///     JSON converter that handles long values as strings to prevent precision loss in JavaScript.
/// </summary>
public sealed class LongToStringConverter : JsonConverter<long>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(long);
    }

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : long.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
