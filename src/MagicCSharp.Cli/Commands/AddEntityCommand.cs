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
        [Description("Owning domain, e.g. 'Orders'. Dots for a subdomain: 'Orders.Fulfilment'.")]
        public string? Domain { get; init; }

        [CommandOption("-k|--use-key")]
        [Description("Key by an unguessable string instead of a Snowflake id. Use when the key is public.")]
        [DefaultValue(false)]
        public bool ShouldUseKey { get; init; }

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

            if (!Naming.IsDottedPascal(Domain))
            {
                return ValidationResult.Error($"Domain must be PascalCase segments separated by dots: {Domain}");
            }

            // An .App holds controllers and the request and response types they use. Entities belong to
            // the domain itself, which is what the .App sits inside.
            return Domain!.EndsWith(".App", StringComparison.Ordinal)
                ? ValidationResult.Error($"Entities belong in the domain, not its HTTP surface. Use --domain {Domain[..^4]}")
                : ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = RepositoryConfig.Load();

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

        var entityPaths = new EntityPaths(config.Prefix, appName, domain, entity);

        Output.Plain($"App: {appName}   Domain: {domain}   Entity: {entity}");
        Output.Note($"Key: {(settings.ShouldUseKey ? "string" : "long (Snowflake)")}   Paginated: {settings.IsPaginated}");

        var missing = new[] { entityPaths.DomainModelsProject, entityPaths.DataProject, entityPaths.EntityFrameworkProject }
            .Where(path => !File.Exists(path))
            .ToList();

        if (missing.Count > 0)
        {
            foreach (var path in missing)
            {
                Output.Error($"Required project not found: {path}");
            }

            Output.Hint($"Create the domain first: mcs create-domain --solution {appName} --name {domain} --models");
            return 1;
        }

        var model = new
        {
            prefix = config.Prefix,
            app_name = appName,
            entity_name = entity,
            entity_variable = Naming.ToVariableName(entity),
            plural = Naming.Pluralize(entity),
            table_name = Naming.ToTableName(entity),
            use_key = settings.ShouldUseKey,
            is_paginated = settings.IsPaginated,
            entity_namespace = $"{config.Prefix}.{appName}.Domains.{domain}.Models.Entities",
        };

        // References go in before any file is written: if one fails, nothing has been generated and the
        // worktree is untouched.
        var added = new List<string>();
        try
        {
            foreach (var project in new[] { entityPaths.DataProject, entityPaths.EntityFrameworkProject })
            {
                if (DotnetCli.EnsureReference(project, entityPaths.DomainModelsProject))
                {
                    added.Add(project);
                }
            }
        }
        catch
        {
            foreach (var project in added)
            {
                DotnetCli.RemoveReference(project, entityPaths.DomainModelsProject);
            }

            throw;
        }

        var templateRenderer = new TemplateRenderer(TemplateResolver.ForRepository(config));

        templateRenderer.Render("Entities/entity.cs.hbs", entityPaths.EntityFile, model);
        templateRenderer.Render("Entities/repository-interface.cs.hbs", entityPaths.RepositoryInterfaceFile, model);
        templateRenderer.Render("Entities/dal.cs.hbs", entityPaths.DalFile, model);
        templateRenderer.Render("Entities/ef-repository.cs.hbs", entityPaths.EfRepositoryFile, model);

        RegisterDbSet(entityPaths);
        RegisterRepository(entityPaths);

        Output.Blank();
        Output.Success("Done. Next:");
        Output.Plain($"  1. Add the columns to {entityPaths.DalFile} and the fields to {entityPaths.EntityFile}");
        Output.Plain($"  2. Narrow on them in ApplyFilter in {entityPaths.EfRepositoryFile}");
        Output.Plain($"  3. dotnet ef migrations add Create{Naming.Pluralize(entity)}Table --project {entityPaths.EntityFrameworkDirectory}");

        return 0;
    }

    /// <summary>
    ///     Adds the DbSet. Without it the DAL is not part of the model and no migration is generated for its
    ///     table.
    /// </summary>
    private static void RegisterDbSet(EntityPaths entityPaths)
    {
        if (!File.Exists(entityPaths.ContextFile))
        {
            Output.Hint($"No context at {entityPaths.ContextFile} — add the DbSet by hand.");
            return;
        }

        var content = File.ReadAllText(entityPaths.ContextFile);

        if (content.Contains($"DbSet<{entityPaths.Entity}Dal>", StringComparison.Ordinal))
        {
            Output.Note($"DbSet already present in {entityPaths.ContextFile}");
            return;
        }

        content = SourceEdits.InsertBeforeLastBrace(content, $"    public DbSet<{entityPaths.Entity}Dal> {entityPaths.Plural} {{ get; set; }} = null!;");
        content = SourceEdits.EnsureUsing(content, $"{entityPaths.Prefix}.{entityPaths.AppName}.Data.EntityFramework.Dals");

        File.WriteAllText(entityPaths.ContextFile, content);
        Output.Updated(entityPaths.ContextFile, "DbSet");
    }

    /// <summary>
    ///     Registers the repository. Registration stays explicit rather than discovered, so the set of things
    ///     that talk to the database is readable in one file.
    /// </summary>
    private static void RegisterRepository(EntityPaths entityPaths)
    {
        if (!File.Exists(entityPaths.RepositoriesModuleFile))
        {
            Output.Hint($"No repositories module at {entityPaths.RepositoriesModuleFile} — register the repository by hand.");
            return;
        }

        var content = File.ReadAllText(entityPaths.RepositoriesModuleFile);

        if (content.Contains($"I{entityPaths.Plural}Repository", StringComparison.Ordinal))
        {
            Output.Note($"Repository already registered in {entityPaths.RepositoriesModuleFile}");
            return;
        }

        content = SourceEdits.InsertBefore(content, "return services;",
            $"services.AddScoped<I{entityPaths.Plural}Repository, {entityPaths.Plural}EfRepository>();");
        content = SourceEdits.EnsureUsing(content, $"{entityPaths.Prefix}.{entityPaths.AppName}.Data.Repositories");
        content = SourceEdits.EnsureUsing(content, $"{entityPaths.Prefix}.{entityPaths.AppName}.Data.EntityFramework.Repositories");

        File.WriteAllText(entityPaths.RepositoriesModuleFile, content);
        Output.Updated(entityPaths.RepositoriesModuleFile, "registration");
    }
}

/// <summary>Where each of an entity's files belongs, so the layout lives in one place.</summary>
public class EntityPaths(string prefix, string appName, string domain, string entity)
{
    private readonly string basePath = $"Apps/{appName}";

    public string Prefix { get; } = prefix;
    public string AppName { get; } = appName;
    public string Entity { get; } = entity;
    public string Plural { get; } = Naming.Pluralize(entity);

    /// <summary>Dots nest, as they do everywhere else: Orders.Fulfilment is Orders/Fulfilment.</summary>
    public string DomainModelsDirectory => $"{basePath}/{AppName}.Domains/{domain.Replace('.', '/')}/Models";
    public string DomainModelsProject => $"{DomainModelsDirectory}/{Prefix}.{AppName}.Domains.{domain}.Models.csproj";
    public string DataProject => $"{basePath}/Data/Data.Models/{Prefix}.{AppName}.Data.Models.csproj";
    public string EntityFrameworkDirectory => $"{basePath}/Data/Data.EntityFramework";
    public string EntityFrameworkProject => $"{EntityFrameworkDirectory}/{Prefix}.{AppName}.Data.EntityFramework.csproj";

    public string EntityFile => $"{DomainModelsDirectory}/Entities/{Entity}.cs";
    public string RepositoryInterfaceFile => $"{basePath}/Data/Data.Models/Repositories/I{Plural}Repository.cs";
    public string DalFile => $"{EntityFrameworkDirectory}/Dals/{Entity}Dal.cs";
    public string EfRepositoryFile => $"{EntityFrameworkDirectory}/Repositories/{Plural}EfRepository.cs";
    public string ContextFile => $"{EntityFrameworkDirectory}/Magic{AppName}Context.cs";
    public string RepositoriesModuleFile => $"{EntityFrameworkDirectory}/{AppName}RepositoriesModule.cs";
}
