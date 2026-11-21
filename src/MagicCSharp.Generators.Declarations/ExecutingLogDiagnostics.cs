using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Why a <c>[LogExecuting]</c> use case gets no decorator: <c>MCS11xx</c>, one id per reason, all errors. The
///     line is opt-in, so a use case the generator cannot serve keeps its hand-written line instead.
/// </summary>
public static class ExecutingLogDiagnostics
{
    public static readonly DiagnosticDescriptor SecretParameter = new DiagnosticDescriptor("MCS1101",
        "A [LogExecuting] use case cannot log a parameter that looks like a secret",
        "Parameter '{0}' of '{1}' looks like a secret (password, secret, token or key, a stream or a byte array); mark it [NotLogged] to leave it out of the Executing line",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ReadOnlyUseCase = new DiagnosticDescriptor("MCS1102",
        "A read-only use case does not log the Executing line",
        "'{0}' is named as a read-only use case (Get, List, Search, Find, Is, Can or Count); remove [LogExecuting]",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor OwnExecutingLine = new DiagnosticDescriptor("MCS1103",
        "A [LogExecuting] use case writes its own Executing line",
        "'{0}' has [LogExecuting] and still writes its own Executing line, which would log every call twice; delete the hand-written line",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NoUseCaseInterface = new DiagnosticDescriptor("MCS1104",
        "A [LogExecuting] class must implement a use case interface",
        "'{0}' has [LogExecuting] but implements no interface extending '{1}', so there is nothing to register its decorator for",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor SeveralUseCaseInterfaces = new DiagnosticDescriptor("MCS1105",
        "A [LogExecuting] class must implement one use case interface",
        "'{0}' implements several use case interfaces ({1}); keep [LogExecuting] for a class with one, and write the line by hand here",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericClass = new DiagnosticDescriptor("MCS1106",
        "A [LogExecuting] class cannot be generic",
        "'{0}' is generic; write its Executing line by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor AbstractClass = new DiagnosticDescriptor("MCS1107",
        "A [LogExecuting] class cannot be abstract",
        "'{0}' is abstract, so nothing can be registered to call it; put [LogExecuting] on the concrete use case",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ByReferenceParameter = new DiagnosticDescriptor("MCS1108",
        "A [LogExecuting] use case cannot take ref, out or in parameters",
        "Parameter '{0}' of '{1}' is passed by reference; write the Executing line of '{1}' by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);
}
