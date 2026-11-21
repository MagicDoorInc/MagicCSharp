namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>The plan for a Dal's three mapping methods.</summary>
public sealed record DalMappingPlan
{
    public required MethodPlan ToEntity { get; init; }

    public required MethodPlan Apply { get; init; }

    public required MethodPlan From { get; init; }

    /// <summary>The reason each method that should have been generated was not.</summary>
    public IEnumerable<DiagnosticInfo> Failures()
    {
        return new[] { ToEntity.Failure, Apply.Failure, From.Failure }.Where(failure => failure != null).Select(failure => failure!);
    }
}
