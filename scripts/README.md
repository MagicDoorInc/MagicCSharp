# Publishing

Every MagicCSharp package shares one version and ships together. `publish-all.sh` builds, packs and
optionally pushes all fourteen: the twelve libraries in `src/`, `MagicCSharp.Cli`, and
`MagicCSharp.Templates`.

```bash
./scripts/publish-all.sh [--dry-run] [--push] [--major|--minor|--patch]
```

| Flag | |
|---|---|
| `--dry-run` | Build and pack everything, then undo the version bump and delete the packages |
| `--push` | Push to NuGet.org after packing (reads `NUGET_API_KEY`, prompts if unset) |
| `--major` / `--minor` / `--patch` | Which part of the version to bump. Patch is the default |

## Where the version lives

`scripts/version.txt` is the source. The script bumps it, then writes the same number to:

- `src/Directory.Build.props` — inherited by every library and the CLI
- `templates/content/magiccsharp-repo/.template.config/template.json` — the version a freshly generated
  repository pins

`templates/MagicCSharp.Templates.csproj` reads `version.txt` directly, since it sits outside `src/`.

## Releasing

```bash
./scripts/publish-all.sh --minor --dry-run   # everything builds and packs; nothing changes
./scripts/publish-all.sh --minor --push      # the release
```

Then move the `CHANGELOG.md` entry under the new version, commit the three bumped files, and tag the commit
`v<version>`. The script does not commit or tag.

A version on NuGet.org cannot be deleted, only unlisted, so a mistake means shipping the next patch. The
script pushes with `--skip-duplicate`, so re-running after a partial failure only sends what is missing.
