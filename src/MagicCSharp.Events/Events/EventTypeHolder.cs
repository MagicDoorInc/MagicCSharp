namespace MagicCSharp.Events;

/// <summary>
/// Holds all registered event types for the application.
/// Used by the serializer to enable polymorphic deserialization.
/// </summary>
public class EventTypeHolder(IEnumerable<Type> eventTypes)
{
    /// <summary>
    /// Get all registered event types.
    /// </summary>
    public IEnumerable<Type> GetEventTypes()
    {
        return eventTypes;
    }
}
