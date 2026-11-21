namespace MagicCSharp.Testing;

/// <summary>
///     Asks <c>MagicCSharp.Generators</c> to make this <c>abstract partial</c> class a fake base for
///     <paramref name="interfaceType" />: the class implements the interface, and every member it and its base classes
///     do not already implement becomes <c>public virtual</c> and throws <see cref="NotScriptedException" />. The
///     fake inherits it and overrides only what its tests use, so a member added to the interface later breaks no
///     fake, and a test that calls an unscripted member fails instead of passing on an empty answer.
/// </summary>
/// <param name="interfaceType">The interface the fake base implements.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateFakeBaseAttribute(Type interfaceType) : Attribute
{
    public Type InterfaceType { get; } = interfaceType;
}
