# Releasing

Every MagicCSharp package shares one version and ships together: the twelve libraries in `src/`,
`MagicCSharp.Analyzers` and `MagicCSharp.Cli`. They are published under the
[MagicDoor](https://www.nuget.org/profiles/MagicDoor) organisation on NuGet.org.

```bash
./scripts/publish-all.sh --minor --dry-run   # everything builds and packs; nothing changes
./scripts/publish-all.sh --minor             # bump the version and pack

git commit -am "Release 0.2.0"
git tag v0.2.0
git push origin master v0.2.0                # the tag publishes
```

Move the `CHANGELOG.md` entry under the new version before committing.

Pushing the tag runs [`.github/workflows/release.yml`](../.github/workflows/release.yml). It checks the tag
matches the version in the repository, builds, runs the tests, packs all fourteen packages, and pushes them.
The job waits in the `nuget` environment for an approval before it runs.

There is no API key. NuGet.org trusts that workflow file, in this repository, in that environment, through
the `MagicDoorPush` trusted publishing policy. Renaming the workflow or the environment stops publishing
until the policy on nuget.org is edited to match.

A version on NuGet.org cannot be deleted, only unlisted, so a mistake means shipping the next patch. The
workflow pushes with `--skip-duplicate`, so re-running it after a partial failure only sends what is missing.

## publish-all.sh

| Flag | |
|---|---|
| `--dry-run` | Build and pack everything, then undo the version bump and delete the packages |
| `--major` / `--minor` / `--patch` | Which part of the version to bump. Patch is the default |

`scripts/version.txt` is the source of the version. The script bumps it, then writes the same number to
`src/Directory.Build.props`, which every package inherits. A repository `mcs init` creates pins the version of
the CLI that created it.
