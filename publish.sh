#!/bin/bash

# MagicCSharp NuGet Package Publish Script
# Usage: ./publish.sh [--push]

set -e  # Exit on error

PROJECT_PATH="src/MagicCSharp/MagicCSharp.csproj"
OUTPUT_DIR="./nupkgs"

echo "================================================"
echo "MagicCSharp NuGet Package Publisher"
echo "================================================"

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean $PROJECT_PATH --configuration Release
rm -rf $OUTPUT_DIR
mkdir -p $OUTPUT_DIR

# Build the project
echo "Building project..."
dotnet build $PROJECT_PATH --configuration Release

# Pack the NuGet package
echo "Creating NuGet package..."
dotnet pack $PROJECT_PATH --configuration Release --output $OUTPUT_DIR --no-build

# Find the generated package
PACKAGE_FILE=$(ls $OUTPUT_DIR/*.nupkg | head -n 1)

if [ -z "$PACKAGE_FILE" ]; then
    echo "Error: No package file generated!"
    exit 1
fi

echo "================================================"
echo "Package created successfully:"
echo "  $PACKAGE_FILE"
echo "================================================"

# Check if --push flag is provided
if [ "$1" == "--push" ]; then
    echo ""
    echo "Publishing to NuGet.org..."
    echo "Note: Make sure you have set your NuGet API key:"
    echo "  dotnet nuget push --help"
    echo ""
    read -p "Enter your NuGet API key (or press Enter to skip): " API_KEY

    if [ -z "$API_KEY" ]; then
        echo "Skipping publish. Package is ready in $OUTPUT_DIR"
    else
        dotnet nuget push "$PACKAGE_FILE" --api-key "$API_KEY" --source https://api.nuget.org/v3/index.json
        echo "Package published successfully!"
    fi
else
    echo ""
    echo "To publish to NuGet.org, run:"
    echo "  ./publish.sh --push"
    echo ""
    echo "Or manually push with:"
    echo "  dotnet nuget push $PACKAGE_FILE --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json"
fi

echo ""
echo "Done!"
