using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     A diagnostic held as plain values until a generator reports it. A <see cref="Diagnostic" /> holds a
///     <see cref="Location" />, which would defeat the incremental cache.
/// </summary>
public sealed record DiagnosticInfo
{
    public required DiagnosticDescriptor Descriptor { get; init; }

    public required SourceLocation? Location { get; init; }

    public required EquatableArray<string> MessageArguments { get; init; }

    public string Id => Descriptor.Id;

    public Diagnostic ToDiagnostic()
    {
        var messageArguments = MessageArguments.Cast<object>().ToArray();
        return Diagnostic.Create(Descriptor, Location?.ToLocation(), messageArguments);
    }
}
