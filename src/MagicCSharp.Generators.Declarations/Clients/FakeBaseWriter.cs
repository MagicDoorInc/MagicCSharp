using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Writes a fake base: the part of the <c>[GenerateFakeBase]</c> class that implements the interface with throwing members.</summary>
internal static class FakeBaseWriter
{
    private const string NotScriptedException = "global::MagicCSharp.Testing.NotScriptedException";

    public static GeneratedFile WriteFile(FakeBaseDeclaration declaration)
    {
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        if (declaration.Class.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + declaration.Class.Namespace + ";");
            sourceWriter.WriteLine();
        }

        var typeParameters = declaration.Class.TypeParameters.Count == 0 ? "" : "<" + string.Join(", ", declaration.Class.TypeParameters) + ">";
        sourceWriter.WriteLine("partial class " + declaration.Class.Name + typeParameters + " : " + declaration.InterfaceFullyQualifiedName);
        sourceWriter.OpenBlock();
        for (var index = 0; index < declaration.Members.Count; index++)
        {
            if (index > 0)
            {
                sourceWriter.WriteLine();
            }

            WriteMember(sourceWriter, declaration.Members[index]);
        }

        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(declaration.Class, "FakeBase"),
            Text = sourceWriter.ToString(),
        };
    }

    private static void WriteMember(SourceWriter sourceWriter, FakeMember fakeMember)
    {
        switch (fakeMember.Kind)
        {
            case FakeMemberKind.Method:
                sourceWriter.WriteLine("public virtual " + fakeMember.TypeFullyQualifiedName + " " + fakeMember.Name + "(" + fakeMember.ParameterList + ")");
                WriteThrowingBody(sourceWriter, fakeMember);
                break;
            case FakeMemberKind.Property:
                sourceWriter.WriteLine("public virtual " + fakeMember.TypeFullyQualifiedName + " " + fakeMember.Name);
                WriteThrowingAccessors(sourceWriter, fakeMember);
                break;
            case FakeMemberKind.Indexer:
                sourceWriter.WriteLine("public virtual " + fakeMember.TypeFullyQualifiedName + " this[" + fakeMember.ParameterList + "]");
                WriteThrowingAccessors(sourceWriter, fakeMember);
                break;
            case FakeMemberKind.Event:
                sourceWriter.WriteLine("public virtual event " + fakeMember.TypeFullyQualifiedName + " " + fakeMember.Name);
                sourceWriter.OpenBlock();
                WriteThrowingAccessor(sourceWriter, "add", fakeMember);
                WriteThrowingAccessor(sourceWriter, "remove", fakeMember);
                sourceWriter.CloseBlock();
                break;
        }
    }

    private static void WriteThrowingAccessors(SourceWriter sourceWriter, FakeMember fakeMember)
    {
        sourceWriter.OpenBlock();
        if (fakeMember.HasGetter)
        {
            WriteThrowingAccessor(sourceWriter, "get", fakeMember);
        }

        if (fakeMember.SetterKind == SetterKind.Set)
        {
            WriteThrowingAccessor(sourceWriter, "set", fakeMember);
        }

        if (fakeMember.SetterKind == SetterKind.Init)
        {
            WriteThrowingAccessor(sourceWriter, "init", fakeMember);
        }

        sourceWriter.CloseBlock();
    }

    private static void WriteThrowingAccessor(SourceWriter sourceWriter, string accessor, FakeMember fakeMember)
    {
        sourceWriter.WriteLine(accessor);
        WriteThrowingBody(sourceWriter, fakeMember);
    }

    private static void WriteThrowingBody(SourceWriter sourceWriter, FakeMember fakeMember)
    {
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("throw new " + NotScriptedException + "(" + SymbolDisplay.FormatLiteral(fakeMember.ScriptedName, true) + ");");
        sourceWriter.CloseBlock();
    }
}
