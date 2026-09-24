using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Modules;

/// <summary>
///     How <see cref="ImplementationRegistrationModule.AddImplementationsOf{TInterface}" /> discovers and registers
///     implementations. Every member has a default, so <c>new ImplementationRegistrationOptions()</c> is the plain
///     one-implementation-per-interface, transient registration.
/// </summary>
public record ImplementationRegistrationOptions
{
    /// <summary>Lifetime used for any implementation the <see cref="LifetimeSelector" /> does not override.</summary>
    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Transient;

    /// <summary>
    ///     Also register <c>Lazy&lt;TSubInterface&gt;</c> for each discovered interface, so a consumer can depend on a
    ///     service without constructing it. Registered as transient: a singleton <c>Lazy&lt;T&gt;</c> would capture the
    ///     root provider and hand every scope the same instance of a scoped service.
    /// </summary>
    public bool ShouldRegisterLazy { get; init; }

    /// <summary>Optional per-implementation lifetime override, e.g. reading an attribute.</summary>
    public Func<Type, ServiceLifetime?>? LifetimeSelector { get; init; }

    /// <summary>
    ///     Optional filter narrowing which loaded assemblies are scanned. Defaults to everything that is not a
    ///     framework assembly. Pass a filter to exclude test doubles from a production container.
    /// </summary>
    public Func<Assembly, bool>? AssemblyFilter { get; init; }

    /// <summary>
    ///     Whether one interface may have several implementations. False — the default — throws instead, because
    ///     for a one-interface-one-implementation marker like a use case, a second implementation means
    ///     <c>GetRequiredService&lt;T&gt;</c> silently resolves whichever was registered last. Set true for a
    ///     marker that genuinely describes a catalog resolved as <c>IEnumerable&lt;T&gt;</c>.
    /// </summary>
    public bool AllowMultipleImplementations { get; init; }
}
