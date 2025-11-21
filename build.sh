#!/bin/bash

# One Pace Jellyfin Plugin Build Script

echo "Building One Pace Jellyfin Plugin..."
echo ""

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found. Please install .NET 8.0 SDK."
    echo "Download from: https://dotnet.microsoft.com/download"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version)
echo "Using .NET SDK version: $DOTNET_VERSION"
echo ""

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean --verbosity quiet

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Build in Release mode
echo "Building in Release mode..."
dotnet build -c Release

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Build successful!"
    echo ""
    echo "Plugin DLL location:"
    echo "  $(pwd)/JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll"
    echo ""
    echo "To install:"
    echo "1. Copy the DLL to your Jellyfin plugins directory:"
    echo "   Linux:   /var/lib/jellyfin/plugins/OnePace/"
    echo "   Windows: %AppData%\\Jellyfin\\Server\\plugins\\OnePace\\"
    echo "   macOS:   ~/.local/share/jellyfin/plugins/OnePace/"
    echo ""
    echo "2. Restart Jellyfin"
    echo ""
else
    echo ""
    echo "❌ Build failed. Check the errors above."
    exit 1
fi
