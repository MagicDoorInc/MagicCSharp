namespace MagicCSharp.UseCases;

/// <summary>
///     Asks for a use case's <c>Executing:</c> trace line to be written for it. MagicCSharp.Generators writes a
///     decorator for the use case's interface that logs <c>Executing: name={name}, …</c> with every argument, at
///     trace level and under the use case's own logger category, before each <c>Execute</c> call, then calls the use
///     case; any other interface method (a read such as <c>GetByLease</c>) is passed through without a line. Use
///     case discovery registers that decorator for the interface in the class's place. Only
///     <c>AddImplementationsOf</c> registers it (and <c>AddMagicUseCases</c> and <c>AddMagicCSharp</c>, which call
///     it): a use case registered another way logs no line. Delete the hand-written line when you add this.
///     <para>
///         The build refuses a read-only use case (Get, List, Search, Find, Is, Can, Count) and a parameter that
///         looks like a secret (a password, secret, token or key, a stream or a byte array) on an <c>Execute</c>
///         method unless that parameter is marked <see cref="NotLoggedAttribute" />. Cancellation tokens are never
///         logged.
///     </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LogExecutingAttribute : Attribute
{
}
