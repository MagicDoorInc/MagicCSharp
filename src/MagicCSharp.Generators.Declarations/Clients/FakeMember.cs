namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>One interface member a fake base stubs with a member that throws <c>NotScriptedException</c>.</summary>
internal sealed record FakeMember
{
    public required FakeMemberKind Kind { get; init; }

    public required string Name { get; init; }

    /// <summary>The return type of a method, the type of a property, indexer or event; written as C#.</summary>
    public required string TypeFullyQualifiedName { get; init; }

    /// <summary>The parameter list of a method or indexer, written as C#; empty otherwise.</summary>
    public required string ParameterList { get; init; }

    public required bool HasGetter { get; init; }

    public required SetterKind SetterKind { get; init; }

    /// <summary>The member as the exception names it: <c>IOrdersClient.GetOrders(OrderFilter)</c>.</summary>
    public required string ScriptedName { get; init; }
}
