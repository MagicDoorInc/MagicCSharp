# Governance

## Where this came from

MagicCSharp is the architecture MagicDoor's backend is built on, extracted so it can be used outside it.
That architecture was shaped by engineers from Amazon and Google — which is mostly to say the opinions here
are not theoretical. They are what was left after finding out which
structures survive a codebase getting large and a team changing, and which do not.

That history is offered as an explanation of *why* the decisions look like this, not as a reason to trust
them. The reasons are in the documentation, and where a decision has a cost, the cost is written next to it.
Judge the design on those.

**What is true today, precisely:** MagicDoor's backend runs this structure, with one fully-formed reference
service in production and older services still on the flat layout they predate. It does not yet consume
these NuGet packages — it runs a copy that was split out and has since diverged. Converging it onto the
published packages is the plan, and until that has happened this document will keep saying so. "Battle
tested" is a claim about the structure, not about the packages.

## Who maintains it

MagicDoor's engineering team. MagicCSharp is what MagicDoor's own services are built on, so the people who
maintain it are the people who depend on it every day, and MagicDoor pays for that time. Two things keep the
risk of adopting it low even so:

- **The framework is small on purpose** — around 7,000 lines of library code. If it were abandoned tomorrow,
  vendoring it is a realistic afternoon rather than a rewrite.
- **Nothing is hidden.** Every file the CLI writes comes from a template you can read and replace. Every
  call `AddMagicApp` makes is public on the package that owns it. Outgrowing the framework means replacing
  two lines with five, not unpicking it.

Pull requests from outside MagicDoor are welcome, and are reviewed the same way as the team's own. See
[CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test and what to raise in an issue first. Publishing a
release stays with MagicDoor.

## How decisions get made

- **A bug fix or a documentation correction** needs one maintainer's review.
- **A new public type, a new package, or a change to the shape of a generated file** needs an issue first
  and agreement before code. These are the changes that are expensive to reverse once people have
  repositories built on them.
- **Disagreement** is settled by the maintainers, on the issue or pull request where it came up, with the
  reasoning written there so the next person can find it.

## What happens when MagicDoor and MagicCSharp want different things

They will, and the answer is decided in advance rather than argued each time.

**The framework stays generic.** Nothing MagicDoor-specific is added to it — not a naming convention, not a
library, not a default that only makes sense for property management software.

MagicDoor's own conventions live where every other adopter's do:

- **House style goes in a template repository**, consumed at `.magiccsharp/templates`. The per-file override
  mechanism exists precisely so that a company can change what its generated code looks like without
  forking the tool. See [docs/template-overrides.md](docs/template-overrides.md).
- **Company-specific code goes in that company's own `Libs/`**, not here.

If MagicDoor needs something the framework will not take, MagicDoor overrides a template or writes a
library. The same valve is available to you, which is the point of it being a valve rather than a
concession.

If MagicDoor stops funding this, that will be said here plainly rather than left to be inferred from a
quiet repository.

## Versions and stability

MagicCSharp follows [Semantic Versioning](https://semver.org) from `1.0`:

- **A breaking change needs a major version**, and the [CHANGELOG](CHANGELOG.md) says how to migrate. New
  features come in minor versions; fixes in patches. Upgrading within `1.x` should not break your build.
- **All packages share one version and ship together.** A mixed set is not tested.
- **Public API** is anything a package exposes as `public`, plus the shape of the files `mcs` generates.
  Generated code is API: people edit it, and changing its shape breaks their next merge.
- **The build rules are the exception to watch.** A new analyzer rule in a minor version can turn code that
  built into a compile error — that is its job. Each one is listed in the CHANGELOG, and every rule can be
  downgraded or turned off in `.editorconfig`.

## Scope

What this project is for: the repository layout, the tool that generates and checks it, and the small set of
packages the generated code depends on.

What it is not for: being a general-purpose application framework. If ASP.NET, EF Core or the BCL already
does something well, MagicCSharp should not wrap it. Several things are deliberately left to you —
authentication, CORS, rate limiting, logging providers, metric exporters — and where that is a decision
rather than an oversight, the documentation says so.
