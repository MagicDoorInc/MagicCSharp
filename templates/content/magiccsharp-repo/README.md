# Acme

Built on [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp).

## Getting started

The scaffolding is pinned in `.config/dotnet-tools.json`, so everyone gets the same version:

```bash
dotnet tool restore
dotnet mcs --help
```

## Common commands

```bash
dotnet mcs create-app --name Shop --database shop
dotnet mcs create-domain --solution Acme.Shop.slnx --name Domains.Orders --models --tests
dotnet mcs add-entity --solution Acme.Shop.slnx --domain Orders --name Order --paginated
dotnet mcs create-lib --name Events --tests

dotnet mcs validate          # lint the conventions the compiler cannot
dotnet mcs sync              # rebuild Acme.All.slnx after a merge conflict
```

## Layout

```
Apps/{Service}/
  {Service}.App/             host: Program.cs, controllers
  {Service}.Domains/{Domain}/
    Default/                 use cases, event handlers
    Models/                  entities, edits, filters
    Tests/
  Data/
    Data.Models/             repository interfaces — no EF dependency
    Data.EntityFramework/    DALs, repositories, context, migrations
Libs/                        code more than one service uses
```

Entities live in the domain that owns them; persistence lives in `Data/`. The arrow points from storage
toward the domain and never back.

Full guide: https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/repository-layout.md
