# MagicCSharp.Templates

`dotnet new` template for a repository laid out the way MagicDoor runs its backend: several services in one
repository, each with its own solution, sharing a set of libraries.

**Optional.** The MagicCSharp packages work in any project structure. This is for when the shape fits.

```bash
dotnet new install MagicCSharp.Templates
dotnet new magiccsharp-repo -n Acme
cd Acme

dotnet tool restore                       # the CLI, pinned in .config/dotnet-tools.json
dotnet mcs create-app --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

`-n` is your namespace and solution-name root: `Acme` gives `Acme.Shop.slnx` and namespaces like
`Acme.Shop.Domains.Orders`.

| Option | Default | |
|---|---|---|
| `-n, --name` | — | Namespace and solution-name root |
| `--MagicCSharpVersion` | the version this template shipped with | MagicCSharp packages to pin |
| `--TargetFramework` | `net10.0` | `net10.0` or `net9.0` |

You get `magiccsharp.json`, `Directory.Build.props`, central package management, the all-projects solution,
`Apps/`, `Libs/`, and a tool manifest pinning `MagicCSharp.Cli` — so everyone on the repository runs the same
scaffolding after `dotnet tool restore`.

Requires the **.NET 10 SDK**, whatever the projects themselves target.

Full guide: https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/repository-layout.md
