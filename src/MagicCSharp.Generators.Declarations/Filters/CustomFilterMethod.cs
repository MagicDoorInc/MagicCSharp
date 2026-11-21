namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>The hand-written custom filter method (<c>ApplyCustomFilter</c> or <c>MatchesCustomFilter</c>), as far as the generator checks it.</summary>
public sealed record CustomFilterMethod
{
    /// <summary>The method is written, with a body.</summary>
    public required bool HasBody { get; init; }

    /// <summary>The filter fields its body reads: <c>filter.X</c> in <c>ApplyCustomFilter</c>, <c>X</c> or <c>this.X</c> in <c>MatchesCustomFilter</c>.</summary>
    public required EquatableArray<string> ReadFieldNames { get; init; }

    public static CustomFilterMethod None { get; } = new CustomFilterMethod
    {
        HasBody = false,
        ReadFieldNames = EquatableArray<string>.Empty,
    };
}
