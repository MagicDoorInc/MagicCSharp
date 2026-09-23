namespace MagicCSharp.Events.Events;

public interface IEventTypeHolder
{
    IReadOnlyList<Type> GetEventTypes();
    IReadOnlyList<Type> GetHandlerTypes(Type eventType);
}

public class MagicEventTypeHolder(
    IReadOnlyList<Type> eventTypes,
    Dictionary<Type, IReadOnlyList<Type>> sortedHandlersByEventType) : IEventTypeHolder
{
    public IReadOnlyList<Type> GetEventTypes()
    {
        return eventTypes;
    }

    public IReadOnlyList<Type> GetHandlerTypes(Type eventType)
    {
        return sortedHandlersByEventType.TryGetValue(eventType, out var handlers) ? handlers : [];
    }
}