using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicCSharp.Events.Events;

/// <summary>
///     JSON-based event serializer that wraps events with type information for polymorphic deserialization.
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

    // Keyed by simple name, because that is what goes on the wire as the discriminator — but built by
    // hand so a duplicate names both offenders, rather than throwing "An item with the same key has
    // already been added" from inside ToDictionary during startup with no clue which types collided.
    private readonly Dictionary<string, Type> allEventTypeMap = BuildTypeMap(eventTypes);

    private static Dictionary<string, Type> BuildTypeMap(IEnumerable<Type> eventTypes)
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in eventTypes)
        {
            if (map.TryGetValue(type.Name, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two event types are both named '{type.Name}': {existing.FullName} and {type.FullName}. " +
                    "The type name is the wire format's discriminator, so it has to be unique across the " +
                    "application. Rename one of them.");
            }

            map[type.Name] = type;
        }

        return map;
    }

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
    ///     Wrapper record for event serialization with type information.
    /// </summary>
    public record MagicEventWrapper(
        string Type,
        JsonElement Body);
}

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