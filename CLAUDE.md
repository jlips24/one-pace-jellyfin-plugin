# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the plugin
dotnet build -c Release

# Or use build scripts
./build.sh              # macOS/Linux
build.bat               # Windows

# Clean and rebuild
dotnet clean
dotnet restore
dotnet build -c Release
```

**Build Output:** `JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll`

**Prerequisites:** .NET 8.0 SDK, Jellyfin 10.8+

## High-Level Architecture

### Core Pattern: Singleton Metadata Service

**Critical Design Decision:** Jellyfin creates NEW provider instances for each metadata request via dependency injection. To share cached metadata across all providers without complex DI setup, this plugin uses a singleton pattern.

```csharp
// Plugin.cs - Entry point, receives DI dependencies
public Plugin(IApplicationPaths paths, IHttpClientFactory http, ILoggerFactory loggerFactory)
{
    Instance = this;
    OnePaceMetadataService.Initialize(http, logger, paths); // Initialize singleton
}

// Providers - Created by Jellyfin DI, access singleton
public OnePaceSeriesProvider(ILogger<OnePaceSeriesProvider> logger)
{
    var metadata = await OnePaceMetadataService.Instance.GetMetadataAsync();
}
```

**Why This Matters:** If you try to inject `OnePaceMetadataService` into provider constructors, you'll get "Unable to resolve service" errors. The singleton pattern initialized in Plugin.cs is the solution.

### Provider Pattern (Jellyfin's Metadata System)

Four provider types implement Jellyfin's metadata interfaces:

1. **OnePaceSeriesProvider** - Series metadata (title, genres, plot)
2. **OnePaceSeasonProvider** - Season/arc metadata (One Pace arcs = Jellyfin seasons)
3. **OnePaceEpisodeProvider** - Episode matching with hybrid strategy (CRC32 → Number → Path)
4. **OnePaceImageProvider** - Poster downloads for seasons only

### Hybrid Episode Matching Strategy

**3-tier fallback approach** in `OnePaceEpisodeProvider.cs`:

**Priority 1: CRC32 Checksum**
```
Filename: [One Pace][1-7] Romance Dawn 01 [D767799C].mkv
Extract: D767799C
Search: All arcs/episodes for matching crc32
```

**Priority 2: Season/Episode Numbers**
```
Jellyfin detects: S01E01
Match: arc.Part == 1, episodes["01"]
```

**Priority 3: Path Structure**
```
Path: /One Pace/Romance Dawn/01.mkv
Extract: "Romance Dawn" folder name
Match: arc.Title contains "Romance Dawn"
```

**Why 3 tiers?** Handles both official releases (with CRC32) and custom encodes (without), supporting multiple file organization methods.

### Multi-Layer Caching

**Layer 1:** In-memory cache (fastest, expires per config)
**Layer 2:** File-based cache (`{JellyfinCachePath}/onepace/`) (persists across restarts)
**Layer 3:** Version tracking via `status.json` (smart updates only when needed)

**Update Flow:**
1. Check memory cache → return if valid
2. Check file cache → load if valid
3. Check version → download if new
4. Cache in memory + file → save version

## Key Files

### Plugin.cs
- Entry point, inherits `BasePlugin<PluginConfiguration>`
- **Critical:** Initializes singleton metadata service in constructor
- Exposes static `Instance` for config access
- GUID: `a7f2e6d4-8c3b-4a1f-9e5d-2b8c7a4f1e3d` (matches manifest.json)

### OnePaceMetadataService.cs
- Singleton pattern with `Initialize()` and `Instance` static accessor
- Fetches from: `https://raw.githubusercontent.com/ladyisatis/one-pace-metadata/main/data.json`
- Three-layer caching (memory, file, version)
- Thread-safe via `SemaphoreSlim`

### OnePaceEpisodeProvider.cs
- Most complex component
- Implements 3-tier episode matching
- CRC32 regex: `\[([A-F0-9]{8})\]`
- Parses runtime from MM:SS format

### Models/OnePaceData.cs
JSON deserialization models:
- `OnePaceData` - Root container
- `TvShow` - Series metadata
- `Arc` - Season metadata (Part = season number)
- `Episode` - Episode metadata with CRC32
- `MetadataStatus` - Version tracking

## Configuration

5 user settings in `PluginConfiguration.cs`:
- `CacheDurationHours` (default: 24)
- `EnableAutoUpdate` (default: true)
- `AutoUpdateIntervalHours` (default: 6)
- `PreferCrc32Matching` (default: true)
- `EnablePosterDownload` (default: true)

Configured via embedded `Configuration/configPage.html`

## Common Patterns

### Accessing Configuration
```csharp
var config = Plugin.Instance?.Configuration;
if (config?.EnableAutoUpdate == true) { /* ... */ }
```

### Using Metadata Service
```csharp
var metadata = await OnePaceMetadataService.Instance.GetMetadataAsync(cancellationToken: token);
if (metadata?.Arcs == null) return;
```

### Matching Series/Seasons/Episodes
All providers check if content is One Pace before providing metadata:
```csharp
if (!IsOnePace(info.Name)) return new MetadataResult<Series>();
```

### Logging
```csharp
_logger.LogInformation("Successfully provided metadata for One Pace series");
_logger.LogDebug("Attempting CRC32 match with: {CRC32}", crc32);
_logger.LogWarning("Failed to match episode using any strategy");
```

## Metadata Source

**Primary:** https://github.com/ladyisatis/one-pace-metadata
- `data.json` - Full metadata (~200KB)
- `status.json` - Version tracking (~100 bytes)
- `posters/{arc}/poster.png` - Arc poster images

**Alternative (Plex-focused):** https://github.com/SpykerNZ/one-pace-for-plex
- Uses .nfo XML files instead of JSON
- Currently NOT used by this plugin

## Debugging

**Check Logs:** Dashboard → Logs, search for "OnePace" or "One Pace"

**Key log messages:**
- "Matched episode by CRC32: [checksum] -> [arc] Episode [number]"
- "Matched episode by season/episode: S[season]E[episode]"
- "No matching episode found"
- "Successfully fetched One Pace metadata (Version: [version])"

**Cache Locations:**
- Linux: `/var/lib/jellyfin/cache/onepace/`
- Windows: `%LocalAppData%/jellyfin/cache/onepace/`
- macOS: `~/Library/Caches/jellyfin/onepace/`

**Force Refresh:**
```csharp
await OnePaceMetadataService.Instance.GetMetadataAsync(forceRefresh: true, cancellationToken);
```

## Testing

1. Build: `dotnet build -c Debug`
2. Copy DLL to Jellyfin plugins folder: `{JellyfinDataPath}/plugins/OnePace/`
3. Restart Jellyfin
4. Add test library with One Pace files
5. Configure library to use "One Pace" metadata provider
6. Scan library
7. Check logs for matching attempts

**Test File Naming:**
- With CRC32: `[One Pace][1-7] Romance Dawn 01 [D767799C].mkv`
- Simple: `Romance Dawn/01.mkv`
- Numbered: `One Pace - S01E01 - ...mkv`

## Important Constraints

1. **Jellyfin 10.8+ Required** - Uses specific API versions
2. **Singleton Service Only** - Don't try to inject OnePaceMetadataService into providers
3. **Season Images Only** - ImageProvider only supports seasons, not series/episodes
4. **Arc Description Used** - Episodes get arc description (no per-episode descriptions in metadata)
5. **Name Must Contain "One Pace"** - Series name must contain "onepace" (case-insensitive, ignores spaces/dashes)

## Common Issues

**"Unable to resolve service" Error:**
- Cause: Tried to inject OnePaceMetadataService into provider constructor
- Fix: Use `OnePaceMetadataService.Instance` singleton accessor

**Episodes Not Matching:**
- Check file naming includes CRC32 in format `[ABC12345]`
- Verify folder structure or season/episode numbers
- Check logs for matching attempts
- Try disabling `PreferCrc32Matching` in config

**Metadata Not Updating:**
- Verify `EnableAutoUpdate` is true
- Check network connectivity to GitHub
- Delete cache files: `{JellyfinCachePath}/onepace/`
- Manually run "Update One Pace Metadata" scheduled task

**Posters Not Showing:**
- Verify `EnablePosterDownload` is true
- Check if arc has poster in metadata (not all do)
- Verify GitHub connectivity
- Check image fetcher enabled for Seasons in library settings
