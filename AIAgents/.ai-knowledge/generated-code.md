# Generated Code

This guide owns the rules for code the compiler writes: when to ask a generator for it, when to write it by hand,
and how to leave the generated shape. Each topic guide shows its own generated shape; this one says how to work with
all of them.

`MagicCSharp.Generators` writes code on every build from declarations already in the class: a use case's interface
from its `Execute` methods, a DAL's mapping from its columns, a repository's filter query and a filter's `Matches`
from the field names, an HTTP client from an interface's routes, a fake from an interface, an `Executing:` log line,
a host's assembly loader, a context's design-time factory.

## Is it on?

A repository `mcs init` created references `MagicCSharp.Generators` from `Directory.Build.props`. If yours does not,
write the hand-written shapes its existing code uses, and ask before adding the package: it is a change to every
project. To add it, a `PackageVersion` at the version of the other MagicCSharp packages and, in
`Directory.Build.props`, `<PackageReference Include="MagicCSharp.Generators" PrivateAssets="all" />`.

## Use the generated shape for new code

| Writing | Generated shape | Guide |
|---|---|---|
| A use case | `[GenerateInterface] partial class`, no hand-written interface | `use-case-patterns.md` |
| The `Executing:` line of a use case that changes something | `[LogExecuting]`, no `logger.LogTrace("Executing: …")` | `use-case-patterns.md` |
| A DAL | `partial`, columns only | `entities-and-database.md` |
| An EF repository | `partial`, on `RepositoryServices<TContext>`, no `CreateDal` or `ApplyFilter` | `entities-and-database.md` |
| A filter used by an in-memory fake | `partial`, lists `IEntityFilter<TEntity>`, no `Matches` | `entities-and-database.md`, `testing-conventions.md` |
| A client for another service | a `[MagicClient]` interface with route attributes, no client class | `service-clients.md` |
| A fake of an interface in a test | `[GenerateFakeBase(typeof(IX))] abstract partial class`, overriding only what the test uses | `testing-conventions.md` |
| A context's design-time factory | `[MigrationsDatabase("name")]` on the context, no factory class | `entities-and-database.md` |

`mcs add-entity` writes the hand-written DAL and repository. Convert them right after: add `partial`, delete the
methods the compiler writes, and build.

Leave existing hand-written code as it is unless the task is about it or the user asks; converting a file nobody
asked about widens the change.

## Write it by hand when it is not a copy

A generator writes only what restates a declaration. Write by hand the member that does something else, and only
that member; the generator still writes the rest of the class:

- a DAL method for a column that converts its value (JSON, a value object, a computed column); an id DAL that writes
  `ToEntity` or `Apply` lists `IDalFromEdit<TDal, TEdit>` to keep its generated `From`;
- a filter field that is not a plain comparison, in `ApplyCustomFilter` and `MatchesCustomFilter`;
- a client method with logic (skipping the call for an empty list, streaming), without a route attribute;
- an interface that must hold something other than the class's `Execute` methods;
- an `Executing:` line for a class `[LogExecuting]` refuses.

Every generator leaves a member alone when the class declares it, so declaring one is how you take it over.

## Leave the generated shape

To take a whole class back: open the generated file (the IDE lists it under the project's analyzers, or set
`EmitCompilerGeneratedFiles` to `true` and look under `obj/`), copy what it wrote into the class or its own file, and
remove the attribute or `partial` that asked for it. The names do not change, so callers do not either. Build: an
error that a member is declared twice means one generator is still asking.

## Build errors from the generators

`MCS1001`–`MCS1609` are the generators refusing something they cannot write. The message names the member and the
change: make the class `partial`, rename a column to the entity's property, add `[NotLogged]` to a real secret,
read a custom field. Fix the declaration, or write that member by hand. Never turn a rule off in `.editorconfig`,
never add a property or attribute only to quiet one, and never mark a parameter `[NotLogged]` that is not a secret.

## Never

- Edit a generated file: it is rewritten on the next build. Change the declaration it came from.
- Commit generated files. They live in the build output.
- Trust an IDE rename to reach generated code. Rename, build, and follow the errors to names the generator read
  from yours.
