using MagicCSharp.Analyzers;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Writes the decorator <c>[LogExecuting]</c> asks for: a <c>file</c>-scoped class, so no hand-written type can
///     collide with it and nothing can call it by name, that implements the use case's interface, logs the
///     <c>Executing:</c> line under the use case's own logger category before each <c>Execute</c> and calls the use
///     case; every other interface method is passed straight through. It carries
///     <c>[RegisteredInPlaceOf]</c>, so use case discovery registers it for the interface in the class's place.
/// </summary>
internal static class ExecutingLogDecoratorWriter
{
    private const string LoggerTypePrefix = "global::Microsoft.Extensions.Logging.ILogger<";

    public static GeneratedFile WriteFile(TypeDeclaration useCaseClass, DecoratedInterface decoratedInterface)
    {
        var decoratorName = useCaseClass.Name + "ExecutingLogDecorator";
        var useCaseFieldName = UseCaseInterfaceWriter.EscapedName(IdentifierWords.ToCamelCase(useCaseClass.Name));
        var loggerType = LoggerTypePrefix + useCaseClass.FullyQualifiedName + ">";

        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        if (useCaseClass.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + useCaseClass.Namespace + ";");
            sourceWriter.WriteLine();
        }

        sourceWriter.WriteLine("/// <summary>Writes the Executing: line of " + useCaseClass.Name + ", then calls it; registered for its interface in its place.</summary>");
        sourceWriter.WriteLine("[global::MagicCSharp.Modules.RegisteredInPlaceOf(typeof(" + useCaseClass.FullyQualifiedName + "))]");
        sourceWriter.WriteLine("file sealed class " + decoratorName + " : " + decoratedInterface.FullyQualifiedName);
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("private readonly " + decoratedInterface.FullyQualifiedName + " " + useCaseFieldName + ";");
        sourceWriter.WriteLine();
        sourceWriter.WriteLine("private readonly " + loggerType + " logger;");
        sourceWriter.WriteLine();
        sourceWriter.WriteLine("public " + decoratorName + "(" + useCaseClass.FullyQualifiedName + " " + useCaseFieldName + ", " + loggerType + " logger)");
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("this." + useCaseFieldName + " = " + useCaseFieldName + ";");
        sourceWriter.WriteLine("this.logger = logger;");
        sourceWriter.CloseBlock();
        foreach (var methodDeclaration in decoratedInterface.Methods)
        {
            sourceWriter.WriteLine();
            WriteMethod(sourceWriter, methodDeclaration, useCaseFieldName);
        }

        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(useCaseClass, "ExecutingLog"),
            Text = sourceWriter.ToString(),
        };
    }

    /// <summary>
    ///     Whether the decorator writes the <c>Executing:</c> line before calling <paramref name="method" />: only on
    ///     <c>Execute</c>, the call that runs the use case. Any other method (a read such as <c>GetByLease</c>) is
    ///     passed through.
    /// </summary>
    public static bool IsLogged(MethodDeclaration method)
    {
        return method.Name == UseCaseDeclaration.ExecuteMethodName;
    }

    private static void WriteMethod(SourceWriter sourceWriter, MethodDeclaration method, string useCaseFieldName)
    {
        var typeParameterList = UseCaseInterfaceWriter.TypeParameterList(method);
        sourceWriter.WriteLine("public " + method.ReturnType.FullyQualifiedName + " " + method.Name + typeParameterList + "(" +
                               UseCaseInterfaceWriter.ParameterList(method) + ")");
        UseCaseInterfaceWriter.WriteConstraintClauses(sourceWriter, method, "");
        sourceWriter.OpenBlock();
        if (IsLogged(method))
        {
            ExecutingLogWriter.WriteLogCall(sourceWriter, "this.logger", method);
        }

        var arguments = string.Join(", ", method.Parameters.Select(parameter =>
            UseCaseInterfaceWriter.RefKindPrefix(parameter.RefKind) + UseCaseInterfaceWriter.EscapedName(parameter.Name)));
        var call = "this." + useCaseFieldName + "." + method.Name + typeParameterList + "(" + arguments + ");";
        sourceWriter.WriteLine(method.ReturnType.FullyQualifiedName == "void" ? call : "return " + call);
        sourceWriter.CloseBlock();
    }
}
