namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     A class implementing <c>IDalTransform&lt;TEntity, TEdit&gt;</c>: the Dal, the entity and edit it maps between,
///     their properties, and which mapping methods the Dal already has.
/// </summary>
public sealed record DalDeclaration
{
    public required TypeDeclaration Dal { get; init; }

    public required TypeReference Entity { get; init; }

    public required TypeReference Edit { get; init; }

    public required EquatableArray<MappableProperty> DalProperties { get; init; }

    public required EquatableArray<MappableProperty> EntityProperties { get; init; }

    public required EquatableArray<MappableProperty> EditProperties { get; init; }

    public required DalPrimaryKind PrimaryKind { get; init; }

    /// <summary>The Dal property named by the assembly's <c>[assembly: TenantKey]</c>, when the Dal has it as a <c>long</c>; else null.</summary>
    public required string? TenantKeyPropertyName { get; init; }

    /// <summary>The Dal declares <c>ToEntity()</c>, or inherits one that is not abstract.</summary>
    public required bool HasOwnToEntity { get; init; }

    /// <summary>The Dal declares <c>Apply(TEdit)</c>, or inherits one that is not abstract.</summary>
    public required bool HasOwnApply { get; init; }

    /// <summary>The Dal declares a static <c>From(TEdit, long)</c> (id Dals) or <c>From(TEdit, string)</c> (key Dals).</summary>
    public required bool HasOwnFrom { get; init; }

    /// <summary>That own <c>From</c> is public, so it can implement <c>IDalFromEdit</c>.</summary>
    public required bool IsOwnFromPublic { get; init; }

    /// <summary>A base class declares <c>ToEntity()</c> abstract, so the generated one overrides it.</summary>
    public required bool ShouldOverrideToEntity { get; init; }

    /// <summary>A base class declares <c>Apply(TEdit)</c> abstract, so the generated one overrides it.</summary>
    public required bool ShouldOverrideApply { get; init; }

    /// <summary>The entity can be built with <c>new TEntity { … }</c>.</summary>
    public required bool HasEntityParameterlessConstructor { get; init; }

    /// <summary>An id Dal that does not implement <c>IDalFromEdit</c> yet, in a compilation where that interface exists.</summary>
    public required bool CanAddDalFromEdit { get; init; }
}
