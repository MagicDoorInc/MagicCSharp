using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicCSharp.Infrastructure;

/// <inheritdoc cref="OptionalConverterFactory" />
public sealed class OptionalConverter<T> : JsonConverter<Optional<T>>
{
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Read is only called when the property is present in the JSON, which is exactly what HasValue means.
        // An omitted property leaves the field at default(Optional<T>) and never reaches this converter.
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return new Optional<T>(value!);
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
