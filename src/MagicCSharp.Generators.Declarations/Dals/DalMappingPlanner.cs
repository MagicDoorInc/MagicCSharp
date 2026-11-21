using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     Decides each mapping method of a Dal by one rule: copy a property to the property of the same name and the same
///     type, nullability included. Nothing is configured, converted or guessed; where the rule does not reach, the
///     method is not generated and the failure names the property.
///     <list type="bullet">
///         <item><c>ToEntity</c>: every entity property with a public <c>set</c> or <c>init</c>, from the Dal.</item>
///         <item>
///             <c>Apply</c>: every edit property onto the Dal, except the columns set only when the row is created —
///             <c>required</c> or <c>[Key]</c> ones, and the id or key.
///         </item>
///         <item>
///             <c>From</c>: the id or key from its parameter, the other columns set only at creation from the edit, then
///             <c>Apply</c>; only for a Dal that opted into it (<see cref="DalDeclaration.HasOptedIntoFrom" />).
///         </item>
///     </list>
/// </summary>
public static class DalMappingPlanner
{
    private const string IdPropertyName = "Id";
    private const string KeyPropertyName = "Key";

    public static DalMappingPlan Plan(DalDeclaration dal)
    {
        return new DalMappingPlan
        {
            ToEntity = PlanToEntity(dal),
            Apply = PlanApply(dal),
            From = PlanFrom(dal),
        };
    }

    /// <summary>
    ///     Whether <paramref name="dalMappableProperty" /> is set once, by <c>From</c>, and never by <c>Apply</c>: a row keeps its
    ///     id, key and creation-time columns.
    /// </summary>
    public static bool IsSetOnlyAtCreation(DalDeclaration dal, MappableProperty dalMappableProperty)
    {
        return dalMappableProperty.IsRequired || dalMappableProperty.IsKey || IsPrimaryKey(dal, dalMappableProperty);
    }

    private static MethodPlan PlanToEntity(DalDeclaration dal)
    {
        if (dal.HasOwnToEntity)
        {
            return MethodPlan.LeftToTheDal;
        }

        if (!dal.HasEntityParameterlessConstructor)
        {
            return Failed(dal, DalMappingDiagnostics.EntityWithoutParameterlessConstructor, dal.Dal.Name, Display(dal.Entity.FullyQualifiedName));
        }

        var propertyAssignments = new List<PropertyAssignment>();
        foreach (var entityMappableProperty in dal.EntityProperties.Where(property => property.IsInitializable))
        {
            var dalMappableProperty = Find(dal.DalProperties, entityMappableProperty.Name);
            var mismatch = Mismatch(dalMappableProperty, entityMappableProperty.TypeName, dalMappableProperty != null && dalMappableProperty.IsReadable, "getter");
            if (mismatch != null)
            {
                return Failed(dal, DalMappingDiagnostics.DalToEntityNotGenerated, dal.Dal.Name, dal.Entity.Name, entityMappableProperty.Name,
                    Display(entityMappableProperty.TypeName), mismatch);
            }

            propertyAssignments.Add(new PropertyAssignment { TargetName = entityMappableProperty.Name, SourceName = dalMappableProperty!.Name });
        }

        return Generated(propertyAssignments);
    }

    private static MethodPlan PlanApply(DalDeclaration dal)
    {
        if (dal.HasOwnApply)
        {
            return MethodPlan.LeftToTheDal;
        }

        var propertyAssignments = new List<PropertyAssignment>();
        foreach (var editMappableProperty in dal.EditProperties.Where(property => property.IsReadable))
        {
            var dalMappableProperty = Find(dal.DalProperties, editMappableProperty.Name);
            if (dalMappableProperty != null && IsSetOnlyAtCreation(dal, dalMappableProperty))
            {
                continue;
            }

            var mismatch = Mismatch(dalMappableProperty, editMappableProperty.TypeName, dalMappableProperty != null && dalMappableProperty.IsSettable, "setter");
            if (mismatch != null)
            {
                return Failed(dal, DalMappingDiagnostics.DalApplyNotGenerated, dal.Dal.Name, dal.Edit.Name, editMappableProperty.Name,
                    Display(editMappableProperty.TypeName), mismatch);
            }

            propertyAssignments.Add(new PropertyAssignment { TargetName = dalMappableProperty!.Name, SourceName = editMappableProperty.Name });
        }

        return Generated(propertyAssignments);
    }

    private static MethodPlan PlanFrom(DalDeclaration dal)
    {
        if (dal.PrimaryKind == DalPrimaryKind.None || dal.HasOwnFrom || !dal.HasOptedIntoFrom)
        {
            return MethodPlan.LeftToTheDal;
        }

        var propertyAssignments = new List<PropertyAssignment>();
        foreach (var dalMappableProperty in dal.DalProperties.Where(property => IsSetOnlyAtCreation(dal, property) && !IsPrimaryKey(dal, property)))
        {
            var editMappableProperty = Find(dal.EditProperties, dalMappableProperty.Name);
            var reason = FromMismatch(dalMappableProperty, editMappableProperty, dal.Edit.Name);
            if (reason != null)
            {
                return Failed(dal, DalMappingDiagnostics.DalFromNotGenerated, dal.Dal.Name, dalMappableProperty.Name, Display(dalMappableProperty.TypeName), reason,
                    dal.Edit.Name);
            }

            propertyAssignments.Add(new PropertyAssignment { TargetName = dalMappableProperty.Name, SourceName = editMappableProperty!.Name });
        }

        return Generated(propertyAssignments);
    }

    private static bool IsPrimaryKey(DalDeclaration dal, MappableProperty dalMappableProperty)
    {
        return (dal.PrimaryKind == DalPrimaryKind.Id && dalMappableProperty.Name == IdPropertyName) ||
               (dal.PrimaryKind == DalPrimaryKind.Key && dalMappableProperty.Name == KeyPropertyName);
    }

    private static MappableProperty? Find(EquatableArray<MappableProperty> properties, string name)
    {
        return properties.FirstOrDefault(property => property.Name == name);
    }

    // Null when the Dal property can take the copy; otherwise the end of the sentence saying why it cannot.
    private static string? Mismatch(MappableProperty? dalMappableProperty, string typeName, bool hasAccessor, string accessorName)
    {
        if (dalMappableProperty == null)
        {
            return "has no Dal property of the same name";
        }

        if (dalMappableProperty.TypeName != typeName)
        {
            return "differs from the Dal property, which is " + Display(dalMappableProperty.TypeName);
        }

        if (!hasAccessor)
        {
            return "meets a Dal property without a public " + accessorName;
        }

        return null;
    }

    private static string? FromMismatch(MappableProperty dalMappableProperty, MappableProperty? editMappableProperty, string editName)
    {
        if (!dalMappableProperty.IsInitializable)
        {
            return "it has no public setter or init";
        }

        if (editMappableProperty == null || !editMappableProperty.IsReadable)
        {
            return "'" + editName + "' has no readable property of that name";
        }

        if (editMappableProperty.TypeName != dalMappableProperty.TypeName)
        {
            return "'" + editName + "." + editMappableProperty.Name + "' is " + Display(editMappableProperty.TypeName);
        }

        return null;
    }

    private static string Display(string fullyQualifiedName)
    {
        return fullyQualifiedName.Replace("global::", "");
    }

    private static MethodPlan Generated(List<PropertyAssignment> propertyAssignments)
    {
        return new MethodPlan
        {
            IsGenerated = true,
            Assignments = new EquatableArray<PropertyAssignment>(propertyAssignments),
            Failure = null,
        };
    }

    private static MethodPlan Failed(DalDeclaration dal, DiagnosticDescriptor descriptor, params string[] messageArguments)
    {
        return new MethodPlan
        {
            IsGenerated = false,
            Assignments = EquatableArray<PropertyAssignment>.Empty,
            Failure = new DiagnosticInfo
            {
                Descriptor = descriptor,
                Location = dal.Dal.Location,
                MessageArguments = new EquatableArray<string>(messageArguments),
            },
        };
    }
}
