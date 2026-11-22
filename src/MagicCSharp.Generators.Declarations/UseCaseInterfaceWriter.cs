using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Writes a use case's interface from its <see cref="UseCaseDeclaration" />.</summary>
public static class UseCaseInterfaceWriter
{
    /// <summary>The interface alone, for a generator that writes the class itself.</summary>
    public static void WriteInterface(SourceWriter sourceWriter, UseCaseDeclaration declaration)
    {
        sourceWriter.WriteLine(AccessibilityKeyword(declaration.Class.Accessibility) + " partial interface " + declaration.InterfaceName + " : " +
                               declaration.MarkerInterface.FullyQualifiedName);
        sourceWriter.OpenBlock();
        for (var index = 0; index < declaration.ExecuteMethods.Count; index++)
        {
            if (index > 0)
            {
                sourceWriter.WriteLine();
            }

            var executeMethod = declaration.ExecuteMethods[index];
            sourceWriter.WriteDocumentation(executeMethod.Documentation);
            sourceWriter.WriteLine(executeMethod.ReturnType.FullyQualifiedName + " " + executeMethod.Name + "(" + InterfaceParameterList(executeMethod) + ");");
        }

        sourceWriter.CloseBlock();
    }

    /// <summary>The interface and the part of the class that implements it, as one file.</summary>
    public static GeneratedFile WriteFile(UseCaseDeclaration declaration)
    {
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        if (declaration.Class.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + declaration.Class.Namespace + ";");
            sourceWriter.WriteLine();
        }

        WriteInterface(sourceWriter, declaration);
        sourceWriter.WriteLine();
        var classKeyword = declaration.Class.Kind == TypeReferenceKind.Record ? "record" : "class";
        sourceWriter.WriteLine("partial " + classKeyword + " " + declaration.Class.Name + " : " + declaration.InterfaceFullyQualifiedName);
        sourceWriter.OpenBlock();
        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(declaration.Class, "Interface"),
            Text = sourceWriter.ToString(),
        };
    }

    internal static string ParameterList(MethodDeclaration method)
    {
        return string.Join(", ", method.Parameters.Select(ParameterText));
    }

    // [NotLogged] is the one parameter attribute the interface carries: whatever reads the interface instead of the
    // class (a decorator or client generated in another project) must still leave that parameter out of its logs.
    // Other parameter attributes are about the class's implementation (model binding, validation, the caller-info
    // and iterator attributes) and mean nothing on an interface member, or change what its callers compile to.
    private static string InterfaceParameterList(MethodDeclaration method)
    {
        return string.Join(", ", method.Parameters.Select(parameter => NotLoggedPrefix(parameter) + ParameterText(parameter)));
    }

    private static string NotLoggedPrefix(ParameterDeclaration parameter)
    {
        return LoggedParameters.IsMarkedNotLogged(parameter) ? "[" + LoggedParameters.NotLoggedAttributeFullyQualifiedName + "] " : "";
    }

    private static string ParameterText(ParameterDeclaration parameter)
    {
        var parameterText = RefKindPrefix(parameter.RefKind) + (parameter.IsParams ? "params " : "") + parameter.Type.FullyQualifiedName + " " +
                            EscapedName(parameter.Name);
        if (parameter.DefaultValueExpression != null)
        {
            parameterText += " = " + parameter.DefaultValueExpression;
        }

        return parameterText;
    }

    private static string RefKindPrefix(RefKind refKind)
    {
        switch (refKind)
        {
            case RefKind.Ref:
                return "ref ";
            case RefKind.Out:
                return "out ";
            case RefKind.In:
                return "in ";
            default:
                return "";
        }
    }

    internal static string EscapedName(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;
    }

    internal static string AccessibilityKeyword(Accessibility accessibility)
    {
        return accessibility == Accessibility.Public ? "public" : "internal";
    }
}
