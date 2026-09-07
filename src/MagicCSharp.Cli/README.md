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
| `mcs create-domain -s Acme.Shop.slnx -n Domains.Orders --models --tests` | a domain |
| `mcs create-lib --name Events --tests` | a shared library |
| `mcs add-entity -s Acme.Shop.slnx -d Orders -n Order --paginated` | an entity and its repository |
| `mcs sync` | rebuild the all-projects solution |
| `mcs validate` | lint the conventions the compiler cannot |
| `mcs templates list \| where \| eject` | see and override the generators' templates |

**Nothing is overwritten** — an existing file is reported and skipped. **Re-running changes nothing**, so
every command is safe to repeat.

The structure this creates is optional; the MagicCSharp packages work in any layout. Full guide:
https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/repository-layout.md
