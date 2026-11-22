# Releasing

Every MagicCSharp package shares one version and ships together, seventeen in all. They are published under
the [MagicDoor](https://www.nuget.org/profiles/MagicDoor) organisation on NuGet.org.

| Kind | Packages |
|---|---|
| Fourteen libraries | `MagicCSharp`, `MagicCSharp.App`, `MagicCSharp.AspNetCore`, `MagicCSharp.Scheduling`, `MagicCSharp.Data`, `MagicCSharp.Data.EntityFramework`, `MagicCSharp.Data.Postgres`, `MagicCSharp.Events`, `MagicCSharp.Events.Kafka`, `MagicCSharp.Events.SQS`, `MagicCSharp.Clients`, `MagicCSharp.Testing`, `MagicCSharp.Testing.Database`, `MagicCSharp.Testing.Clients` |
| Two compiler packages | `MagicCSharp.Analyzers` (build rules), `MagicCSharp.Generators` (source generators; carries `MagicCSharp.Generators.Declarations`, which is not a package of its own) |
| One tool | `MagicCSharp.Cli` (`mcs`) |

```bash
./scripts/publish-all.sh --minor --dry-run   # everything builds and packs; nothing changes
./scripts/publish-all.sh --minor             # bump the version and pack

git commit -am "Release 0.2.0"
git tag v0.2.0
git push origin master v0.2.0                # the tag publishes
```

Move the `CHANGELOG.md` entry under the new version before committing.

`publish-all.sh` ends with the package smoke check: [package-smoke/](package-smoke/) is a small app built only from
the packages it just packed, which uses every generator (`[GenerateInterface]`, `[LogExecuting]`, `[GenerateDalMapping]`,
`[GenerateFilterQuery]`, `[GenerateMatches]`, `[MagicClient]`, `[GenerateFakeBase]`, `[GenerateDesignTimeFactory]`, the
assembly loader) and fails naming the one that did not run. The solution's tests reference the generators as projects, so this is
the only check of what the packages actually carry. The release workflow runs it again on what it is about to push.

Pushing the tag runs [`.github/workflows/release.yml`](../.github/workflows/release.yml). It checks the tag
matches the version in the repository, builds, runs the tests, packs all seventeen packages, runs the package
smoke check on them, and pushes them.
It runs as soon as the tag is pushed; there is no approval step. The `nuget` environment only accepts `v*`
tags, so nothing but a version tag can publish.

There is no API key. NuGet.org trusts that workflow file, in this repository, in that environment, through
the `MagicDoorPush` trusted publishing policy. Renaming the workflow or the environment stops publishing
until the policy on nuget.org is edited to match.

A version on NuGet.org cannot be deleted, only unlisted, so a mistake means shipping the next patch. The
workflow pushes with `--skip-duplicate`, so re-running it after a partial failure only sends what is missing.

## publish-all.sh

| Flag | |
|---|---|
| `--dry-run` | Build and pack everything and run the smoke check, then undo the version bump and delete the packages |
| `--major` / `--minor` / `--patch` | Which part of the version to bump. Patch is the default |

`scripts/version.txt` is the source of the version. The script bumps it, then writes the same number to
`src/Directory.Build.props`, which every package inherits. A repository `mcs init` creates pins the version of
the CLI that created it.
