using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0006: every control-flow body is a braced block.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequiredBracesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0006";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Statement body must be a braced block",
        "The body of this '{0}' is not wrapped in braces", "Style", DiagnosticSeverity.Error, true,
        "A braceless body makes adding a second statement a silent bug, and makes the block boundary " +
        "depend on indentation rather than syntax. 'else if' chains and stacked 'using' statements are allowed.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeElse, SyntaxKind.ElseClause);
        context.RegisterSyntaxNodeAction(AnalyzeLoop, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
            SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.LockStatement);
        context.RegisterSyntaxNodeAction(AnalyzeNestable, SyntaxKind.UsingStatement, SyntaxKind.FixedStatement);
    }

    private static void AnalyzeIf(SyntaxNodeAnalysisContext context)
    {
        var statement = (IfStatementSyntax)context.Node;
        Report(context, statement.Statement, statement.IfKeyword);
    }

    private static void AnalyzeElse(SyntaxNodeAnalysisContext context)
    {
        var clause = (ElseClauseSyntax)context.Node;
        if (clause.Statement.IsKind(SyntaxKind.IfStatement))
        {
            return;
        }

        Report(context, clause.Statement, clause.ElseKeyword);
    }

    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context)
    {
        var statement = (StatementSyntax)context.Node;
        var body = GetBody(statement);
        if (body == null)
        {
            return;
        }

        Report(context, body, statement.GetFirstToken());
    }

    private static void AnalyzeNestable(SyntaxNodeAnalysisContext context)
    {
        var statement = (StatementSyntax)context.Node;
        var body = GetBody(statement);
        if (body == null)
        {
            return;
        }

        if (body.IsKind(statement.Kind()))
        {
            return;
        }

        Report(context, body, statement.GetFirstToken());
    }

    private static StatementSyntax? GetBody(StatementSyntax statement)
    {
        var forStatement = statement as ForStatementSyntax;
        if (forStatement != null)
        {
            return forStatement.Statement;
        }

        var forEachStatement = statement as CommonForEachStatementSyntax;
        if (forEachStatement != null)
        {
            return forEachStatement.Statement;
        }

        var whileStatement = statement as WhileStatementSyntax;
        if (whileStatement != null)
        {
            return whileStatement.Statement;
        }

        var doStatement = statement as DoStatementSyntax;
        if (doStatement != null)
        {
            return doStatement.Statement;
        }

        var lockStatement = statement as LockStatementSyntax;
        if (lockStatement != null)
        {
            return lockStatement.Statement;
        }

        var usingStatement = statement as UsingStatementSyntax;
        if (usingStatement != null)
        {
            return usingStatement.Statement;
        }

        var fixedStatement = statement as FixedStatementSyntax;
        return fixedStatement != null ? fixedStatement.Statement : null;
    }

    private static void Report(SyntaxNodeAnalysisContext context, StatementSyntax body, SyntaxToken keyword)
    {
        if (body.IsKind(SyntaxKind.Block))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation(), keyword.ValueText));
    }
}
