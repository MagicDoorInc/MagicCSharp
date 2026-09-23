namespace MagicCSharp.Events.Events;

public interface IEventTypeHolder
{
    IReadOnlyList<Type> GetEventTypes();
    IReadOnlyList<Type> GetHandlerTypes(Type eventType);
}
