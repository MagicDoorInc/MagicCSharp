namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Writes the <c>Executing:</c> trace line a use case logs before it does anything:
///     <c>Executing: chargeId={chargeId}, request={request}</c>, naming the parameters <see cref="LoggedParameters" />
///     chooses. The arguments go in an explicit array, so a lone array argument is logged as one value instead of
///     being spread over the placeholders.
/// </summary>
public static class ExecutingLogWriter
{
    /// <param name="sourceWriter">The method body being written.</param>
    /// <param name="loggerExpression">The <c>ILogger</c> to log to, as C#: <c>this.logger</c>.</param>
    /// <param name="method">The method whose parameters the line names.</param>
    public static void WriteLogCall(SourceWriter sourceWriter, string loggerExpression, MethodDeclaration method)
    {
        var loggedParameters = LoggedParameters.Choose(method);
        var message = ExecutingLineSyntax.ExecutingPrefix;
        if (loggedParameters.Count == 0)
        {
            sourceWriter.WriteLine("global::Microsoft.Extensions.Logging.LoggerExtensions.LogTrace(" + loggerExpression + ", \"" + message + "\");");
            return;
        }

        message += " " + string.Join(", ", loggedParameters.Select(parameter => parameter.Name + "={" + parameter.Name + "}"));
        var arguments = string.Join(", ", loggedParameters.Select(parameter => UseCaseInterfaceWriter.EscapedName(parameter.Name)));
        sourceWriter.WriteLine("global::Microsoft.Extensions.Logging.LoggerExtensions.LogTrace(" + loggerExpression + ", \"" + message + "\", new object?[] { " +
                               arguments + " });");
    }
}
