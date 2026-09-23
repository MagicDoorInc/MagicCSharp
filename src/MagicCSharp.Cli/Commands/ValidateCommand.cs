using System.ComponentModel;
using System.Text.RegularExpressions;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

public record Violation(string Rule, string File, int Line, string Message, string Why);

public record SourceFile(string Path, string[] Lines)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string RelativePath => System.IO.Path.GetRelativePath(Directory.GetCurrentDirectory(), Path).Replace('\\', '/');
    public string Text { get; } = string.Join('\n', Lines);
}

/// <summary>
///     Checks the handful of conventions the compiler cannot, and that go wrong quietly.
///     <para>
///         Each rule is here because breaking it produces working code that fails later: a test that passes
///         today and fails at midnight, an event that deserializes to nulls after a deploy, a repository
///         whose writes are invisible in the log.
///     </para>
///     <para>
///         Regex rather than Roslyn, for now. Every rule is written to under-report rather than over-report —
///         a false positive you have to argue with is worse than a miss.
///     </para>
/// </summary>
public class ValidateCommand : Command<ValidateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--path <PATH>")]
        [Description("Directory to scan. Defaults to the working directory.")]
        [DefaultValue(".")]
        public string Path { get; init; } = ".";

        public override ValidationResult Validate()
        {
            return Directory.Exists(Path)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Path not found: {Path}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var root = System.IO.Path.GetFullPath(settings.Path);

        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !System.IO.Path.GetRelativePath(root, path)
                .Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj" or "Migrations" || segment.StartsWith('.')))
            .Select(path => new SourceFile(path, File.ReadAllLines(path)))
            .ToList();

        AnsiConsole.MarkupLine($"[grey]Scanning {files.Count} files under {Markup.Escape(settings.Path)}[/]");

        var violations = new List<Violation>();
        violations.AddRange(NoWallClock(files));
        violations.AddRange(EventPropertiesArePrimitive(files));
        violations.AddRange(NoIOptionsInUseCases(files));
        violations.AddRange(DalPropertiesAreRequired(files));

        if (violations.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No violations.[/]");
            return 0;
        }

        foreach (var group in violations.GroupBy(violation => violation.Rule))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold red]{Markup.Escape(group.Key)}[/] — {Markup.Escape(group.First().Why)}");

            foreach (var violation in group)
            {
                AnsiConsole.MarkupLine($"  {Markup.Escape(violation.File)}:{violation.Line}  {Markup.Escape(violation.Message)}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red]{violations.Count} violations in {violations.Select(v => v.File).Distinct().Count()} files.[/]");

        return 1;
    }

    /// <summary>
    ///     Reading the wall clock directly makes the behaviour untestable — you cannot write the test for
    ///     "a fee applies after thirty days" without waiting thirty days.
    /// </summary>
    private static IEnumerable<Violation> NoWallClock(IReadOnlyList<SourceFile> files)
    {
        var pattern = new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b");

        foreach (var file in files)
        {
            // A TimeProvider implementation is the one place allowed to read it.
            if (Regex.IsMatch(file.Text, @":\s*TimeProvider\b"))
            {
                continue;
            }

            foreach (var (line, number) in file.Lines.Select((line, index) => (line, index + 1)))
            {
                if (IsComment(line) || !pattern.IsMatch(line) || IsAllowed(line))
                {
                    continue;
                }

                yield return new Violation(
                    "Wall clock read directly",
                    file.RelativePath,
                    number,
                    line.Trim(),
                    "Inject TimeProvider and call GetUtcNow(), so a test can move time with FakeTimeProvider.");
            }
        }
    }

    /// <summary>
    ///     An event crosses a process boundary and is deserialized by code built from a different commit. A
    ///     property whose type is an entity ties the event's wire format to that entity's shape, so a field
    ///     added on one side arrives as null on the other. Ids and primitives keep the contract stable.
    /// </summary>
    private static IEnumerable<Violation> EventPropertiesArePrimitive(IReadOnlyList<SourceFile> files)
    {
        var primitives = new HashSet<string>(StringComparer.Ordinal)
        {
            "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong",
            "short", "ushort", "string", "object", "Guid", "DateTimeOffset", "DateTime", "DateOnly", "TimeOnly",
            "TimeSpan", "TimeZoneInfo",
        };

        var property = new Regex(@"^\s*public\s+(?:required\s+)?(?<type>[A-Za-z0-9_.<>\[\]?]+)\s+(?<name>[A-Za-z0-9_]+)\s*\{\s*(get|init|set)");

        foreach (var file in files.Where(file => file.Text.Contains(": MagicEvent") || file.Text.Contains(", MagicEvent")))
        {
            foreach (var (line, number) in file.Lines.Select((line, index) => (line, index + 1)))
            {
                var match = property.Match(line);
                if (!match.Success || IsComment(line))
                {
                    continue;
                }

                var type = match.Groups["type"].Value.TrimEnd('?');

                // Unwrap one level of collection: List<long> is as fine as long.
                var element = Regex.Match(type, @"^(?:List|IReadOnlyList|IList|ICollection|IEnumerable|HashSet)<(?<inner>.+)>$");
                if (element.Success)
                {
                    type = element.Groups["inner"].Value.TrimEnd('?');
                }

                // An enum is a name we cannot resolve here, so anything ending in an obvious enum-ish word is
                // let through rather than reported. Under-reporting on purpose.
                if (primitives.Contains(type) || type.EndsWith("Type") || type.EndsWith("Status") || type.EndsWith("Kind") ||
                    type.EndsWith("Reason") || type.EndsWith("Source"))
                {
                    continue;
                }

                yield return new Violation(
                    "Event carries a non-primitive property",
                    file.RelativePath,
                    number,
                    $"{match.Groups["name"].Value} is {match.Groups["type"].Value}",
                    "Events are deserialized by code from another commit. Carry ids and primitives; let the handler load the rest.");
            }
        }
    }

    /// <summary>
    ///     IOptions is a configuration-system type. A use case taking one cannot be constructed in a test
    ///     without building a configuration, and it hides what the use case actually needs behind a settings
    ///     bag that grows.
    /// </summary>
    private static IEnumerable<Violation> NoIOptionsInUseCases(IReadOnlyList<SourceFile> files)
    {
        var pattern = new Regex(@"\bIOptions(Snapshot|Monitor)?<");

        foreach (var file in files.Where(file => file.Name.EndsWith("UseCase.cs")))
        {
            foreach (var (line, number) in file.Lines.Select((line, index) => (line, index + 1)))
            {
                if (IsComment(line) || !pattern.IsMatch(line))
                {
                    continue;
                }

                yield return new Violation(
                    "IOptions injected into a use case",
                    file.RelativePath,
                    number,
                    line.Trim(),
                    "Inject the values themselves, or a small interface. A use case should be constructible in a test with no configuration.");
            }
        }
    }

    /// <summary>
    ///     A non-nullable DAL column needs <c>[Required]</c>, which is what tells EF to generate NOT NULL.
    ///     Without it EF infers a nullable column, then throws on read when the row legitimately holds null.
    ///     <para>
    ///         The C# <c>required</c> keyword is a different claim and is checked separately. It is right for a
    ///         column set once — the key, a parent id that never changes — because the object initializer in
    ///         <c>From()</c> is where that happens, and the keyword makes forgetting it a compile error. It is
    ///         wrong for a column <c>Apply(edit)</c> sets: the initializer cannot know those values, so the
    ///         keyword would force a meaningless placeholder. So <c>required</c> is accepted exactly when
    ///         <c>From()</c>'s initializer assigns the property and <c>Apply()</c> does not.
    ///     </para>
    /// </summary>
    internal static IEnumerable<Violation> DalPropertiesAreRequired(IReadOnlyList<SourceFile> files)
    {
        var property = new Regex(@"^\s*public\s+(?<required>required\s+)?(?<type>[A-Za-z0-9_.<>\[\]]+)\s+(?<name>[A-Za-z0-9_]+)\s*\{\s*get;\s*set;\s*\}\s*$");

        var typeDeclaration = new Regex(@"^\s*(?:public|internal|abstract|sealed|partial|\s)*\b(?<kind>interface|class|record|struct)\b");

        foreach (var file in files.Where(file => file.Name.EndsWith("Dal.cs")))
        {
            var inInterface = false;
            var setInFrom = AssignedIn(file, @"\bstatic\s+\w+\s+From\s*\(");
            var setInApply = AssignedIn(file, @"\bvoid\s+Apply\s*\(");

            for (var index = 0; index < file.Lines.Length; index++)
            {
                var line = file.Lines[index];

                var declaration = typeDeclaration.Match(line);
                if (declaration.Success && !IsComment(line))
                {
                    // An interface declares the shape; it carries no columns and cannot be annotated.
                    inInterface = declaration.Groups["kind"].Value == "interface";
                }

                var match = property.Match(line);

                if (!match.Success || IsComment(line) || inInterface)
                {
                    continue;
                }

                if (match.Groups["required"].Success)
                {
                    var name = match.Groups["name"].Value;
                    var isSetOnceInFrom = setInFrom.Contains(name) && !setInApply.Contains(name);

                    if (isSetOnceInFrom || HasAttributeAbove(file, index, "Key"))
                    {
                        continue;
                    }

                    yield return new Violation(
                        "DAL property is 'required' but not set once in From()",
                        file.RelativePath,
                        index + 1,
                        setInApply.Contains(name)
                            ? $"{name} is 'required' and Apply() assigns it"
                            : $"{name} is 'required' and From() does not assign it",
                        "'required' is for a column From()'s initializer sets once and Apply() leaves alone. For any other column use [Required].");
                    continue;
                }

                // Nullable and initialized properties already say what happens when nothing assigns them.
                if (line.Contains('?') || line.Contains('=') || match.Groups["type"].Value.Contains('<'))
                {
                    continue;
                }

                if (HasAttributeAbove(file, index, "Required") || HasAttributeAbove(file, index, "Key"))
                {
                    continue;
                }

                yield return new Violation(
                    "DAL column is non-nullable but not marked [Required]",
                    file.RelativePath,
                    index + 1,
                    $"{match.Groups["name"].Value} is {match.Groups["type"].Value}",
                    "Add [Required] so EF generates NOT NULL, or make the property nullable to match the column.");
            }
        }
    }

    /// <summary>
    ///     The names assigned (<c>Name = ...</c>) inside the body of the first method whose signature matches,
    ///     found by brace depth from that line. For <c>From()</c> that is the object initializer's members and
    ///     for <c>Apply()</c> its statements — a line-based read, which is all a DAL written from the template
    ///     needs.
    /// </summary>
    private static HashSet<string> AssignedIn(SourceFile file, string methodSignature)
    {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var signature = new Regex(methodSignature);
        var assignment = new Regex(@"^\s*(?:dal\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)");

        var start = Array.FindIndex(file.Lines, line => signature.IsMatch(line) && !IsComment(line));
        if (start < 0)
        {
            return assigned;
        }

        var depth = 0;
        var hasEnteredBody = false;

        for (var index = start; index < file.Lines.Length; index++)
        {
            var line = file.Lines[index];

            if (hasEnteredBody && !IsComment(line))
            {
                var match = assignment.Match(line);
                if (match.Success)
                {
                    assigned.Add(match.Groups["name"].Value);
                }
            }

            depth += line.Count(character => character == '{') - line.Count(character => character == '}');
            hasEnteredBody = hasEnteredBody || line.Contains('{');

            if (hasEnteredBody && depth <= 0)
            {
                break;
            }
        }

        return assigned;
    }

    /// <summary>
    ///     Whether the attribute block immediately above a property contains the given attribute. Walks up
    ///     through attribute and blank lines only, so it cannot pick up an attribute on a different member.
    /// </summary>
    private static bool HasAttributeAbove(SourceFile file, int propertyIndex, string attributeName)
    {
        for (var index = propertyIndex - 1; index >= 0; index--)
        {
            var line = file.Lines[index].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith('['))
            {
                return false;
            }

            if (line.Contains($"[{attributeName}]", StringComparison.Ordinal) || line.Contains($"[{attributeName}(", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith('*');
    }

    /// <summary>
    ///     A line ending in <c>// conventions: allow — reason</c> is exempt.
    ///     <para>
    ///         Every rule here has real exceptions — an event stamping its own creation time has no clock to
    ///         inject. Without a way to say so in the code, the alternative is a rule nobody trusts or a list
    ///         of special-cased files nobody maintains. The reason is required so the exemption argues for
    ///         itself in review.
    ///     </para>
    /// </summary>
    private static bool IsAllowed(string line)
    {
        var marker = line.IndexOf("// conventions: allow", StringComparison.Ordinal);

        if (marker < 0)
        {
            return false;
        }

        // Bare "allow" with nothing after it does not count; say why.
        return line[(marker + "// conventions: allow".Length)..].Trim(' ', '-', '—').Length > 0;
    }
}
