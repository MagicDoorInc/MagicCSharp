namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>
///     What one <c>[MagicClient]</c> interface or <c>[GenerateFakeBase]</c> class gets: its generated files, or the
///     diagnostics that refuse it.
/// </summary>
public sealed record ClientGenerationResult
{
    public static ClientGenerationResult Nothing { get; } = new ClientGenerationResult
    {
        Files = EquatableArray<GeneratedFile>.Empty,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };

    public required EquatableArray<GeneratedFile> Files { get; init; }

    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }

    internal static ClientGenerationResult Generated(params GeneratedFile[] files)
    {
        return new ClientGenerationResult
        {
            Files = new EquatableArray<GeneratedFile>(files),
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    internal static ClientGenerationResult Refused(IEnumerable<DiagnosticInfo> problems)
    {
        return new ClientGenerationResult
        {
            Files = EquatableArray<GeneratedFile>.Empty,
            Diagnostics = new EquatableArray<DiagnosticInfo>(problems),
        };
    }
}
