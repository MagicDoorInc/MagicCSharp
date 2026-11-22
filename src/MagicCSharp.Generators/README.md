# MagicCSharp.Generators

Source generators for a MagicCSharp codebase. You write the part only you know — a use case's `Execute`, a DAL's
columns, a filter's fields, a client's routes — and the compiler writes the repetitive part from it on every build:
the interface, the mapping, the query, the HTTP calls. The generated code is shown by the IDE and never committed.

```bash
dotnet add package MagicCSharp.Generators
```

Reference it with `PrivateAssets="all"`, in every project (a repository `mcs init` created already does, from its
`Directory.Build.props`). It writes nothing until a class asks for it, so adding it changes no existing code.

## What it writes, and where each is explained

| You write | The compiler writes | Guide |
|---|---|---|
| `[GenerateInterface]` on a `partial` use case class | its interface `IX : IMagicUseCase`, from the public `Execute` methods and their doc comments | [Use cases](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md#generated-interfaces) |
| `[LogExecuting]` on a use case | a decorator registered in its place that logs `Executing: name=value, …` at trace level, then calls it | [Use cases](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md#the-executing-line) |
| `[GenerateDalMapping]` on a `partial` DAL with only its columns | `ToEntity`, `Apply` and `From`, matched by name and type | [Data.EntityFramework](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.EntityFramework/README.md#generated-dal-mapping) |
| `[GenerateFilterQuery]` on a `partial` repository without `ApplyFilter` | `ApplyFilter`, one helper call per filter field | [Data](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data/README.md#generated-filters) |
| `[GenerateMatches]` on a `partial` filter listing `IEntityFilter<TEntity>` | `Matches(entity)`, the same rules in memory | [Data](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data/README.md#generated-filters) |
| `[MagicClient]` on an interface with route attributes | the client class and `services.Add{Name}(configuration)` | [Clients](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Clients/README.md) |
| `[GenerateFakeBase(typeof(IX))]` on an `abstract partial` test class | every member of `IX` as `virtual`, throwing `NotScriptedException` | [Testing](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing/README.md#a-generated-fake-base) |
| `MagicCSharpGenerateAssemblyLoader=true` in a host project | `AssemblyLoader`, which loads every referenced project before `Main` | [App](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.App/README.md#the-assembly-loader) |
| `[GenerateDesignTimeFactory]` and `[MigrationsDatabase("name")]` on a `DbContext` | its design-time factory for `dotnet ef`, against that database | [Data.EntityFramework](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.EntityFramework/README.md#migrations) |

Every generator runs only for a class that carries its attribute: making a class `partial`, or listing an interface such
as `IEntityFilter<TEntity>`, asks for nothing on its own. A DAL, repository or filter with its attribute must be
`partial`, even one that writes every member itself, or the build fails naming it; a `DbContext` need not be, because
its design-time factory is a class of its own.

The attributes live in the runtime package of what they mark (`[GenerateInterface]` and `[GenerateMatches]` in
`MagicCSharp`, `[GenerateDalMapping]`, `[GenerateFilterQuery]` and `[GenerateDesignTimeFactory]` in
`MagicCSharp.Data.EntityFramework`, `[MagicClient]` in `MagicCSharp.Clients`, `[GenerateFakeBase]` in
`MagicCSharp.Testing`), so the class that carries one already references it.

## Writing it by hand instead

Every generator leaves alone what you write yourself. A DAL that declares its own `Apply` gets only `ToEntity`, and
`From` too when it lists `IDalFromEdit<TDal, TEdit>` (a DAL that maps by hand, as one written before 1.1 does, gets no
`From` and no error about one); a repository that declares `ApplyFilter` gets none; a client method without a route attribute is yours to
write in the `partial` class. So the generated shape is the default, and a hand-written member is the exception that
says "this one is different": a column that converts its value, a filter that is not a plain comparison, a call that
skips the request for an empty list.

To leave the generated shape for a whole class, write what the compiler wrote — the interface, the method, the
client — and remove the attribute that asked for it. Callers do not change: the names are the same.
To see what to write, look at the generated file (below) and copy it.

When the generator cannot write something, the build fails naming the member and what to do (the rules below). Fix
the declaration or write that member by hand; do not turn the rule off, because the generated code would be missing.

## Seeing the generated files

The IDE lists them under the project's analyzers. To also write them to disk, set `EmitCompilerGeneratedFiles` to
`true`; they go under `obj/`. Generated files carry `// <auto-generated/>`, are rewritten on every build and are
never committed or edited.

An IDE rename of a member reaches the generated code on the next build, not through the rename itself: rename, build,
and follow any error to a name the generator read from yours.

## Settings

MSBuild properties, in `Directory.Build.props` or a project. A list is comma-separated, because the compiler reads
these values from an editorconfig, where `;` starts a comment.

| Property | Default | |
|---|---|---|
| `MagicCSharpUseCaseMarkerInterface` | `MagicCSharp.UseCases.IMagicUseCase` | The interface generated use case interfaces extend, for a codebase with its own marker |
| `MagicCSharpUseCaseCallerPrefixes` | (none) | Caller prefixes besides `External` and `Internal`, e.g. `Partner,Admin`, so `[LogExecuting]` refuses `PartnerGetInvoiceUseCase` as read-only |
| `MagicCSharpGenerateAssemblyLoader` | `false` | `true` in a host project writes its `AssemblyLoader` |
| `MagicCSharpDesignTimeContextFactoryBase` | ``MagicCSharp.Data.Postgres.MagicDbContextFactory`1`` | The class generated design-time factories derive from |

## Rules

Each refusal is a build error with its own id, naming what to change.

| Rule | Reported when |
|---|---|
| MCS1001 | A `[GenerateInterface]` class is not `partial` |
| MCS1002 | It has no public instance `Execute` method |
| MCS1003 | It is nested in another type |
| MCS1004 | It is generic |
| MCS1005 | It is static |
| MCS1006 | One of its `Execute` methods is generic |
| MCS1007 | The use case marker interface is not an interface the project can see |
| MCS1101 | A `[LogExecuting]` use case has a parameter that looks like a secret (password, secret, token or key, a stream, a byte array) without `[NotLogged]` |
| MCS1102 | A `[LogExecuting]` use case is named as read-only (Get, List, Search, Find, Is, Can, Count) |
| MCS1103 | A `[LogExecuting]` use case still writes its own `Executing:` line |
| MCS1104 | A `[LogExecuting]` class implements no use case interface |
| MCS1105 | It implements several |
| MCS1106 | It is generic |
| MCS1107 | It is abstract |
| MCS1108 | It takes a `ref`, `out` or `in` parameter |
| MCS1201 | `ToEntity` cannot be generated: an entity property has no DAL column of the same name and type |
| MCS1202 | `Apply` cannot be generated: an edit property has no settable DAL column of the same name and type |
| MCS1203 | `From` cannot be generated: a column set only at creation has no edit property to come from (only on a DAL that lists `IDalFromEdit` or writes neither `ToEntity` nor `Apply`) |
| MCS1204 | A `[GenerateDalMapping]` DAL is nested |
| MCS1205 | A `[GenerateDalMapping]` DAL is generic |
| MCS1206 | The entity has no accessible parameterless constructor |
| MCS1207 | A `[GenerateDalMapping]` DAL is not `partial` |
| MCS1208 | `[GenerateDalMapping]` is on a class that is not a concrete DAL implementing `IDalTransform<TEntity, TEdit>` |
| MCS1301 | A filter field fits no rule, and the repository's `ApplyCustomFilter` does not read it |
| MCS1302 | A filter field's entity property has no DAL column |
| MCS1303 | That DAL column has another type |
| MCS1304 | A filter field fits no rule, and the filter's `MatchesCustomFilter` does not read it |
| MCS1305 / MCS1306 | A filtered repository is nested / generic |
| MCS1307 / MCS1308 | A `[GenerateMatches]` filter is nested / generic |
| MCS1309 | A `[GenerateFilterQuery]` repository is not `partial` |
| MCS1310 | `[GenerateFilterQuery]` is on a class that is not a concrete repository inheriting an abstract `ApplyFilter` over a DAL |
| MCS1311 | A `[GenerateMatches]` filter is not `partial` |
| MCS1312 | `[GenerateMatches]` is on a type that is not a class implementing `IEntityFilter<TEntity>` |
| MCS1501 | More than one `[MagicClientConvention]` |
| MCS1502 | The client base lacks a member the generated client calls |
| MCS1503 | The client registration method was not found |
| MCS1504 | A `{value}` in a route matches no parameter |
| MCS1505 | A route value is not a string, integer, `Guid` or enum |
| MCS1506 | A client write has more than one body parameter |
| MCS1507 | A client route method does not return `Task` or `Task<T>` |
| MCS1508 / MCS1509 | A `[MagicClient]` interface is generic / nested |
| MCS1510 | A client route method is generic |
| MCS1511 | A `[MagicClient]` interface name does not start with `I` and a capital |
| MCS1512 / MCS1513 | A `[GenerateFakeBase]` class is not `partial` / not `abstract` |
| MCS1514 | `[GenerateFakeBase]` names a type that is not an interface |
| MCS1515 | The interface has a generic method |
| MCS1516 | A `[GenerateFakeBase]` class is nested |
| MCS1602 | `[MigrationsDatabase]` has a blank name |
| MCS1603 | The design-time factory base type is not in the compilation |
| MCS1604 | It is not a generic class with one type parameter and a constructor taking the database name first |
| MCS1605 | The context does not meet its type constraint |
| MCS1606 / MCS1607 / MCS1608 | The context is nested / generic / abstract |
| MCS1611 | A `[GenerateDesignTimeFactory]` context has no `[MigrationsDatabase]` |
| MCS1609 | The project already declares the `AssemblyLoader` the generator writes |

## Building on the declarations

`MagicCSharp.Generators.Declarations.dll`, in the same analyzer folder, holds the declaration model, its readers and
the source writers. A generator in another repository compiles against it from this package and is loaded next to it:

```xml
<PackageReference Include="MagicCSharp.Generators" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true"/>
<Reference Include="MagicCSharp.Generators.Declarations"
           HintPath="$(PkgMagicCSharp_Generators)/analyzers/dotnet/cs/MagicCSharp.Generators.Declarations.dll"
           Private="false"/>
```

The declarations are a building block with no compatibility promise: their types may change between any two
versions, patch releases included. A generator built on them pins the exact version of `MagicCSharp.Generators`
(`Version="[1.1.2]"`) and is rebuilt and checked against each new one.

A generator never sees another's output, so one that needs a generated use case interface looks it up through
`UseCasePipelines.GeneratedInterfaceIndex`. Its own diagnostics use its own ids, never `MCS`.
