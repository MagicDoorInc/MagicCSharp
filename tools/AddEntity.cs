#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console.Cli@0.50.0
#:package Scriban@7.2.5

// Scaffolds one entity across the four files it needs: the entity/edit/filter triple in the domain, the
// repository interface in Data, and the DAL and EF repository in Data.EntityFramework. Then registers the
// repository and adds the DbSet.
//
// Nothing here is magic at runtime — the output is ordinary code you own and edit. The script exists because
// the four files have to agree on names, namespaces and generic arguments, and getting one wrong produces a
// compiler error a few layers away from the mistake.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<AddEntityCommand>();
app.Configure(config =>
{
    config.SetApplicationName("dotnet run tools/AddEntity.cs --");
    config.AddExample("--solution", "Acme.Shop.slnx", "--domain", "Orders", "--name", "Order", "--paginated");
    config.AddExample("-s", "Acme.Shop.slnx", "-d", "Access", "-n", "ApiKey", "--use-key");
});
return app.Run(args);

public class AddEntitySettings : CommandSettings
{
    [CommandOption("-s|--solution <SOLUTION>")]
    [Description("Solution file, e.g. 'Acme.Shop.slnx'")]
    public string? Solution { get; set; }

    [CommandOption("-n|--name <NAME>")]
    [Description("Entity name in PascalCase, e.g. 'Order'")]
    public string? Name { get; set; }

    [CommandOption("-d|--domain <DOMAIN>")]
    [Description("Owning domain, e.g. 'Orders'")]
    public string? Domain { get; set; }

    [CommandOption("-k|--use-key")]
    [Description("Key the entity by an unguessable string instead of a Snowflake id. Use when the key is public.")]
    [DefaultValue(false)]
    public bool UseKey { get; set; }

    [CommandOption("-p|--paginated")]
    [Description("Also give the repository page-at-a-time reads")]
    [DefaultValue(false)]
    public bool IsPaginated { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Solution))
        {
            return ValidationResult.Error("Solution file is required. Use --solution <FILE>");
        }

        if (!File.Exists(Solution) || !Solution.EndsWith(".slnx"))
        {
            return ValidationResult.Error($"Solution file not found or not a .slnx: {Solution}");
        }

        if (string.IsNullOrWhiteSpace(Name) || !Regex.IsMatch(Name, "^[A-Z][0-9a-zA-Z]+$"))
        {
            return ValidationResult.Error($"Entity name must be PascalCase with no dots: {Name}");
        }

        if (string.IsNullOrWhiteSpace(Domain) || !Regex.IsMatch(Domain, "^[A-Z][0-9a-zA-Z]+$"))
        {
            return ValidationResult.Error($"Domain must be PascalCase with no dots: {Domain}");
        }

        return ValidationResult.Success();
    }
}

public class AddEntityCommand : AsyncCommand<AddEntitySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AddEntitySettings settings)
    {
        var config = RepoConfig.Load();
        if (config == null)
        {
            return 1;
        }

        var solution = settings.Solution!;
        var entity = settings.Name!;
        var domain = settings.Domain!;

        // "Acme.Shop.slnx" -> "Shop"
        var appName = Path.GetFileName(solution).Replace($"{config.Prefix}.", "").Replace(".slnx", "");

        var paths = new EntityPaths(config.Prefix, appName, domain, entity);

        AnsiConsole.MarkupLine($"[green]App:[/] {appName}   [green]Domain:[/] {domain}   [green]Entity:[/] {entity}");
        AnsiConsole.MarkupLine($"[grey]Key: {(settings.UseKey ? "string" : "long (Snowflake)")}   Paginated: {settings.IsPaginated}[/]");

        var required = new[]
        {
            paths.DomainModelsProject,
            paths.DataProject,
            paths.EntityFrameworkProject,
        };

        var missing = required.Where(path => !File.Exists(path)).ToList();
        if (missing.Count > 0)
        {
            foreach (var path in missing)
            {
                AnsiConsole.MarkupLine($"[red]Required project not found:[/] {Markup.Escape(path)}");
            }

            AnsiConsole.MarkupLine(
                $"[yellow]Create the domain first:[/] dotnet run tools/CreateAppLib.cs -- --solution {Markup.Escape(solution)} --name Domains.{Markup.Escape(domain)} --models");
            return 1;
        }

        var model = new EntityModel
        {
            Prefix = config.Prefix,
            AppName = appName,
            EntityName = entity,
            Plural = Naming.Pluralize(entity),
            TableName = Naming.ToTableName(entity),
            UseKey = settings.UseKey,
            IsPaginated = settings.IsPaginated,
            EntityNamespace = $"{config.Prefix}.{appName}.Domains.{domain}.Models.Entities",
        };

        // Add the project references before writing files: if one fails, nothing has been generated yet and
        // the worktree is untouched.
        var added = new List<string>();
        try
        {
            foreach (var project in new[] { paths.DataProject, paths.EntityFrameworkProject })
            {
                if (await Dotnet.EnsureReference(project, paths.DomainModelsProject))
                {
                    added.Add(project);
                }
            }
        }
        catch
        {
            foreach (var project in added)
            {
                await Dotnet.RemoveReference(project, paths.DomainModelsProject);
            }

            throw;
        }

        var written = new List<string>();
        written.Add(await Templates.Render("entity.cs.hbs", paths.EntityFile, model));
        written.Add(await Templates.Render("repository-interface.cs.hbs", paths.RepositoryInterfaceFile, model));
        written.Add(await Templates.Render("dal.cs.hbs", paths.DalFile, model));
        written.Add(await Templates.Render("ef-repository.cs.hbs", paths.EfRepositoryFile, model));

        foreach (var file in written.Where(path => path.Length > 0))
        {
            AnsiConsole.MarkupLine($"  [green]created[/] {Markup.Escape(file)}");
        }

        await RegisterDbSet(paths.ContextFile, model);
        await RegisterRepository(paths.RepositoriesModuleFile, model);

        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[bold green]Done.[/] Next:");
        AnsiConsole.MarkupLine($"  1. Add the columns to [blue]{Markup.Escape(paths.DalFile)}[/] and the fields to [blue]{Markup.Escape(paths.EntityFile)}[/]");
        AnsiConsole.MarkupLine($"  2. Narrow on them in ApplyFilter in [blue]{Markup.Escape(paths.EfRepositoryFile)}[/]");
        AnsiConsole.MarkupLine($"  3. dotnet ef migrations add Create{model.Plural}Table --project {Markup.Escape(paths.EntityFrameworkDirectory)}");

        return 0;
    }

    /// <summary>
    ///     Adds the DbSet to the context. Without it the DAL is not part of the model and no migration is
    ///     generated for its table.
    /// </summary>
    private static async Task RegisterDbSet(string contextFile, EntityModel model)
    {
        if (!File.Exists(contextFile))
        {
            AnsiConsole.MarkupLine($"[yellow]No context at {Markup.Escape(contextFile)} — add the DbSet by hand.[/]");
            return;
        }

        var content = await File.ReadAllTextAsync(contextFile);
        var declaration = $"    public DbSet<{model.EntityName}Dal> {model.Plural} {{ get; set; }} = null!;";

        if (content.Contains($"DbSet<{model.EntityName}Dal>"))
        {
            AnsiConsole.MarkupLine($"  [grey]DbSet already present in {Markup.Escape(contextFile)}[/]");
            return;
        }

        var lastBrace = content.LastIndexOf('}');
        if (lastBrace < 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not find where to add the DbSet in {Markup.Escape(contextFile)}.[/]");
            return;
        }

        // Insert before the closing brace of the class, keeping whatever is already there.
        var updated = content[..lastBrace].TrimEnd() + Environment.NewLine + Environment.NewLine + declaration + Environment.NewLine + content[lastBrace..];

        updated = SourceEdits.EnsureUsing(updated, $"{model.Prefix}.{model.AppName}.Data.EntityFramework.Dals");

        await File.WriteAllTextAsync(contextFile, updated);
        AnsiConsole.MarkupLine($"  [green]updated[/] {Markup.Escape(contextFile)} (DbSet)");
    }

    /// <summary>
    ///     Registers the repository. Repository registration stays explicit rather than discovered, so the
    ///     set of things that talk to the database is readable in one file.
    /// </summary>
    private static async Task RegisterRepository(string modulePath, EntityModel model)
    {
        if (!File.Exists(modulePath))
        {
            AnsiConsole.MarkupLine($"[yellow]No repositories module at {Markup.Escape(modulePath)} — register the repository by hand.[/]");
            return;
        }

        var content = await File.ReadAllTextAsync(modulePath);
        var registration = $"        services.AddScoped<I{model.Plural}Repository, {model.Plural}EfRepository>();";

        if (content.Contains($"I{model.Plural}Repository"))
        {
            AnsiConsole.MarkupLine($"  [grey]Repository already registered in {Markup.Escape(modulePath)}[/]");
            return;
        }

        var anchor = content.IndexOf("return services;", StringComparison.Ordinal);
        if (anchor < 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not find where to register in {Markup.Escape(modulePath)}.[/]");
            return;
        }

        var updated = content[..anchor] + registration + Environment.NewLine + Environment.NewLine + "        " + content[anchor..];

        // The module lives in ...Data.EntityFramework, so neither the interface nor the implementation is in
        // scope without these.
        updated = SourceEdits.EnsureUsing(updated, $"{model.Prefix}.{model.AppName}.Data.Repositories");
        updated = SourceEdits.EnsureUsing(updated, $"{model.Prefix}.{model.AppName}.Data.EntityFramework.Repositories");

        await File.WriteAllTextAsync(modulePath, updated);
        AnsiConsole.MarkupLine($"  [green]updated[/] {Markup.Escape(modulePath)} (registration)");
    }
}

public record EntityModel
{
    public required string Prefix { get; init; }
    public required string AppName { get; init; }
    public required string EntityName { get; init; }
    public required string Plural { get; init; }
    public required string TableName { get; init; }
    public required bool UseKey { get; init; }
    public required bool IsPaginated { get; init; }
    public required string EntityNamespace { get; init; }
}

public class EntityPaths(string prefix, string appName, string domain, string entity)
{
    private readonly string basePath = $"Apps/{appName}";

    public string DomainModelsDirectory => $"{basePath}/{appName}.Domains/{domain}/Models";
    public string DomainModelsProject => $"{DomainModelsDirectory}/{prefix}.{appName}.Domains.{domain}.Models.csproj";
    public string DataProject => $"{basePath}/Data/Data.Models/{prefix}.{appName}.Data.Models.csproj";
    public string EntityFrameworkDirectory => $"{basePath}/Data/Data.EntityFramework";
    public string EntityFrameworkProject => $"{EntityFrameworkDirectory}/{prefix}.{appName}.Data.EntityFramework.csproj";

    public string EntityFile => $"{DomainModelsDirectory}/Entities/{entity}.cs";
    public string RepositoryInterfaceFile => $"{basePath}/Data/Data.Models/Repositories/I{Naming.Pluralize(entity)}Repository.cs";
    public string DalFile => $"{EntityFrameworkDirectory}/Dals/{entity}Dal.cs";
    public string EfRepositoryFile => $"{EntityFrameworkDirectory}/Repositories/{Naming.Pluralize(entity)}EfRepository.cs";
    public string ContextFile => $"{EntityFrameworkDirectory}/Magic{appName}Context.cs";
    public string RepositoriesModuleFile => $"{EntityFrameworkDirectory}/{appName}RepositoriesModule.cs";
}

public static class SourceEdits
{
    /// <summary>
    ///     Adds a using directive if the file does not already have it, placed after the existing ones so the
    ///     block stays together and sorted-ish rather than accumulating at the top of the file.
    /// </summary>
    public static string EnsureUsing(string content, string namespaceName)
    {
        var directive = $"using {namespaceName};";

        if (content.Contains(directive, StringComparison.Ordinal))
        {
            return content;
        }

        var lines = content.Split(Environment.NewLine).ToList();
        var lastUsing = lines.FindLastIndex(line => line.StartsWith("using ", StringComparison.Ordinal));

        if (lastUsing < 0)
        {
            // No usings yet: put it at the top, followed by a blank line.
            lines.Insert(0, "");
            lines.Insert(0, directive);
            return string.Join(Environment.NewLine, lines);
        }

        // Keep the block alphabetical, which is what the editorconfig and every other file in the repo do.
        var insertAt = lines.FindIndex(0, lastUsing + 1, line =>
            line.StartsWith("using ", StringComparison.Ordinal) && string.CompareOrdinal(line, directive) > 0);

        lines.Insert(insertAt < 0 ? lastUsing + 1 : insertAt, directive);

        return string.Join(Environment.NewLine, lines);
    }
}

public static class Naming
{
    public static string Pluralize(string input)
    {
        if (input.EndsWith('y') && input.Length > 1 && !"aeiou".Contains(char.ToLowerInvariant(input[^2])))
        {
            return input[..^1] + "ies";
        }

        if (input.EndsWith('s') || input.EndsWith('x') || input.EndsWith('z') || input.EndsWith("ch") || input.EndsWith("sh"))
        {
            return input + "es";
        }

        return input + "s";
    }

    /// <summary>PascalCase to plural snake_case, which is the table naming the DAL templates assume.</summary>
    public static string ToTableName(string input)
    {
        var snake = Regex.Replace(input, "([A-Z])", match => "_" + match.Value.ToLowerInvariant()).TrimStart('_');
        return Pluralize(snake);
    }
}

public static class Templates
{
    /// <summary>
    ///     Renders a template to a path, and refuses to overwrite. Regenerating over a file you have edited
    ///     would silently throw the edits away, so an existing file is reported and skipped.
    /// </summary>
    public static async Task<string> Render(string templateName, string targetPath, EntityModel model)
    {
        var templatePath = Path.Combine("tools", "Templates", "Entities", templateName);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}");
        }

        if (File.Exists(targetPath))
        {
            AnsiConsole.MarkupLine($"  [yellow]exists, skipped[/] {Markup.Escape(targetPath)}");
            return "";
        }

        var template = Template.Parse(await File.ReadAllTextAsync(templatePath));

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        var templateContext = new TemplateContext();
        templateContext.PushGlobal(scriptObject);

        var rendered = await template.RenderAsync(templateContext);

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllTextAsync(targetPath, rendered);

        return targetPath;
    }
}

public static class Dotnet
{
    /// <summary>Adds a project reference unless it is already there. Returns whether it added one.</summary>
    public static async Task<bool> EnsureReference(string project, string reference)
    {
        var existing = await Run("list", project, "reference");
        if (existing.Contains(Path.GetFileName(reference), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await Run("add", project, "reference", reference);
        AnsiConsole.MarkupLine($"  [green]referenced[/] {Markup.Escape(Path.GetFileName(reference))} from {Markup.Escape(Path.GetFileName(project))}");
        return true;
    }

    public static Task RemoveReference(string project, string reference)
    {
        return Run("remove", project, "reference", reference);
    }

    private static async Task<string> Run(params string[] arguments)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed: {error}{output}");
        }

        return output;
    }
}

/// <summary>
///     Reads magiccsharp.json from the working directory. Its presence is what marks a directory as the root
///     of a MagicCSharp-layout repository.
/// </summary>
public record RepoConfig
{
    public const string FileName = "magiccsharp.json";

    public required string Prefix { get; init; }

    public static RepoConfig? Load()
    {
        if (!File.Exists(FileName))
        {
            AnsiConsole.MarkupLine($"[red]{FileName} not found.[/]");
            AnsiConsole.MarkupLine("[yellow]Run this from the repository root. Create the file with:[/]");
            AnsiConsole.WriteLine("""  { "prefix": "Acme" }""");
            return null;
        }

        var config = JsonSerializer.Deserialize<RepoConfig>(File.ReadAllText(FileName), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (config == null || string.IsNullOrWhiteSpace(config.Prefix))
        {
            AnsiConsole.MarkupLine($"[red]{FileName} has no \"prefix\".[/]");
            return null;
        }

        return config;
    }
}
