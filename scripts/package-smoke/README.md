# Package smoke check

A small app built only from packed MagicCSharp packages, never from this repository's source. It uses every
generator the way a consumer does and exits non-zero when one of them did not run or wrote the wrong thing:

- `[GenerateInterface]` and `[LogExecuting]` on a use case in `PackageSmoke.Library`, resolved through
  `AddMagicUseCases`, writing its `Executing:` line;
- the assembly loader (`MagicCSharpGenerateAssemblyLoader`), which loads `PackageSmoke.Library` before `Main`;
- a `partial` DAL mapped by convention, built through the repository's default `CreateDal`;
- a filtered repository whose `ApplyFilter` is generated, turned into SQL without a database;
- a filter's generated `Matches`, run through `InMemoryRepository`;
- `[MagicClient]` with its generated class and registration, calling a stubbed HTTP handler;
- `[GenerateFakeBase]`, whose unscripted member throws `NotScriptedException`;
- `[MigrationsDatabase]` and its generated design-time context factory.

`scripts/publish-all.sh` runs it against the packages it packed, with a NuGet cache of its own, so a locally
packed version never lands in the machine's shared cache. To run it by hand against a folder of packages:

```bash
NUGET_PACKAGES="$(mktemp -d)" dotnet run --project scripts/package-smoke/PackageSmoke \
  -p:MagicCSharpVersion=1.1.0 -p:RestoreAdditionalProjectSources="$PWD/nupkgs"
```

It is not part of `MagicCSharp.slnx`: the solution builds the packages from source, this checks what ships.
