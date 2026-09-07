# MagicCSharp.Cli

`mcs` — scaffolding for a repository built on MagicCSharp.

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme
mcs create-app --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

Or pin it per repository, so the whole team gets the same version:

```bash
dotnet new tool-manifest
dotnet tool install MagicCSharp.Cli
# commit .config/dotnet-tools.json; teammates run: dotnet tool restore
```

| Command | |
|---|---|
| `mcs init --prefix Acme` | set the current directory up as a repository |
| `mcs create-app --name Shop --database shop` | a service |
| `mcs create-domain -s Shop -n Orders --models --tests` | a domain |
| `mcs create-domain -s Shop -n Orders.App --tests` | that domain's endpoints |
| `mcs create-app-lib -s Shop -n Processors --tests` | a library inside one service |
| `mcs create-lib --name Events --tests` | a shared library |
| `mcs add-entity -s Shop -d Orders -n Order --paginated` | an entity and its repository |
| `mcs sync` | rebuild the all-projects solution |
| `mcs validate` | lint the conventions the compiler cannot |
| `mcs templates list \| where \| eject` | see and override the generators' templates |

**Nothing is overwritten** — an existing file is reported and skipped. **Re-running changes nothing**, so
every command is safe to repeat.

## Your own templates

Everything generated comes from a template you can replace, one file at a time:

```bash
mcs templates list
mcs templates eject Entities/dal.cs.hbs
```

Across several repositories, keep the templates in a repository of their own and add it as a submodule at
`.magiccsharp/templates`. See
[template overrides](https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/template-overrides.md).

The structure this creates is optional; the MagicCSharp packages work in any layout. Full guide:
https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/repository-layout.md
