using System.Text;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Writes generated C# a line at a time, four-space indented, with <c>\n</c> line ends on every platform so the
///     output is the same wherever it is built. It writes blocks and nothing else: generated code has no
///     expression-bodied members.
/// </summary>
public sealed class SourceWriter
{
    private const string Indentation = "    ";

    private readonly StringBuilder text = new StringBuilder();

    private int depth;

    public void WriteLine(string line)
    {
        if (line.Length > 0)
        {
            for (var level = 0; level < depth; level++)
            {
                text.Append(Indentation);
            }

            text.Append(line);
        }

        text.Append('\n');
    }

    public void WriteLine()
    {
        text.Append('\n');
    }

    /// <summary>Writes <see cref="GeneratedFile.Header" /> and a blank line; every generated file starts with it.</summary>
    public void WriteFileHeader()
    {
        text.Append(GeneratedFile.Header);
        text.Append('\n');
    }

    public void OpenBlock()
    {
        WriteLine("{");
        depth++;
    }

    public void CloseBlock()
    {
        depth--;
        WriteLine("}");
    }

    public void WriteDocumentation(DocumentationComment documentation)
    {
        foreach (var line in documentation.Lines)
        {
            WriteLine("///" + line);
        }
    }

    public override string ToString()
    {
        return text.ToString();
    }
}
