# One Pace Jellyfin Plugin - Project Structure

## Overview

This document describes the complete structure of the One Pace Jellyfin plugin.

## Directory Tree

```
one-pace-jellyfin-plugin/
├── .gitignore                              # Git ignore rules
├── OnePaceJellyfinPlugin.sln              # Visual Studio solution file
├── README.md                               # Main documentation
├── BUILDING.md                             # Build instructions
├── PROJECT_STRUCTURE.md                    # This file
├── build.sh                                # Build script (macOS/Linux)
├── build.bat                               # Build script (Windows)
│
└── JellyfinPlugin.OnePace/                # Main plugin project
    ├── JellyfinPlugin.OnePace.csproj      # Project configuration
    ├── Plugin.cs                           # Plugin entry point
    ├── PluginConfiguration.cs             # Configuration model
    │
    ├── Configuration/
    │   └── configPage.html                 # Admin UI for plugin settings
    │
    ├── Models/
    │   └── OnePaceData.cs                  # Data models for JSON deserialization
    │       ├── OnePaceData                 # Root data container
    │       ├── TvShow                      # Series metadata
    │       ├── Arc                         # Season/arc metadata
    │       ├── Episode                     # Episode metadata
    │       └── MetadataStatus              # Version tracking
    │
    ├── Services/
    │   └── OnePaceMetadataService.cs       # Core metadata service
    │       ├── GetMetadataAsync()          # Fetch/cache metadata
    │       ├── FetchMetadataFromGitHubAsync() # Download from GitHub
    │       ├── ShouldUpdateMetadataAsync() # Version checking
    │       └── Cache management methods
    │
    ├── Providers/
    │   ├── OnePaceSeriesProvider.cs        # Series metadata provider
    │   │   ├── GetSearchResults()          # Search for series
    │   │   └── GetMetadata()               # Provide series metadata
    │   │
    │   ├── OnePaceSeasonProvider.cs        # Season/arc metadata provider
    │   │   ├── GetSearchResults()          # Search for seasons
    │   │   └── GetMetadata()               # Provide season metadata
    │   │
    │   ├── OnePaceEpisodeProvider.cs       # Episode metadata provider
    │   │   ├── GetSearchResults()          # Search for episodes
    │   │   ├── GetMetadata()               # Provide episode metadata
    │   │   ├── FindEpisodeMatch()          # Hybrid matching logic
    │   │   ├── ExtractCrc32FromPath()      # CRC32 extraction
    │   │   ├── FindByCrc32()               # CRC32 matching
    │   │   ├── FindBySeasonEpisode()       # Season/episode matching
    │   │   └── FindByPathStructure()       # Folder structure matching
    │   │
    │   └── OnePaceImageProvider.cs         # Poster image provider
    │       ├── Supports()                  # Check if item supported
    │       ├── GetSupportedImages()        # List supported image types
    │       ├── GetImages()                 # Fetch image URLs
    │       └── GetImageResponse()          # Download images
    │
    └── ScheduledTasks/
        └── MetadataUpdateTask.cs           # Background update task
            ├── ExecuteAsync()              # Run update check
            └── GetDefaultTriggers()        # Schedule configuration
```

## Component Descriptions

### Core Files

#### Plugin.cs
- Main plugin entry point
- Inherits from `BasePlugin<PluginConfiguration>`
- Provides plugin name, ID, and configuration pages
- Static instance accessor for configuration

#### PluginConfiguration.cs
- Configuration model with user-configurable settings:
  - `CacheDurationHours`: How long to cache metadata
  - `EnableAutoUpdate`: Toggle automatic updates
  - `AutoUpdateIntervalHours`: Update check frequency
  - `PreferCrc32Matching`: Matching strategy preference
  - `EnablePosterDownload`: Toggle poster downloads

#### JellyfinPlugin.OnePace.csproj
- Project configuration file
- Targets .NET 8.0
- References Jellyfin.Controller and Jellyfin.Model (v10.8.*)
- Embeds configuration HTML as resource

### Configuration

#### Configuration/configPage.html
- Admin UI shown in Jellyfin dashboard
- JavaScript form handling
- Saves/loads configuration via Jellyfin API
- User-friendly interface for all settings

### Data Models

#### Models/OnePaceData.cs
Contains all data models:

1. **OnePaceData**: Root container
   - Last update timestamp
   - Base URL for resources
   - TV show metadata
   - List of arcs

2. **TvShow**: Series-level metadata
   - Title, original title, sort title
   - Genres, premiere date, status
   - Rating, plot description

3. **Arc**: Season/arc metadata
   - Part number (season number)
   - Saga, title, description
   - Poster path
   - Dictionary of episodes

4. **Episode**: Episode metadata
   - Length (runtime)
   - CRC32 checksums (regular + extended)
   - Tracker IDs

5. **MetadataStatus**: Version tracking
   - Version number
   - Last update timestamp

### Services

#### Services/OnePaceMetadataService.cs
Core service for metadata management:

**Responsibilities**:
- Fetch metadata from GitHub
- Cache data locally
- Check for version updates
- Manage cache lifecycle

**Key Methods**:
- `GetMetadataAsync()`: Main method to get metadata (cached or fresh)
- `FetchMetadataFromGitHubAsync()`: Downloads from GitHub
- `ShouldUpdateMetadataAsync()`: Checks if new version available
- `TryLoadFromFileCache()`: Loads from local cache
- `SaveToFileCache()`: Saves to local cache
- `GetCachedVersion()`: Retrieves cached version number
- `SaveCurrentVersion()`: Stores version info

**Caching Strategy**:
- In-memory cache with timestamp
- File-based cache for persistence
- Version-based invalidation
- Configurable duration

### Providers

Providers implement Jellyfin's metadata provider interfaces:

#### Providers/OnePaceSeriesProvider.cs
Implements: `IRemoteMetadataProvider<Series, SeriesInfo>`

**Responsibilities**:
- Match series by name ("One Pace")
- Provide series-level metadata
- Return genres, premiere date, plot, etc.

**Methods**:
- `GetSearchResults()`: Returns matching series
- `GetMetadata()`: Provides full series metadata
- `IsOnePace()`: Helper to identify One Pace series

#### Providers/OnePaceSeasonProvider.cs
Implements: `IRemoteMetadataProvider<Season, SeasonInfo>`

**Responsibilities**:
- Match seasons by index (part number) or name
- Provide arc metadata as seasons
- Return arc title and description

**Methods**:
- `GetSearchResults()`: Find matching arcs
- `GetMetadata()`: Provide arc metadata
- `IsOnePaceSeries()`: Verify series ownership

#### Providers/OnePaceEpisodeProvider.cs
Implements: `IRemoteMetadataProvider<Episode, EpisodeInfo>`

**Responsibilities**:
- Match episodes using hybrid strategy
- Provide episode metadata
- Handle CRC32 and name-based matching

**Matching Strategy** (priority order):
1. CRC32 checksum from filename
2. Season/episode number
3. Path structure (folder name + episode number)

**Methods**:
- `GetSearchResults()`: Find matching episodes
- `GetMetadata()`: Provide episode metadata
- `FindEpisodeMatch()`: Orchestrate matching strategies
- `ExtractCrc32FromPath()`: Extract CRC32 from filename
- `FindByCrc32()`: Match by checksum
- `FindBySeasonEpisode()`: Match by numbers
- `FindByPathStructure()`: Match by folder structure
- `ParseRuntime()`: Convert MM:SS to minutes

**CRC32 Regex**: `\[([A-F0-9]{8})\]`
- Matches: `[D767799C]`, `[ABC12345]`
- Case-insensitive

#### Providers/OnePaceImageProvider.cs
Implements: `IRemoteImageProvider`

**Responsibilities**:
- Provide poster images for arcs (seasons)
- Download images from GitHub
- Support Primary image type

**Methods**:
- `Supports()`: Check if item is supported (Season only)
- `GetSupportedImages()`: Return ImageType.Primary
- `GetImages()`: Construct poster URLs
- `GetImageResponse()`: Download image via HTTP

**Image URL Format**:
```
{base_url}/{poster_path}
Example: https://raw.githubusercontent.com/.../posters/1/poster.png
```

### Scheduled Tasks

#### ScheduledTasks/MetadataUpdateTask.cs
Implements: `IScheduledTask`

**Responsibilities**:
- Periodic metadata updates
- Respect user configuration
- Report progress

**Methods**:
- `ExecuteAsync()`: Run update task
- `GetDefaultTriggers()`: Define schedule

**Default Schedule**:
- Interval-based (configurable)
- Default: Every 6 hours
- Respects `EnableAutoUpdate` setting

## Data Flow

### Initial Metadata Fetch

```
User adds library
    ↓
Jellyfin scans files
    ↓
SeriesProvider.GetMetadata()
    ↓
MetadataService.GetMetadataAsync()
    ↓
Check cache validity
    ↓
[Cache valid] → Return cached data
    ↓
[Cache invalid] → Check version (status.json)
    ↓
[New version] → Download data.json
    ↓
Parse JSON → Cache locally → Return data
```

### Episode Matching Flow

```
Jellyfin finds episode file
    ↓
EpisodeProvider.GetMetadata(info)
    ↓
Extract file path, season, episode numbers
    ↓
FindEpisodeMatch():
    ↓
[1. CRC32 Matching]
    Extract CRC32 from filename [ABC12345]
    ↓
    Search all arcs/episodes for matching CRC32
    ↓
    [Match found] → Return (arc, episode)
    ↓
    [No match] → Continue to next strategy
    ↓
[2. Season/Episode Matching]
    Use season number (arc part) + episode number
    ↓
    Find arc by part number
    ↓
    Find episode by number (01, 02, etc.)
    ↓
    [Match found] → Return (arc, episode)
    ↓
    [No match] → Continue to next strategy
    ↓
[3. Path Structure Matching]
    Extract folder name from path
    ↓
    Find arc with matching title in folder name
    ↓
    Find episode by number
    ↓
    [Match found] → Return (arc, episode)
    ↓
    [No match] → Return null (no metadata)
```

### Image Download Flow

```
Jellyfin requests season images
    ↓
ImageProvider.GetImages(season)
    ↓
Check if poster download enabled
    ↓
MetadataService.GetMetadataAsync()
    ↓
Find arc by season index or name
    ↓
Check if arc has poster path
    ↓
Construct full URL: {base_url}/{poster}
    ↓
Return RemoteImageInfo
    ↓
Jellyfin calls GetImageResponse(url)
    ↓
Download image via HTTP
    ↓
Return HttpResponseMessage
    ↓
Jellyfin caches image locally
```

## Configuration Storage

### Plugin Configuration
Stored by Jellyfin at:
- Linux: `/var/lib/jellyfin/config/plugins/configurations/JellyfinPlugin.OnePace.xml`
- Windows: `%AppData%\Jellyfin\Server\config\plugins\configurations\JellyfinPlugin.OnePace.xml`
- macOS: `~/.local/share/jellyfin/config/plugins/configurations/JellyfinPlugin.OnePace.xml`

Format: XML serialization of `PluginConfiguration`

### Metadata Cache
Stored at:
- Linux: `/var/lib/jellyfin/cache/onepace/`
- Windows: `%LocalAppData%\jellyfin\cache\onepace\`
- macOS: `~/Library/Caches/jellyfin/onepace/`

Files:
- `onepace-metadata-cache.json`: Full metadata
- `onepace-version.txt`: Current version timestamp

## External Dependencies

### NuGet Packages
- `Jellyfin.Controller` v10.8.*
  - Provides: Entity classes (Series, Season, Episode)
  - Provides: Provider interfaces
  - Provides: Application paths

- `Jellyfin.Model` v10.8.*
  - Provides: Model classes
  - Provides: Plugin base classes
  - Provides: Serialization

### External Data Sources
- **Metadata**: https://github.com/ladyisatis/one-pace-metadata
  - `data.json`: Full metadata (all arcs, episodes)
  - `status.json`: Version information
  - `posters/`: Arc poster images

- **Update Frequency**: Maintained by community, updated regularly

## Build Artifacts

### Debug Build
```
JellyfinPlugin.OnePace/bin/Debug/net8.0/
├── JellyfinPlugin.OnePace.dll        # Main assembly
├── JellyfinPlugin.OnePace.pdb        # Debug symbols
└── JellyfinPlugin.OnePace.deps.json  # Dependencies
```

### Release Build
```
JellyfinPlugin.OnePace/bin/Release/net8.0/
├── JellyfinPlugin.OnePace.dll        # Optimized assembly
└── JellyfinPlugin.OnePace.deps.json  # Dependencies
```

Only the `.dll` file is needed for deployment.

## Plugin Lifecycle

1. **Initialization**
   - Jellyfin loads DLL
   - Instantiates `Plugin` class
   - Registers providers
   - Registers scheduled tasks

2. **Configuration Load**
   - Loads from XML configuration
   - Applies defaults if not configured

3. **Library Scan**
   - Series provider matches series
   - Season provider matches arcs
   - Episode provider matches episodes
   - Image provider downloads posters

4. **Background Tasks**
   - Scheduled task runs periodically
   - Checks for metadata updates
   - Downloads if new version available

5. **Shutdown**
   - Cached data persists to file
   - Configuration saved

## Security Considerations

- All HTTP requests use HTTPS
- No authentication required (public GitHub raw files)
- No user credentials stored
- Local cache files are JSON (no executable code)
- CRC32 extraction uses compiled regex (safe)

## Performance Characteristics

- **Metadata Download**: ~200KB JSON file
- **Cache Hit**: Instant (in-memory or file)
- **CRC32 Matching**: O(n*m) where n=arcs, m=episodes per arc
- **Memory Usage**: ~2-3MB for cached metadata
- **Disk Usage**: ~300KB (metadata + version files)

## Testing Checklist

- [ ] Plugin loads in Jellyfin
- [ ] Configuration page appears
- [ ] Series metadata loads
- [ ] Season metadata loads
- [ ] Episodes match by CRC32
- [ ] Episodes match by name
- [ ] Posters download
- [ ] Cache persists across restarts
- [ ] Auto-update task runs
- [ ] Logs show helpful information

## Future Enhancements

Potential improvements:
- Episode-specific descriptions (if added to metadata)
- Series poster/backdrop images
- Multi-language support (if added to metadata)
- Custom metadata override options
- Advanced matching rules configuration
- Statistics/analytics in config page
- Manual refresh button in config
- Webhooks for metadata updates

## License & Attribution

- Plugin code: Custom implementation
- Metadata source: ladyisatis/one-pace-metadata (community-maintained)
- One Pace project: https://onepace.net
- Jellyfin platform: https://jellyfin.org
