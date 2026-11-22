namespace MagicCSharp.UseCases;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to write this use case's interface: <c>I{ClassName}</c>, extending
///     <see cref="IMagicUseCase" />, with every public <c>Execute</c> method of the class and its doc comment. A
///     parameter marked <see cref="NotLoggedAttribute" /> keeps the mark on the interface; no other parameter
///     attribute is copied. The class must be <c>partial</c>. A class that already implements its own interface is
///     left alone.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateInterfaceAttribute : Attribute
{
}
