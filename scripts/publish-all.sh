#!/bin/bash

# MagicCSharp NuGet Packages Publish Script
# Usage: ./publish-all.sh [--push] [--major|--minor|--patch]
#
# Flags:
#   --push         Push packages to NuGet.org after building
#   --major        Increment major version (X.0.0)
#   --minor        Increment minor version (0.X.0)
#   --patch        Increment patch version (0.0.X) - DEFAULT
#
# Examples:
#   ./publish-all.sh                    # Build locally, increment patch
#   ./publish-all.sh --push             # Build and push, increment patch
#   ./publish-all.sh --minor --push     # Build and push, increment minor

set -e  # Exit on error

# Navigate to repository root
cd "$(dirname "$0")/.."

VERSION_FILE="scripts/version.txt"
OUTPUT_DIR="./nupkgs"

# Package paths
PACKAGES=(
    "src/MagicCSharp/MagicCSharp.csproj"
    "src/MagicCSharp.Data/MagicCSharp.Data.csproj"
    "src/MagicCSharp.Events/MagicCSharp.Events.csproj"
    "src/MagicCSharp.Events.Kafka/MagicCSharp.Events.Kafka.csproj"
    "src/MagicCSharp.Events.SQS/MagicCSharp.Events.SQS.csproj"
)

echo "================================================"
echo "MagicCSharp NuGet Package Publisher"
echo "================================================"
echo ""

# Parse arguments
PUSH=false
VERSION_TYPE="patch"

for arg in "$@"; do
    case $arg in
        --push)
            PUSH=true
            ;;
        --major)
            VERSION_TYPE="major"
            ;;
        --minor)
            VERSION_TYPE="minor"
            ;;
        --patch)
            VERSION_TYPE="patch"
            ;;
        *)
            echo "Unknown argument: $arg"
            echo "Usage: $0 [--push] [--major|--minor|--patch]"
            exit 1
            ;;
    esac
done

# Read current version
if [ ! -f "$VERSION_FILE" ]; then
    echo "Error: $VERSION_FILE not found!"
    exit 1
fi

CURRENT_VERSION=$(cat $VERSION_FILE)
echo "Current version: $CURRENT_VERSION"

# Parse version
IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_VERSION"
MAJOR="${VERSION_PARTS[0]}"
MINOR="${VERSION_PARTS[1]}"
PATCH="${VERSION_PARTS[2]}"

# Increment version
case $VERSION_TYPE in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo "New version: $NEW_VERSION ($VERSION_TYPE bump)"
echo ""

# Update version file
echo "$NEW_VERSION" > $VERSION_FILE
echo "Updated $VERSION_FILE"

# Update version in all .csproj files
echo "Updating version in .csproj files..."
for PROJECT in "${PACKAGES[@]}"; do
    if [ -f "$PROJECT" ]; then
        # Use sed to update the <Version> tag
        if [[ "$OSTYPE" == "darwin"* ]]; then
            # macOS
            sed -i '' "s/<Version>.*<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$PROJECT"
        else
            # Linux
            sed -i "s/<Version>.*<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$PROJECT"
        fi
        echo "  ✓ Updated $PROJECT"
    else
        echo "  ⚠ Warning: $PROJECT not found"
    fi
done

echo ""

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf $OUTPUT_DIR
mkdir -p $OUTPUT_DIR

for PROJECT in "${PACKAGES[@]}"; do
    if [ -f "$PROJECT" ]; then
        dotnet clean "$PROJECT" --configuration Release > /dev/null
    fi
done

echo "✓ Clean complete"
echo ""

# Build and pack all packages
echo "Building and packing packages..."
echo ""

SUCCESSFUL_PACKAGES=()

for PROJECT in "${PACKAGES[@]}"; do
    if [ ! -f "$PROJECT" ]; then
        echo "⚠ Skipping $PROJECT (not found)"
        continue
    fi

    PROJECT_NAME=$(basename "$PROJECT" .csproj)
    echo "─────────────────────────────────────────────"
    echo "Building $PROJECT_NAME..."
    echo "─────────────────────────────────────────────"

    # Build
    if dotnet build "$PROJECT" --configuration Release; then
        echo "✓ Build successful"
    else
        echo "✗ Build failed for $PROJECT_NAME"
        exit 1
    fi

    # Pack
    if dotnet pack "$PROJECT" --configuration Release --output $OUTPUT_DIR --no-build; then
        echo "✓ Package created"
        SUCCESSFUL_PACKAGES+=("$OUTPUT_DIR/$PROJECT_NAME.$NEW_VERSION.nupkg")
    else
        echo "✗ Pack failed for $PROJECT_NAME"
        exit 1
    fi

    echo ""
done

# Summary
echo "================================================"
echo "Build Summary"
echo "================================================"
echo "Version: $NEW_VERSION"
echo ""
echo "Packages created:"
for PKG in "${SUCCESSFUL_PACKAGES[@]}"; do
    echo "  ✓ $(basename $PKG)"
done
echo "================================================"
echo ""

# Push to NuGet if requested
if [ "$PUSH" = true ]; then
    echo "Publishing to NuGet.org..."
    echo ""

    # Check for API key in environment variable
    if [ -z "$NUGET_API_KEY" ]; then
        read -p "Enter your NuGet API key (or press Enter to skip): " API_KEY
        if [ -z "$API_KEY" ]; then
            echo "Skipping publish. Packages are ready in $OUTPUT_DIR"
            echo ""
            echo "To publish later, set NUGET_API_KEY environment variable or run:"
            echo "  ./publish-all.sh --push"
            exit 0
        fi
    else
        API_KEY="$NUGET_API_KEY"
        echo "Using NUGET_API_KEY from environment"
    fi

    echo ""
    echo "Publishing packages..."
    echo ""

    for PKG in "${SUCCESSFUL_PACKAGES[@]}"; do
        echo "Publishing $(basename $PKG)..."
        if dotnet nuget push "$PKG" --api-key "$API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate; then
            echo "✓ Published successfully"
        else
            echo "✗ Failed to publish $(basename $PKG)"
            exit 1
        fi
        echo ""
    done

    echo "================================================"
    echo "All packages published successfully!"
    echo "Version $NEW_VERSION is now live on NuGet.org"
    echo "================================================"
else
    echo "Packages built successfully but not published."
    echo ""
    echo "To publish to NuGet.org, run:"
    echo "  ./publish-all.sh --push"
    echo ""
    echo "Or set NUGET_API_KEY environment variable and run:"
    echo "  export NUGET_API_KEY='your-api-key'"
    echo "  ./publish-all.sh --push"
fi

echo ""
echo "Done!"
