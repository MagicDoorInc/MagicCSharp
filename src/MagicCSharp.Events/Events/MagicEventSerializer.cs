using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicCSharp.Events;

/// <summary>
/// JSON-based event serializer that wraps events with type information for polymorphic deserialization.
/// </summary>
public class MagicEventSerializer(IEnumerable<Type> eventTypes) : IEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new LongToStringConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultBufferSize = 4096,
    };

    private readonly Dictionary<string, Type> allEventTypeMap = eventTypes.ToDictionary(t => t.Name);

    public string SerializeMagicEvent(MagicEvent magicEvent)
    {
        var body = JsonSerializer.Serialize((object)magicEvent, SerializerOptions);
        var jsonDocument = JsonDocument.Parse(body);
        var wrapper = new MagicEventWrapper(magicEvent.GetType().Name, jsonDocument.RootElement);
        return JsonSerializer.Serialize(wrapper, SerializerOptions);
    }

    public MagicEvent? DeserializeMagicEvent(string json)
    {
        var wrapper = JsonSerializer.Deserialize<MagicEventWrapper>(json, SerializerOptions);
        if (wrapper == null || !allEventTypeMap.TryGetValue(wrapper.Type, out var type))
        {
            // We don't know the event type - this is ok, we're not interested in it
            return null;
        }

        var @event = JsonSerializer.Deserialize(wrapper.Body.ToString(), type, SerializerOptions);
        if (@event == null || @event is not MagicEvent)
        {
            throw new InvalidOperationException($"Failed to deserialize MagicEvent: {wrapper.Body}");
        }

        return (MagicEvent)@event;
    }

    /// <summary>
    /// Wrapper record for event serialization with type information.
    /// </summary>
    public record MagicEventWrapper(
        string Type,
        JsonElement Body);
}

/// <summary>
/// JSON converter that handles long values as strings to prevent precision loss in JavaScript.
/// </summary>
public sealed class LongToStringConverter : JsonConverter<long>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(long);
    }

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number
            ? reader.GetInt64()
            : long.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
