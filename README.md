# One Pace Jellyfin Plugin

A Jellyfin plugin that automatically fetches metadata for [One Pace](https://onepace.net) from the community-maintained GitHub repository.

## Features

- **Automatic Metadata Fetching**: Pulls series, arc (season), and episode metadata from the [ladyisatis/one-pace-metadata](https://github.com/ladyisatis/one-pace-metadata) repository
- **Hybrid Episode Matching**: Matches episodes using either:
  - CRC32 checksums in filenames (e.g., `[D767799C]`)
  - Arc name + episode number from folder structure
- **Poster Artwork**: Automatically downloads arc poster images
- **Smart Caching**: Caches metadata locally and only updates when new versions are available
- **Auto-Updates**: Optional background task to automatically check for metadata updates
- **Arc Descriptions**: Provides detailed descriptions for each One Pace arc

## Installation

### Option 1: Build from Source

1. **Prerequisites**:
   - .NET 8.0 SDK
   - Jellyfin 10.8 or later

2. **Clone and Build**:
   ```bash
   cd /Users/jakelipson/Desktop/one-pace-jellyfin-plugin
   dotnet build -c Release
   ```

3. **Install Plugin**:
   ```bash
   # Copy the built DLL to your Jellyfin plugins directory
   cp JellyfinPlugin.OnePace/bin/Release/net8.0/JellyfinPlugin.OnePace.dll \
      /path/to/jellyfin/plugins/OnePace/
   ```

4. **Restart Jellyfin**

### Option 2: Manual Installation

1. Download the latest `JellyfinPlugin.OnePace.dll` from the releases
2. Create a folder named `OnePace` in your Jellyfin plugins directory:
   - Linux: `/var/lib/jellyfin/plugins/OnePace/`
   - Windows: `%AppData%\Jellyfin\Server\plugins\OnePace\`
   - macOS: `~/.local/share/jellyfin/plugins/OnePace/`
3. Copy the DLL into this folder
4. Restart Jellyfin

## Configuration

After installation, configure the plugin in Jellyfin:

1. Go to **Dashboard** → **Plugins** → **One Pace**
2. Configure the following settings:

### Caching Settings

- **Cache Duration (hours)**: How long to cache metadata before checking for updates (default: 24 hours)

### Auto-Update Settings

- **Enable Auto-Update**: Automatically check for and download new metadata (default: enabled)
- **Auto-Update Interval (hours)**: How often to check for updates (default: 6 hours)

### Episode Matching Settings

- **Prefer CRC32 Matching**: Try CRC32 matching before name-based matching (default: enabled)
  - Recommended if your files have CRC32 checksums in filenames

### Image Settings

- **Download Poster Images**: Automatically download arc poster artwork (default: enabled)

## Library Setup

### File Naming Conventions

The plugin supports two file naming conventions:

#### Option 1: CRC32 in Filename (Recommended)

For official One Pace releases with CRC32 checksums:

```
One Pace/
├── Romance Dawn/
│   ├── [One Pace][1-7] Romance Dawn 01 [1080p][D767799C].mkv
│   ├── [One Pace][8-16] Romance Dawn 02 [1080p][E5F09F49].mkv
│   └── ...
├── Orange Town/
│   └── ...
```

#### Option 2: Simple Folder Structure

For custom-organized files without CRC32:

```
One Pace/
├── Romance Dawn/
│   ├── 01.mkv
│   ├── 02.mkv
│   └── ...
├── Orange Town/
│   ├── 01.mkv
│   └── ...
```

### Jellyfin Library Configuration

1. Create a new library or use an existing TV Shows library
2. Add your One Pace folder as a source
3. In **Library Settings**:
   - Set **Content Type**: TV Shows
   - Set **Preferred Language**: English
   - Enable **Automatically refresh metadata from the internet**
4. Under **Metadata Downloaders**, ensure **One Pace** is checked:
   - Series metadata
   - Season metadata
   - Episode metadata
5. Under **Image Fetchers**, ensure **One Pace** is checked for seasons

### Initial Scan

After setup:

1. Go to your One Pace library
2. Click the **⋮** menu → **Scan Library**
3. The plugin will:
   - Identify the series as "One Pace"
   - Match each arc to its metadata (as seasons)
   - Match episodes using CRC32 or folder structure
   - Download poster images for each arc

## How Episode Matching Works

The plugin uses a **hybrid matching strategy**:

### 1. CRC32 Matching (Primary)

Extracts CRC32 checksums from filenames in brackets:
- File: `[One Pace][1-7] Romance Dawn 01 [1080p][D767799C].mkv`
- Extracted CRC32: `D767799C`
- Matches against metadata to identify: Romance Dawn, Episode 01

### 2. Name-Based Matching (Fallback)

If no CRC32 is found, matches by:
- Season number (arc part number)
- Episode number
- Folder name containing arc title

### 3. Path Structure Matching (Last Resort)

Analyzes folder structure:
- Folder: `Romance Dawn/`
- Episode number from filename: `01.mkv`
- Matches: Romance Dawn, Episode 01

## Metadata Source

This plugin fetches metadata from:
- **Primary Source**: [ladyisatis/one-pace-metadata](https://github.com/ladyisatis/one-pace-metadata)
- **Data URL**: `https://raw.githubusercontent.com/ladyisatis/one-pace-metadata/main/data.json`
- **Update Frequency**: Repository is actively maintained and updated regularly

### Available Metadata

- **Series Level**:
  - Title, original title, sort title
  - Genres (Action, Adventure, Anime, Fantasy, Shounen)
  - Premiere date, status, rating
  - Plot description

- **Arc/Season Level**:
  - Arc title and saga grouping
  - Arc description
  - Poster images
  - Part number (season number)

- **Episode Level**:
  - Episode number within arc
  - Runtime/duration
  - Arc description (episodes don't have individual descriptions)

## Troubleshooting

### Plugin Not Appearing

- Verify the DLL is in the correct plugins folder
- Check Jellyfin logs for plugin loading errors
- Restart Jellyfin server

### Episodes Not Matching

1. **Check file naming**:
   - Ensure CRC32 checksums are in brackets: `[ABC12345]`
   - Or use simple numbered files: `01.mkv`, `02.mkv`

2. **Check folder structure**:
   - Arc folders should match arc names in metadata
   - Example: `Romance Dawn`, `Orange Town`, etc.

3. **Check logs**:
   - Go to Dashboard → Logs
   - Look for "One Pace" entries
   - Check for matching attempts and errors

### Metadata Not Updating

1. **Force refresh**:
   - Go to Dashboard → Scheduled Tasks
   - Run "Update One Pace Metadata" manually

2. **Clear cache**:
   - Delete cache files in: `{JellyfinDataDir}/cache/onepace/`
   - Restart Jellyfin

3. **Check configuration**:
   - Ensure auto-update is enabled
   - Verify cache duration settings

### Posters Not Downloading

1. **Check configuration**:
   - Ensure "Download Poster Images" is enabled

2. **Verify metadata availability**:
   - Not all arcs have poster images in the repository
   - Check the [metadata repository](https://github.com/ladyisatis/one-pace-metadata/tree/main/posters)

3. **Force image refresh**:
   - Right-click on season → Refresh Metadata
   - Check "Replace all metadata"

## Development

### Project Structure

```
JellyfinPlugin.OnePace/
├── Configuration/
│   └── configPage.html          # Admin configuration UI
├── Models/
│   └── OnePaceData.cs           # JSON data models
├── Providers/
│   ├── OnePaceSeriesProvider.cs # Series metadata
│   ├── OnePaceSeasonProvider.cs # Season/arc metadata
│   ├── OnePaceEpisodeProvider.cs # Episode metadata with matching
│   └── OnePaceImageProvider.cs  # Poster images
├── ScheduledTasks/
│   └── MetadataUpdateTask.cs    # Background update task
├── Services/
│   └── OnePaceMetadataService.cs # Metadata fetching & caching
├── Plugin.cs                     # Main plugin entry point
└── PluginConfiguration.cs        # Configuration model
```

### Building

```bash
# Restore dependencies
dotnet restore

# Build in debug mode
dotnet build

# Build in release mode
dotnet build -c Release

# Publish for distribution
dotnet publish -c Release
```

### Testing

1. Build the plugin
2. Copy to Jellyfin plugins directory
3. Restart Jellyfin
4. Add test library with One Pace files
5. Check logs for metadata matching

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## Credits

- **Metadata Source**: [ladyisatis/one-pace-metadata](https://github.com/ladyisatis/one-pace-metadata)
- **One Pace Project**: [onepace.net](https://onepace.net)
- **Inspired by**: [jwueller/jellyfin-plugin-onepace](https://github.com/jwueller/jellyfin-plugin-onepace)

## License

This plugin is provided as-is for use with Jellyfin. The One Pace project and its metadata are maintained by their respective communities.

## Support

For issues and questions:

1. Check the [Troubleshooting](#troubleshooting) section
2. Review Jellyfin logs for errors
3. Open an issue on GitHub with:
   - Jellyfin version
   - Plugin version
   - Log excerpts
   - File naming examples

## Changelog

### Version 1.0.0
- Initial release
- Series, season, and episode metadata providers
- Hybrid episode matching (CRC32 + name-based)
- Poster image downloads
- Smart caching with version tracking
- Auto-update scheduled task
- Configuration UI
