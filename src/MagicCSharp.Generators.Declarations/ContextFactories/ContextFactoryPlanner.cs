namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>Decides, per context, whether its factory is written, skipped for a hand-written one, or refused.</summary>
public static class ContextFactoryPlanner
{
    /// <summary>The diagnostics that refuse <paramref name="context" />'s factory; empty when it can be written.</summary>
    public static EquatableArray<DiagnosticInfo> Refusals(MigrationsDatabaseContext context, ContextFactoryBase factoryBase)
    {
        var refusal = FirstRefusal(context, factoryBase);
        return refusal == null ? EquatableArray<DiagnosticInfo>.Empty : new EquatableArray<DiagnosticInfo>([refusal]);
    }

    /// <summary>
    ///     MCS1602 when the context names no database; null otherwise. Checked even when the project writes its own
    ///     factory, because the migration check reads the name too.
    /// </summary>
    public static DiagnosticInfo? BlankDatabaseName(MigrationsDatabaseContext context)
    {
        return string.IsNullOrWhiteSpace(context.DatabaseName) ? Refusal(ContextFactoryDiagnostics.BlankDatabaseName, context, context.Name) : null;
    }

    /// <summary>Whether the project writes its own factory for the context, so nothing is generated.</summary>
    public static bool HasHandWrittenFactory(MigrationsDatabaseContext context, EquatableArray<string> contextsWithHandWrittenFactories)
    {
        return contextsWithHandWrittenFactories.Contains(context.FullyQualifiedName);
    }

    private static DiagnosticInfo? FirstRefusal(MigrationsDatabaseContext context, ContextFactoryBase factoryBase)
    {
        if (context.IsNested)
        {
            return Refusal(ContextFactoryDiagnostics.NestedContext, context, context.FullyQualifiedName);
        }

        if (context.IsGeneric)
        {
            return Refusal(ContextFactoryDiagnostics.GenericContext, context, context.Name);
        }

        if (context.IsAbstract)
        {
            return Refusal(ContextFactoryDiagnostics.AbstractContext, context, context.Name);
        }

        if (!factoryBase.IsFound)
        {
            return Refusal(ContextFactoryDiagnostics.FactoryBaseNotFound, context, context.Name, factoryBase.MetadataName);
        }

        if (factoryBase.UnusableReason != null)
        {
            return Refusal(ContextFactoryDiagnostics.FactoryBaseUnusable, context, context.Name, factoryBase.MetadataName, factoryBase.UnusableReason);
        }

        var unmetConstraint = factoryBase.ContextConstraints.FirstOrDefault(constraint => !context.BaseTypesAndInterfaces.Contains(constraint));
        if (unmetConstraint != null)
        {
            return Refusal(ContextFactoryDiagnostics.ContextFailsFactoryBaseConstraint, context, context.Name, factoryBase.MetadataName,
                unmetConstraint.Substring(GlobalAlias.Prefix.Length));
        }

        return null;
    }

    private static DiagnosticInfo Refusal(Microsoft.CodeAnalysis.DiagnosticDescriptor descriptor, MigrationsDatabaseContext context, params string[] messageArguments)
    {
        return new DiagnosticInfo
        {
            Descriptor = descriptor,
            Location = context.Location,
            MessageArguments = new EquatableArray<string>(messageArguments),
        };
    }
}
