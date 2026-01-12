namespace MagicCSharp.Events.Events;

/// <summary>
///     Interface for serializing and deserializing MagicEvent instances.
/// </summary>
public interface IEventSerializer
{
    /// <summary>
    ///     Serialize a MagicEvent to a string.
    /// </summary>
    string SerializeMagicEvent(MagicEvent magicEvent);

    /// <summary>
    ///     Deserialize a string back to a MagicEvent.
    ///     Returns null if the event type is not recognized.
    /// </summary>
    MagicEvent? DeserializeMagicEvent(string json);
}