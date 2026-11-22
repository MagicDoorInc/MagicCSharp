using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Reads a type parameter's constraints into the <c>where</c> clause that declares them again, in the order C#
///     requires: the primary constraint, the constraint types, then <c>new()</c>. A method that implements an
///     interface method implicitly must repeat its constraints.
/// </summary>
public static class TypeParameterConstraintReader
{
    /// <summary>The clause, <c>where TResult : class, new()</c>; null when the type parameter has no constraint.</summary>
    public static string? ReadClause(ITypeParameterSymbol typeParameter)
    {
        var constraints = new List<string>();
        var primaryConstraint = PrimaryConstraint(typeParameter);
        if (primaryConstraint != null)
        {
            constraints.Add(primaryConstraint);
        }

        constraints.AddRange(typeParameter.ConstraintTypes.Select(constraintType => TypeReferenceReader.Read(constraintType).FullyQualifiedName));
        if (typeParameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        if (constraints.Count == 0)
        {
            return null;
        }

        return "where " + UseCaseInterfaceWriter.EscapedName(typeParameter.Name) + " : " + string.Join(", ", constraints);
    }

    // unmanaged is checked before struct: an unmanaged type parameter has the value type constraint as well.
    private static string? PrimaryConstraint(ITypeParameterSymbol typeParameter)
    {
        if (typeParameter.HasReferenceTypeConstraint)
        {
            return typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class";
        }

        if (typeParameter.HasUnmanagedTypeConstraint)
        {
            return "unmanaged";
        }

        if (typeParameter.HasValueTypeConstraint)
        {
            return "struct";
        }

        if (typeParameter.HasNotNullConstraint)
        {
            return "notnull";
        }

        return null;
    }
}
