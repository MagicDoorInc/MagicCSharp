namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>Writes the generated part of a <c>[GenerateDalMapping]</c> Dal from its <see cref="DalMappingPlan" />.</summary>
public static class DalMappingWriter
{
    private const string DalFromEditInterface = "global::MagicCSharp.Data.EntityFramework.Dals.IDalFromEdit";

    /// <summary>The generated part, or null when the plan generates nothing and the Dal needs no <c>IDalFromEdit</c>.</summary>
    public static GeneratedFile? WriteFile(DalDeclaration dal, DalMappingPlan plan)
    {
        var shouldImplementDalFromEdit = dal.CanAddDalFromEdit && (plan.From.IsGenerated || (dal.HasOwnFrom && dal.IsOwnFromPublic));
        var generatedMethodWriters = GeneratedMethodWriters(dal, plan);
        if (generatedMethodWriters.Count == 0 && !shouldImplementDalFromEdit)
        {
            return null;
        }

        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        if (dal.Dal.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + dal.Dal.Namespace + ";");
            sourceWriter.WriteLine();
        }

        var baseList = shouldImplementDalFromEdit
            ? " : " + DalFromEditInterface + "<" + dal.Dal.FullyQualifiedName + ", " + dal.Edit.FullyQualifiedName + ">"
            : "";
        var classKeyword = dal.Dal.Kind == TypeReferenceKind.Record ? "record" : "class";
        sourceWriter.WriteLine("partial " + classKeyword + " " + dal.Dal.Name + baseList);
        sourceWriter.OpenBlock();
        for (var index = 0; index < generatedMethodWriters.Count; index++)
        {
            if (index > 0)
            {
                sourceWriter.WriteLine();
            }

            generatedMethodWriters[index](sourceWriter);
        }

        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(dal.Dal, "DalMapping"),
            Text = sourceWriter.ToString(),
        };
    }

    private static List<Action<SourceWriter>> GeneratedMethodWriters(DalDeclaration dal, DalMappingPlan plan)
    {
        var generatedMethodWriters = new List<Action<SourceWriter>>();
        if (plan.ToEntity.IsGenerated)
        {
            generatedMethodWriters.Add(sourceWriter => WriteToEntity(sourceWriter, dal, plan.ToEntity));
        }

        if (plan.Apply.IsGenerated)
        {
            generatedMethodWriters.Add(sourceWriter => WriteApply(sourceWriter, dal, plan.Apply));
        }

        if (plan.From.IsGenerated)
        {
            generatedMethodWriters.Add(sourceWriter => WriteFrom(sourceWriter, dal, plan.From));
        }

        return generatedMethodWriters;
    }

    private static void WriteToEntity(SourceWriter sourceWriter, DalDeclaration dal, MethodPlan toEntity)
    {
        sourceWriter.WriteLine("public " + OverrideKeyword(dal.ShouldOverrideToEntity) + dal.Entity.FullyQualifiedName + " ToEntity()");
        sourceWriter.OpenBlock();
        var initializerLines = toEntity.Assignments.Select(propertyAssignment => propertyAssignment.TargetName + " = " + propertyAssignment.SourceName + ",");
        WriteObjectCreation(sourceWriter, "return new " + dal.Entity.FullyQualifiedName, initializerLines);
        sourceWriter.CloseBlock();
    }

    private static void WriteApply(SourceWriter sourceWriter, DalDeclaration dal, MethodPlan apply)
    {
        sourceWriter.WriteLine("public " + OverrideKeyword(dal.ShouldOverrideApply) + "void Apply(" + dal.Edit.FullyQualifiedName + " edit)");
        sourceWriter.OpenBlock();
        foreach (var propertyAssignment in apply.Assignments)
        {
            sourceWriter.WriteLine(propertyAssignment.TargetName + " = edit." + propertyAssignment.SourceName + ";");
        }

        sourceWriter.CloseBlock();
    }

    private static void WriteFrom(SourceWriter sourceWriter, DalDeclaration dal, MethodPlan fromPlan)
    {
        var isIdKeyed = dal.PrimaryKind == DalPrimaryKind.Id;
        var primaryParameter = isIdKeyed ? "long id" : "string key";
        var primaryAssignment = isIdKeyed ? "Id = id," : "Key = key,";
        var dalVariable = VariableNameOf(dal.Dal.Name);

        sourceWriter.WriteLine("public static " + dal.Dal.FullyQualifiedName + " From(" + dal.Edit.FullyQualifiedName + " edit, " + primaryParameter + ")");
        sourceWriter.OpenBlock();
        var initializerLines = new[] { primaryAssignment }.Concat(
            fromPlan.Assignments.Select(propertyAssignment => propertyAssignment.TargetName + " = edit." + propertyAssignment.SourceName + ","));
        WriteObjectCreation(sourceWriter, "var " + dalVariable + " = new " + dal.Dal.FullyQualifiedName, initializerLines);
        sourceWriter.WriteLine(dalVariable + ".Apply(edit);");
        sourceWriter.WriteLine("return " + dalVariable + ";");
        sourceWriter.CloseBlock();
    }

    // An object initializer ends in "};", which SourceWriter's blocks do not write, so its lines are indented here.
    private static void WriteObjectCreation(SourceWriter sourceWriter, string creation, IEnumerable<string> initializerLines)
    {
        sourceWriter.WriteLine(creation);
        sourceWriter.WriteLine("{");
        foreach (var initializerLine in initializerLines)
        {
            sourceWriter.WriteLine("    " + initializerLine);
        }

        sourceWriter.WriteLine("};");
    }

    private static string OverrideKeyword(bool isOverride)
    {
        return isOverride ? "override " : "";
    }

    // The Dal's name in camel case, as a hand-written From names it ('chargeDal'); never one of the parameters.
    private static string VariableNameOf(string dalName)
    {
        var variableName = char.ToLowerInvariant(dalName[0]) + dalName.Substring(1);
        if (variableName == "edit" || variableName == "id" || variableName == "key")
        {
            return variableName + "Row";
        }

        return variableName;
    }
}
