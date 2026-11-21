namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>How a Dal's rows are keyed, which decides the second parameter of its <c>From</c>.</summary>
public enum DalPrimaryKind
{
    /// <summary>A Snowflake <c>long Id</c> (<c>IDalId</c>): <c>From(edit, long id)</c>.</summary>
    Id,

    /// <summary>A string <c>Key</c> (<c>IDalKey</c>): <c>From(edit, string key)</c>.</summary>
    Key,

    /// <summary>Neither; the Dal gets no <c>From</c>.</summary>
    None,
}
