using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Writes a use case's interface from its <see cref="UseCaseDeclaration" />.</summary>
public static class UseCaseInterfaceWriter
{
    private const string ConstraintClauseIndentation = "    ";

    /// <summary>The interface alone, for a generator that writes the class itself.</summary>
    public static void WriteInterface(SourceWriter sourceWriter, UseCaseDeclaration declaration)
    {
        sourceWriter.WriteLine(AccessibilityKeyword(declaration.Class.Accessibility) + " partial interface " + declaration.InterfaceName + " : " +
                               declaration.MarkerInterface.FullyQualifiedName);
        sourceWriter.OpenBlock();
        for (var index = 0; index < declaration.Methods.Count; index++)
        {
            if (index > 0)
            {
                sourceWriter.WriteLine();
            }

            var method = declaration.Methods[index];
            sourceWriter.WriteDocumentation(method.Documentation);
            var signature = method.ReturnType.FullyQualifiedName + " " + method.Name + TypeParameterList(method) + "(" + InterfaceParameterList(method) + ")";
            if (method.TypeParameterConstraintClauses.Count == 0)
            {
                sourceWriter.WriteLine(signature + ";");
                continue;
            }

            sourceWriter.WriteLine(signature);
            WriteConstraintClauses(sourceWriter, method, ";");
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

    /// <summary><c>&lt;TKey, TResult&gt;</c> for a generic method; empty for any other.</summary>
    internal static string TypeParameterList(MethodDeclaration method)
    {
        return method.TypeParameters.Count == 0 ? "" : "<" + string.Join(", ", method.TypeParameters.Select(EscapedName)) + ">";
    }

    /// <summary>Each <c>where</c> clause on a line of its own, one level in; the last ends with <paramref name="lastLineEnd" />.</summary>
    internal static void WriteConstraintClauses(SourceWriter sourceWriter, MethodDeclaration method, string lastLineEnd)
    {
        for (var index = 0; index < method.TypeParameterConstraintClauses.Count; index++)
        {
            var lineEnd = index == method.TypeParameterConstraintClauses.Count - 1 ? lastLineEnd : "";
            sourceWriter.WriteLine(ConstraintClauseIndentation + method.TypeParameterConstraintClauses[index] + lineEnd);
        }
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

    /// <summary><c>ref </c>, <c>out </c> or <c>in </c>, as a parameter or an argument passed to it is written; empty for a value.</summary>
    internal static string RefKindPrefix(RefKind refKind)
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
