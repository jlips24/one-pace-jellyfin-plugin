# Building the One Pace Jellyfin Plugin

This guide will help you build the One Pace plugin from source.

## Prerequisites

### Required Software

1. **.NET 8.0 SDK** (or later)
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Choose the SDK (not Runtime) for your operating system

2. **Git** (optional, for cloning)
   - Download: https://git-scm.com/downloads

### Verify Installation

After installing .NET SDK, verify it's working:

```bash
dotnet --version
```

You should see version 8.0.x or higher.

## Building the Plugin

### Option 1: Using Build Scripts

#### On macOS/Linux:

```bash
cd /Users/jakelipson/Desktop/one-pace-jellyfin-plugin
./build.sh
```

#### On Windows:

```cmd
cd C:\path\to\one-pace-jellyfin-plugin
build.bat
```

### Option 2: Manual Build

```bash
# Navigate to the project directory
cd /Users/jakelipson/Desktop/one-pace-jellyfin-plugin

# Restore NuGet packages
dotnet restore

# Build in Release mode
dotnet build -c Release
```

## Build Output

After a successful build, the plugin DLL will be located at:

```
JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll
```

## Installing the Built Plugin

### Linux

```bash
# Create plugin directory
sudo mkdir -p /var/lib/jellyfin/plugins/OnePace

# Copy the DLL
sudo cp JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll \
        /var/lib/jellyfin/plugins/OnePace/

# Set permissions
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/OnePace
```

### macOS

```bash
# Create plugin directory
mkdir -p ~/.local/share/jellyfin/plugins/OnePace

# Copy the DLL
cp JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll \
   ~/.local/share/jellyfin/plugins/OnePace/
```

### Windows

```cmd
# Create plugin directory
mkdir "%AppData%\Jellyfin\Server\plugins\OnePace"

# Copy the DLL
copy JellyfinPlugin.OnePace\bin\Release\net8.0\JellyfinPlugin.OnePace.dll ^
     "%AppData%\Jellyfin\Server\plugins\OnePace\"
```

### Docker

```bash
# Copy to your Jellyfin config volume
docker cp JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll \
          jellyfin:/config/plugins/OnePace/

# Restart container
docker restart jellyfin
```

## Troubleshooting Build Issues

### Error: SDK not found

**Problem**: `dotnet: command not found` or similar

**Solution**: Install .NET 8.0 SDK from https://dotnet.microsoft.com/download

### Error: NuGet package restore failed

**Problem**: Cannot restore Jellyfin packages

**Solution**:
1. Check internet connection
2. Clear NuGet cache: `dotnet nuget locals all --clear`
3. Try again: `dotnet restore`

### Error: Missing Jellyfin references

**Problem**: Cannot find `Jellyfin.Controller` or `Jellyfin.Model`

**Solution**:
1. Ensure you're using .NET 8.0 SDK
2. Check that the .csproj file specifies version `10.8.*` for Jellyfin packages
3. Run `dotnet restore --force`

### Build warnings about nullability

**Problem**: Warnings about nullable reference types

**Solution**: These are informational warnings and don't affect functionality. The build should still succeed.

## Development Build

For development with debug symbols:

```bash
# Build in Debug mode
dotnet build -c Debug

# Run with file watcher for auto-rebuild
dotnet watch build
```

Debug DLL location:
```
JellyfinPlugin.OnePace/bin/Debug/net8.0/JellyfinPlugin.OnePace.dll
```

## Creating a Release Package

To create a distributable release:

```bash
# Publish the plugin
dotnet publish -c Release -o publish/

# Create a zip file
cd publish
zip -r OnePacePlugin-v1.0.0.zip *
```

## Build Configuration

The build is configured in:

- `JellyfinPlugin.OnePace.csproj` - Project settings and dependencies
- `OnePaceJellyfinPlugin.sln` - Solution file
- `.gitignore` - Files excluded from version control

### Key Dependencies

From `JellyfinPlugin.OnePace.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="Jellyfin.Controller" Version="10.8.*" />
    <PackageReference Include="Jellyfin.Model" Version="10.8.*" />
</ItemGroup>
```

These packages provide the Jellyfin plugin API.

## Testing the Built Plugin

After installing:

1. Restart Jellyfin
2. Go to Dashboard → Plugins
3. Verify "One Pace" appears in the list
4. Configure the plugin settings
5. Add a One Pace library and scan
6. Check logs for any errors:
   - Dashboard → Logs
   - Look for entries containing "One Pace"

## Continuous Integration

For CI/CD pipelines (GitHub Actions, GitLab CI, etc.):

```yaml
# Example GitHub Actions workflow
name: Build Plugin

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build -c Release
      - run: dotnet publish -c Release -o dist/
      - uses: actions/upload-artifact@v3
        with:
          name: plugin
          path: dist/JellyfinPlugin.OnePace.dll
```

## Getting Help

If you encounter build issues:

1. Check this guide's troubleshooting section
2. Verify .NET SDK version: `dotnet --version`
3. Check Jellyfin compatibility (10.8+)
4. Review build logs for specific errors
5. Open an issue on GitHub with:
   - .NET SDK version
   - Operating system
   - Full build output/errors
