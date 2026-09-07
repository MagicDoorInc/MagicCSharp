using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Scaffolds one entity across the four files it needs, then registers it.
///     <para>
///         Nothing here is magic at runtime — the output is ordinary code you own and edit. The command
///         exists because the four files have to agree on names, namespaces and generic arguments, and
///         getting one wrong produces a compiler error a few layers from the mistake.
///     </para>
/// </summary>
public class AddEntityCommand : Command<AddEntityCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-s|--solution <SERVICE>")]
        [Description("Which service, e.g. 'Shop'. Omit when the repository has only one.")]
        public string? Solution { get; init; }

        [CommandOption("-n|--name <NAME>")]
        [Description("Entity name in PascalCase, e.g. 'Order'")]
        public string? Name { get; init; }

        [CommandOption("-d|--domain <DOMAIN>")]
        [Description("Owning domain, e.g. 'Orders'")]
        public string? Domain { get; init; }

        [CommandOption("-k|--use-key")]
        [Description("Key by an unguessable string instead of a Snowflake id. Use when the key is public.")]
        [DefaultValue(false)]
        public bool UseKey { get; init; }

        [CommandOption("-p|--paginated")]
        [Description("Also give the repository page-at-a-time reads")]
        [DefaultValue(false)]
        public bool IsPaginated { get; init; }

        public override ValidationResult Validate()
        {
            // The solution is not checked here: resolving a service name, or falling back to the only
            // service there is, needs the repository config, which is not loaded until Execute.
            if (!Naming.IsPascalWord(Name))
            {
                return ValidationResult.Error($"Entity name must be PascalCase with no dots: {Name}");
            }

            return Naming.IsPascalWord(Domain)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Domain must be PascalCase with no dots: {Domain}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = RepoConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var solution = SolutionArgument.Resolve(config, settings.Solution);

        if (solution == null)
        {
            return 1;
        }

        var entity = settings.Name!;
        var domain = settings.Domain!;
        var appName = config.AppNameFromSolution(solution);

        var paths = new EntityPaths(config.Prefix, appName, domain, entity);

        Output.Plain($"App: {appName}   Domain: {domain}   Entity: {entity}");
        Output.Note($"Key: {(settings.UseKey ? "string" : "long (Snowflake)")}   Paginated: {settings.IsPaginated}");

        var missing = new[] { paths.DomainModelsProject, paths.DataProject, paths.EntityFrameworkProject }
            .Where(path => !File.Exists(path))
            .ToList();

        if (missing.Count > 0)
        {
            foreach (var path in missing)
            {
                Output.Error($"Required project not found: {path}");
            }

            Output.Hint($"Create the domain first: mcs create-domain --solution {appName} --name Domains.{domain} --models");
            return 1;
        }

        var model = new
        {
            prefix = config.Prefix,
            app_name = appName,
            entity_name = entity,
            plural = Naming.Pluralize(entity),
            table_name = Naming.ToTableName(entity),
            use_key = settings.UseKey,
            is_paginated = settings.IsPaginated,
            entity_namespace = $"{config.Prefix}.{appName}.Domains.{domain}.Models.Entities",
        };

        // References go in before any file is written: if one fails, nothing has been generated and the
        // worktree is untouched.
        var added = new List<string>();
        try
        {
            foreach (var project in new[] { paths.DataProject, paths.EntityFrameworkProject })
            {
                if (DotnetCli.EnsureReference(project, paths.DomainModelsProject))
                {
                    added.Add(project);
                }
            }
        }
        catch
        {
            foreach (var project in added)
            {
                DotnetCli.RemoveReference(project, paths.DomainModelsProject);
            }

            throw;
        }

        var renderer = new TemplateRenderer(TemplateResolver.ForRepository(config));

        renderer.Render("Entities/entity.cs.hbs", paths.EntityFile, model);
        renderer.Render("Entities/repository-interface.cs.hbs", paths.RepositoryInterfaceFile, model);
        renderer.Render("Entities/dal.cs.hbs", paths.DalFile, model);
        renderer.Render("Entities/ef-repository.cs.hbs", paths.EfRepositoryFile, model);

        RegisterDbSet(paths, config.Prefix, appName, entity, Naming.Pluralize(entity));
        RegisterRepository(paths, config.Prefix, appName, Naming.Pluralize(entity));

        Output.Blank();
        Output.Success("Done. Next:");
        Output.Plain($"  1. Add the columns to {paths.DalFile} and the fields to {paths.EntityFile}");
        Output.Plain($"  2. Narrow on them in ApplyFilter in {paths.EfRepositoryFile}");
        Output.Plain($"  3. dotnet ef migrations add Create{Naming.Pluralize(entity)}Table --project {paths.EntityFrameworkDirectory}");

        return 0;
    }

    /// <summary>
    ///     Adds the DbSet. Without it the DAL is not part of the model and no migration is generated for its
    ///     table.
    /// </summary>
    private static void RegisterDbSet(EntityPaths paths, string prefix, string appName, string entity, string plural)
    {
        if (!File.Exists(paths.ContextFile))
        {
            Output.Hint($"No context at {paths.ContextFile} — add the DbSet by hand.");
            return;
        }

        var content = File.ReadAllText(paths.ContextFile);

        if (content.Contains($"DbSet<{entity}Dal>", StringComparison.Ordinal))
        {
            Output.Note($"DbSet already present in {paths.ContextFile}");
            return;
        }

        content = SourceEdits.InsertBeforeLastBrace(content, $"    public DbSet<{entity}Dal> {plural} {{ get; set; }} = null!;");
        content = SourceEdits.EnsureUsing(content, $"{prefix}.{appName}.Data.EntityFramework.Dals");

        File.WriteAllText(paths.ContextFile, content);
        Output.Updated(paths.ContextFile, "DbSet");
    }

    /// <summary>
    ///     Registers the repository. Registration stays explicit rather than discovered, so the set of things
    ///     that talk to the database is readable in one file.
    /// </summary>
    private static void RegisterRepository(EntityPaths paths, string prefix, string appName, string plural)
    {
        if (!File.Exists(paths.RepositoriesModuleFile))
        {
            Output.Hint($"No repositories module at {paths.RepositoriesModuleFile} — register the repository by hand.");
            return;
        }

        var content = File.ReadAllText(paths.RepositoriesModuleFile);

        if (content.Contains($"I{plural}Repository", StringComparison.Ordinal))
        {
            Output.Note($"Repository already registered in {paths.RepositoriesModuleFile}");
            return;
        }

        content = SourceEdits.InsertBefore(content, "return services;",
            $"        services.AddScoped<I{plural}Repository, {plural}EfRepository>();");
        content = SourceEdits.EnsureUsing(content, $"{prefix}.{appName}.Data.Repositories");
        content = SourceEdits.EnsureUsing(content, $"{prefix}.{appName}.Data.EntityFramework.Repositories");

        File.WriteAllText(paths.RepositoriesModuleFile, content);
        Output.Updated(paths.RepositoriesModuleFile, "registration");
    }
}

/// <summary>Where each of an entity's files belongs, so the layout lives in one place.</summary>
public class EntityPaths(string prefix, string appName, string domain, string entity)
{
    private readonly string basePath = $"Apps/{appName}";
    private readonly string plural = Naming.Pluralize(entity);

    public string DomainModelsDirectory => $"{basePath}/{appName}.Domains/{domain}/Models";
    public string DomainModelsProject => $"{DomainModelsDirectory}/{prefix}.{appName}.Domains.{domain}.Models.csproj";
    public string DataProject => $"{basePath}/Data/Data.Models/{prefix}.{appName}.Data.Models.csproj";
    public string EntityFrameworkDirectory => $"{basePath}/Data/Data.EntityFramework";
    public string EntityFrameworkProject => $"{EntityFrameworkDirectory}/{prefix}.{appName}.Data.EntityFramework.csproj";

    public string EntityFile => $"{DomainModelsDirectory}/Entities/{entity}.cs";
    public string RepositoryInterfaceFile => $"{basePath}/Data/Data.Models/Repositories/I{plural}Repository.cs";
    public string DalFile => $"{EntityFrameworkDirectory}/Dals/{entity}Dal.cs";
    public string EfRepositoryFile => $"{EntityFrameworkDirectory}/Repositories/{plural}EfRepository.cs";
    public string ContextFile => $"{EntityFrameworkDirectory}/Magic{appName}Context.cs";
    public string RepositoriesModuleFile => $"{EntityFrameworkDirectory}/{appName}RepositoriesModule.cs";
}
