namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>The type of a filter field, as far as the filter rules care.</summary>
public enum FilterFieldShape
{
    /// <summary>A collection of a struct or of <c>string</c>: <c>List&lt;long&gt;?</c>, <c>Status[]</c>, <c>IReadOnlyList&lt;string&gt;?</c>.</summary>
    List,

    /// <summary>A nullable struct, <c>long?</c> or <c>bool?</c>; a <c>bool?</c> may also take the <c>NullablePresence</c> rule.</summary>
    NullableValue,

    /// <summary>MagicCSharp's <c>ComparableRange&lt;T&gt;</c>, or a type derived from it.</summary>
    ComparableRange,

    /// <summary>Fits no rule's shape (a <c>string</c>, a non-nullable value, any other type), so it is always a custom field.</summary>
    Custom,
}
