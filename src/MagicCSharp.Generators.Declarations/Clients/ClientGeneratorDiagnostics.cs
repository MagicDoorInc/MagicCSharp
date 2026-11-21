using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>
///     The <c>MCS15xx</c> diagnostics of the client generators: <c>[MagicClient]</c> interfaces, their client
///     convention, and <c>[GenerateFakeBase]</c> classes. A refused client or fake base gets no generated code at
///     all, so the build shows the refusal instead of a wall of follow-on errors.
/// </summary>
public static class ClientGeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor MoreThanOneClientConvention = new DiagnosticDescriptor("MCS1501",
        "More than one [MagicClientConvention]",
        "'{0}' cannot be generated: both '{1}' and '{2}' declare [assembly: MagicClientConvention]; keep one",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ClientBaseLacksMember = new DiagnosticDescriptor("MCS1502",
        "The client base lacks a member the generated client calls",
        "'{0}' cannot be generated: its base '{1}' has no '{2}'",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ClientRegistrationNotFound = new DiagnosticDescriptor("MCS1503",
        "The client registration method was not found",
        "'{0}' cannot be registered: '{1}' has no static method '{2}<TInterface, TClient>(IServiceCollection services, IConfiguration configuration, string? baseUrl)'",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor RouteValueWithoutParameter = new DiagnosticDescriptor("MCS1504",
        "A route value matches no parameter",
        "'{0}.{1}': the route '{2}' names '{{{3}}}', which is not a parameter of the method",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor UnsupportedRouteValueType = new DiagnosticDescriptor("MCS1505",
        "A route value must be a string, integer, Guid or enum",
        "'{0}.{1}': route value '{2}' is a '{3}'; a route value must be a string, an integer, a Guid or an enum",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MoreThanOneBodyParameter = new DiagnosticDescriptor("MCS1506",
        "A client write has more than one body parameter",
        "'{0}.{1}' has more than one object parameter outside the route ({2}); a request has one body, so group them in one request type or write the method by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ClientMethodNotTask = new DiagnosticDescriptor("MCS1507",
        "A client route method returns Task or Task<T>",
        "'{0}.{1}' returns '{2}'; a method with a client route attribute returns Task or Task<T>, or is written by hand without the attribute",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericClientInterface = new DiagnosticDescriptor("MCS1508",
        "A [MagicClient] interface cannot be generic",
        "'{0}' is generic; write its client by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedClientInterface = new DiagnosticDescriptor("MCS1509",
        "A [MagicClient] interface cannot be nested",
        "'{0}' is nested in another type; move it to the top level of its file",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericClientMethod = new DiagnosticDescriptor("MCS1510",
        "A client route method cannot be generic",
        "'{0}.{1}' is generic; remove its client route attribute and write it by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ClientInterfaceNameWithoutI = new DiagnosticDescriptor("MCS1511",
        "A [MagicClient] interface name starts with I",
        "'{0}' does not start with 'I' and a capital letter, so its client class has no name; rename it 'I{0}'",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor FakeBaseNotPartial = new DiagnosticDescriptor("MCS1512",
        "A [GenerateFakeBase] class must be partial",
        "'{0}' has [GenerateFakeBase] but is not partial; add 'partial' so the generated members can be attached to it",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor FakeBaseNotAbstract = new DiagnosticDescriptor("MCS1513",
        "A [GenerateFakeBase] class must be abstract",
        "'{0}' has [GenerateFakeBase] but is not abstract; a fake base is only inherited, by the fake that overrides what its tests use",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor FakeBaseTypeNotInterface = new DiagnosticDescriptor("MCS1514",
        "[GenerateFakeBase] names an interface",
        "'{0}': [GenerateFakeBase] names '{1}', which is not an interface",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor FakeBaseInterfaceHasGenericMethod = new DiagnosticDescriptor("MCS1515",
        "A fake base cannot stub a generic method",
        "'{0}': '{1}.{2}' is generic, which a fake base does not stub; implement the interface in the fake by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedFakeBase = new DiagnosticDescriptor("MCS1516",
        "A [GenerateFakeBase] class cannot be nested",
        "'{0}' is nested in another type; move it to the top level of its file",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    internal static DiagnosticInfo Problem(DiagnosticDescriptor descriptor, SourceLocation? location, params string[] messageArguments)
    {
        return new DiagnosticInfo
        {
            Descriptor = descriptor,
            Location = location,
            MessageArguments = new EquatableArray<string>(messageArguments),
        };
    }
}
