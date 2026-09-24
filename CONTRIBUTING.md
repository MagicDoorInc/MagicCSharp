# Contributing

Pull requests are welcome, from anyone. MagicDoor's engineers maintain MagicCSharp and review every pull
request the same way, whether it comes from inside MagicDoor or not.

MagicCSharp is small — about 7,000 lines of library code. That is deliberate: it should stay small enough
that you could read all of it in an afternoon and own it if you had to.

Every change to `master` goes through a pull request, and two checks must pass before it can merge: the build
and tests, and a run of `mcs` that scaffolds a repository from nothing and builds it.

## Building it

```bash
dotnet build MagicCSharp.slnx
dotnet test MagicCSharp.slnx
```

You need the **.NET 10 SDK**. The libraries target net9.0; the CLI, the tests and generated repositories
target net10.0.

Some tests start a PostgreSQL container through Testcontainers, so the full run needs Docker. Without it:

```bash
dotnet test MagicCSharp.slnx --filter "Category!=Database"
```

Before opening a pull request:

```bash
dotnet run --project src/MagicCSharp.Cli -- validate --path src
```

## Trying the CLI against a scratch repository

The fastest way to see whether a change to the templates or the generators did what you meant:

```bash
dotnet pack src/MagicCSharp.Cli/MagicCSharp.Cli.csproj -c Release -o /tmp/feed -p:Version=0.0.0-dev
dotnet tool install -g MagicCSharp.Cli --version 0.0.0-dev --add-source /tmp/feed

mkdir /tmp/scratch && cd /tmp/scratch
mcs init --prefix Acme
mcs create-app --name Shop --database shop
mcs create-domain --name Orders --models --tests
```

Give the local build a version that is not on nuget.org. Reusing a published version means NuGet serves the
cached real package and your change silently is not there.

## What is welcome

- **Bugs, with the smallest thing that reproduces them.** A failing test is ideal; a `mcs` command sequence
  and what it produced is nearly as good.
- **Template changes.** If you find yourself overriding a built-in template for a reason that is not
  house style, that is a template that should change upstream. Send the diff.
- **Documentation that removes a surprise.** The gap between what a document says and what the code does is
  the most expensive kind of bug this project can have.
- **New `mcs validate` rules**, particularly ones that catch a mistake you actually made.

Two things to raise in an issue before writing code: a new package, and a change to a generated file's
shape. Both are hard to reverse once people have repositories built on them.

## House rules for the code

The existing code is the specification, but the ones worth stating:

- **Comments say why, never what.** If a comment restates the line below it, delete one of them. The
  comments worth writing are the ones that stop someone "simplifying" a decision back into a bug.
- **Name what a thing is for, not what it is.** `ApplicationAssemblies`, not `AssemblyHelper`.
- **Every public type and member gets an XML doc comment.** These ship as packages; the tooltip is the
  documentation most people will read.
- **A test's name is a sentence about behaviour.** `Update_many_writes_nothing_when_any_key_is_missing`,
  not `TestUpdate2`.
- **Anything that talks to a database gets a test that talks to a database.** See
  `tests/MagicCSharp.Data.EntityFramework.Tests`.

## Commit messages

Say what changed and why it needed to change. If the change fixes something subtle, the message is where the
next person finds out what was subtle about it — several of this project's most useful explanations live
in commit messages rather than in code.
