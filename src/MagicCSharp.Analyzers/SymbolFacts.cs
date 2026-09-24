using Microsoft.CodeAnalysis;

namespace MagicCSharp.Analyzers;

internal static class SymbolFacts
{
    /// <summary>
    ///     Whether <paramref name="symbol" /> is the consumer's own code or MagicCSharp's, as opposed to a third
    ///     party's whose names the consumer does not choose.
    ///     <para>
    ///         Own code is anything in the compilation being analyzed, or in an assembly that shares its first
    ///         name segment — Acme.Shop.Domains.Orders and Acme.Libraries.Events are one codebase split into
    ///         projects, so an interface from one injected into the other is still Acme's to name.
    ///     </para>
    /// </summary>
    public static bool IsFirstParty(ISymbol symbol, Compilation compilation)
    {
        var assembly = symbol.ContainingAssembly;
        if (assembly == null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly))
        {
            return true;
        }

        var firstSegment = FirstNameSegment(assembly.Name);
        if (firstSegment == KnownTypeNames.FrameworkAssemblySegment)
        {
            return true;
        }

        return firstSegment == FirstNameSegment(compilation.AssemblyName ?? "");
    }

    private static string FirstNameSegment(string assemblyName)
    {
        var dot = assemblyName.IndexOf('.');
        return dot < 0 ? assemblyName : assemblyName.Substring(0, dot);
    }

    public static bool IsObsolete(ISymbol symbol)
    {
        return HasAttribute(symbol, "System.ObsoleteAttribute");
    }

    public static bool IsInheritedMember(ISymbol symbol)
    {
        if (symbol.IsOverride)
        {
            return true;
        }

        if (HasExplicitInterfaceImplementation(symbol))
        {
            return true;
        }

        var containingType = symbol.ContainingType;
        if (containingType == null || symbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        foreach (var contract in containingType.AllInterfaces)
        {
            foreach (var member in contract.GetMembers())
            {
                var implementation = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(implementation, symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool Implements(INamedTypeSymbol type, INamedTypeSymbol? contract)
    {
        if (contract == null)
        {
            return false;
        }

        return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract));
    }

    public static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType == null)
        {
            return false;
        }

        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    public static bool IsLambdaParameter(ISymbol symbol)
    {
        var parameter = symbol as IParameterSymbol;
        if (parameter == null)
        {
            return false;
        }

        var method = parameter.ContainingSymbol as IMethodSymbol;
        return method != null && method.MethodKind == MethodKind.AnonymousFunction;
    }

    public static bool IsAiToolParameter(ISymbol symbol)
    {
        var parameter = symbol as IParameterSymbol;
        if (parameter == null)
        {
            return false;
        }

        var method = parameter.ContainingSymbol as IMethodSymbol;
        return method != null && HasAttribute(method, "System.ComponentModel.DescriptionAttribute");
    }

    public static bool IsInjectedDependency(IParameterSymbol parameter)
    {
        var method = parameter.ContainingSymbol as IMethodSymbol;
        if (method == null)
        {
            return false;
        }

        if (method.MethodKind == MethodKind.Constructor)
        {
            return true;
        }

        return HasAttribute(parameter, KnownTypeNames.FromServicesAttribute);
    }

    public static bool HasAttribute(ISymbol symbol, string attributeTypeName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == attributeTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExplicitInterfaceImplementation(ISymbol symbol)
    {
        var method = symbol as IMethodSymbol;
        if (method != null)
        {
            return method.ExplicitInterfaceImplementations.Length > 0;
        }

        var property = symbol as IPropertySymbol;
        if (property != null)
        {
            return property.ExplicitInterfaceImplementations.Length > 0;
        }

        var eventSymbol = symbol as IEventSymbol;
        return eventSymbol != null && eventSymbol.ExplicitInterfaceImplementations.Length > 0;
    }
}
