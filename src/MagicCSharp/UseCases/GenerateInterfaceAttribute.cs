namespace MagicCSharp.UseCases;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to write this use case's interface: <c>I{ClassName}</c>, extending
///     <see cref="IMagicUseCase" />, with every public instance method the class declares (its <c>Execute</c>
///     methods and any other, such as <c>GetByLease</c>), each with its doc comment, type parameters and constraints.
///     Left out: static methods, overrides, members the compiler writes, and methods that implement another
///     interface the class lists (<c>IDisposable.Dispose</c>), which belong to that interface. A parameter marked
///     <see cref="NotLoggedAttribute" /> keeps the mark on the interface; no other parameter attribute is copied. The
///     class must be <c>partial</c> and have a public <c>Execute</c>. A class that already implements its own
///     interface is left alone.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateInterfaceAttribute : Attribute
{
}
