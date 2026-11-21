namespace MagicCSharp.Modules;

/// <summary>
///     Registers the class it marks in another class's place.
///     <see cref="ImplementationRegistrationModule.AddImplementationsOf{TInterface}" /> registers the marked class for
///     every interface <see cref="ImplementationType" /> is discovered under, and <see cref="ImplementationType" />
///     as itself, both with <see cref="ImplementationType" />'s lifetime, so the marked class can take it as a
///     dependency and call it. Generated decorators carry it, such as the one <c>[LogExecuting]</c> asks for.
///     <para>
///         Only <c>AddImplementationsOf</c> reads it (and <c>AddMagicUseCases</c> and <c>AddMagicCSharp</c>, which call
///         it). A class registered any other way — by hand, through <c>AddImplementationsOfBase</c> or another scanning
///         library — keeps its own registration, and the marked class is not registered at all.
///     </para>
/// </summary>
/// <param name="implementationType">The class whose registrations the marked class takes over.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisteredInPlaceOfAttribute(Type implementationType) : Attribute
{
    /// <summary>The class whose registrations the marked class takes over.</summary>
    public Type ImplementationType { get; } = implementationType;
}
