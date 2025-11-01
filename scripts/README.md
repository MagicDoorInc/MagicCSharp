# MagicCSharp Publishing Scripts

This directory contains scripts for building and publishing MagicCSharp NuGet packages.

## Version Management

The version for all packages is stored in `scripts/version.txt`. The publish script automatically:
- Reads the current version
- Increments it based on the specified bump type (major, minor, or patch)
- Updates all `.csproj` files with the new version
- Builds and packages all projects

## Scripts

### publish-all.sh

**Main publishing script** - Handles all MagicCSharp packages with automatic version management.

#### Usage

```bash
./publish-all.sh [--push] [--major|--minor|--patch]
```

#### Flags

- `--push` - Push packages to NuGet.org after building
- `--major` - Increment major version (X.0.0)
- `--minor` - Increment minor version (0.X.0)
- `--patch` - Increment patch version (0.0.X) - **DEFAULT**

#### Packages Built

- MagicCSharp
- MagicCSharp.Data
- MagicCSharp.Events
- MagicCSharp.Events.Kafka
- MagicCSharp.Events.SQS

#### Examples

**Build locally (patch bump):**
```bash
./publish-all.sh
```

**Build and publish (patch bump):**
```bash
./publish-all.sh --push
```

**Build and publish (minor bump):**
```bash
./publish-all.sh --minor --push
```

**Build locally (major bump):**
```bash
./publish-all.sh --major
```

#### API Key

The script will prompt for your NuGet API key when using `--push`. Alternatively, set it as an environment variable:

```bash
export NUGET_API_KEY='your-api-key'
./publish-all.sh --push
```

### publish.sh

**Legacy script** - Original script for publishing only the core MagicCSharp package. Use `publish-all.sh` instead for managing all packages together.

## Workflow

### Development Release (Patch)

For bug fixes and minor updates:

```bash
# Build and test locally first
./publish-all.sh

# Verify packages in ./nupkgs/
# Then publish
./publish-all.sh --push
```

### Feature Release (Minor)

For new features:

```bash
./publish-all.sh --minor --push
```

### Breaking Changes (Major)

For breaking changes:

```bash
./publish-all.sh --major --push
```

## Version File

`scripts/version.txt` contains the current version in SemVer format:

```
1.0.0
```

This file is automatically updated by `publish-all.sh` and should not be edited manually unless necessary.

## Testing Without Publishing

To test the build process without incrementing the version or publishing:

1. Make a backup of `version.txt`
2. Run `./publish-all.sh`
3. Check packages in `./nupkgs/`
4. Restore `version.txt` from backup

Or just inspect the packages locally without worrying about version increments - you can always edit `version.txt` back if needed.

## Troubleshooting

**"Error: version.txt not found!"**
- Ensure `scripts/version.txt` exists in the repository

**"Build failed for X"**
- Check that all dependencies are restored
- Run `dotnet restore` in the repository root

**"Pack failed for X"**
- Ensure the README.md file exists for each package
- Check .csproj file for valid PackageReference entries

**"Failed to publish X"**
- Verify your NuGet API key is valid
- Check that the package version doesn't already exist on NuGet.org
- The script uses `--skip-duplicate` to avoid re-publishing existing versions
