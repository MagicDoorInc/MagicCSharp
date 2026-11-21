namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     What happens to one mapping method: generated with these assignments, left to the Dal (it has its own, or
///     needs none), or not generated because of <see cref="Failure" />.
/// </summary>
public sealed record MethodPlan
{
    public static MethodPlan LeftToTheDal { get; } = new MethodPlan
    {
        IsGenerated = false,
        Assignments = EquatableArray<PropertyAssignment>.Empty,
        Failure = null,
    };

    public required bool IsGenerated { get; init; }

    public required EquatableArray<PropertyAssignment> Assignments { get; init; }

    public required DiagnosticInfo? Failure { get; init; }
}
