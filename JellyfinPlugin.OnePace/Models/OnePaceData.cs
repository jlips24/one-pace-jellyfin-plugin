using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyfinPlugin.OnePace.Models
{
    /// <summary>
    /// Root data model for One Pace metadata from GitHub repository.
    /// </summary>
    public class OnePaceData
    {
        /// <summary>
        /// Gets or sets the last update timestamp in ISO 8601 format.
        /// </summary>
        [JsonPropertyName("last_update")]
        public string LastUpdate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last update timestamp as Unix timestamp.
        /// </summary>
        [JsonPropertyName("last_update_ts")]
        public double LastUpdateTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the base URL for accessing images and other resources.
        /// </summary>
        [JsonPropertyName("base_url")]
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the TV show metadata.
        /// </summary>
        [JsonPropertyName("tvshow")]
        public TvShow TvShow { get; set; } = new TvShow();

        /// <summary>
        /// Gets or sets the list of arcs (seasons).
        /// </summary>
        [JsonPropertyName("arcs")]
        public List<Arc> Arcs { get; set; } = new List<Arc>();
    }

    /// <summary>
    /// Represents series-level metadata.
    /// </summary>
    public class TvShow
    {
        /// <summary>
        /// Gets or sets the series title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sort title for proper ordering.
        /// </summary>
        [JsonPropertyName("sorttitle")]
        public string SortTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original title.
        /// </summary>
        [JsonPropertyName("originaltitle")]
        public string OriginalTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of genres.
        /// </summary>
        [JsonPropertyName("genre")]
        public List<string> Genres { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the premiere date in YYYY-MM-DD format.
        /// </summary>
        [JsonPropertyName("premiered")]
        public string Premiered { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the release date in YYYY-MM-DD format.
        /// </summary>
        [JsonPropertyName("releasedate")]
        public string ReleaseDate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        [JsonPropertyName("year")]
        public string Year { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the series status (e.g., "Continuing").
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the custom rating (e.g., "TV-14").
        /// </summary>
        [JsonPropertyName("customrating")]
        public string CustomRating { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plot/description.
        /// </summary>
        [JsonPropertyName("plot")]
        public string Plot { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an arc (season) in One Pace.
    /// </summary>
    public class Arc
    {
        /// <summary>
        /// Gets or sets the arc part number (used as season number).
        /// </summary>
        [JsonPropertyName("part")]
        public int Part { get; set; }

        /// <summary>
        /// Gets or sets the saga name (e.g., "East Blue Saga").
        /// </summary>
        [JsonPropertyName("saga")]
        public string Saga { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arc title (e.g., "Romance Dawn").
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original title.
        /// </summary>
        [JsonPropertyName("originaltitle")]
        public string OriginalTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arc description/plot.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relative path to the poster image.
        /// </summary>
        [JsonPropertyName("poster")]
        public string Poster { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the dictionary of episodes in this arc.
        /// Key is the episode number as a string (e.g., "01", "02").
        /// </summary>
        [JsonPropertyName("episodes")]
        public Dictionary<string, Episode> Episodes { get; set; } = new Dictionary<string, Episode>();
    }

    /// <summary>
    /// Represents an episode in One Pace.
    /// </summary>
    public class Episode
    {
        /// <summary>
        /// Gets or sets the episode length/duration in MM:SS format.
        /// </summary>
        [JsonPropertyName("length")]
        public string Length { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CRC32 checksum for file matching.
        /// </summary>
        [JsonPropertyName("crc32")]
        public string Crc32 { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CRC32 checksum for extended version.
        /// </summary>
        [JsonPropertyName("crc32_extended")]
        public string Crc32Extended { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tracker ID.
        /// </summary>
        [JsonPropertyName("tid")]
        public string TrackerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tracker ID for extended version.
        /// </summary>
        [JsonPropertyName("tid_extended")]
        public string TrackerIdExtended { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the length for extended version.
        /// </summary>
        [JsonPropertyName("length_extended")]
        public string LengthExtended { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents detailed episode metadata from YAML files.
    /// </summary>
    public class EpisodeDetails
    {
        [JsonPropertyName("arc")]
        public int Arc { get; set; }

        [JsonPropertyName("episode")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("originaltitle")]
        public string OriginalTitle { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("chapters")]
        public string Chapters { get; set; } = string.Empty;

        [JsonPropertyName("episodes")]
        public string AnimeEpisodes { get; set; } = string.Empty;

        [JsonPropertyName("released")]
        public string Released { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents version status from status.json.
    /// </summary>
    public class MetadataStatus
    {
        /// <summary>
        /// Gets or sets the current version number.
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        [JsonPropertyName("last_update")]
        public string LastUpdate { get; set; } = string.Empty;
    }
}
