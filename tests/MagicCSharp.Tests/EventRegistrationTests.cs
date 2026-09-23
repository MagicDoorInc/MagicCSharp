using MagicCSharp.Events;
using MagicCSharp.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicCSharp.Tests;

public class EventRegistrationTests
{
    [Fact]
    public void AddLocalMagicEvents_is_enough_on_its_own()
    {
        // It used to register only IEventDispatcher, so an application that called just this failed at
        // resolution the first time it dispatched — and the README told people to do exactly that.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddLocalMagicEvents();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IEventDispatcher>());
        Assert.NotNull(provider.GetService<IAsyncEventDispatcher>());
        Assert.NotNull(provider.GetService<IEventTypeHolder>());
        Assert.NotNull(provider.GetService<IEventSerializer>());
    }

    [Fact]
    public void Registering_events_twice_does_not_handle_everything_twice()
    {
        // Each transport calls AddMagicEvents, and an application may call it too. Without the guard every
        // handler is registered twice and every event handled twice.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMagicEvents();
        services.AddMagicEvents();
        services.AddLocalMagicEvents();

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IEventTypeHolder>());
        Assert.Single(provider.GetServices<IAsyncEventDispatcher>());
    }

    [Fact]
    public void Handler_priority_is_read_from_the_static_property()
    {
        // The dispatcher reads Priority without constructing the handler. An instance property compiles and
        // is silently ignored, which is what the docs used to show.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMagicEvents();

        using var provider = services.BuildServiceProvider();
        var holder = provider.GetRequiredService<IEventTypeHolder>();

        var handlers = holder.GetHandlerTypes(typeof(OrderPlaced));

        // Lower priority runs first: RunLast must come after the default.
        Assert.Equal([typeof(FirstHandler), typeof(LastHandler)], handlers);
    }
}

public record OrderPlaced : MagicEvent
{
    public long OrderId { get; init; }
}

public class FirstHandler : IEventHandler<OrderPlaced>
{
    public static MagicEventPriority Priority => MagicEventPriority.AddDataNoDependencies;

    public Task Handle(OrderPlaced magicEvent) => Task.CompletedTask;
}

public class LastHandler : IEventHandler<OrderPlaced>
{
    public static MagicEventPriority Priority => MagicEventPriority.RunLast;

    public Task Handle(OrderPlaced magicEvent) => Task.CompletedTask;
}
