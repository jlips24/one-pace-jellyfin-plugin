using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JellyfinPlugin.OnePace.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyfinPlugin.OnePace.Providers
{
    /// <summary>
    /// Provides episode-level metadata for One Pace episodes.
    /// Uses hybrid matching: CRC32 checksums first, then falls back to name matching.
    /// </summary>
    public class OnePaceEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        private readonly OnePaceMetadataService _metadataService;
        private readonly ILogger<OnePaceEpisodeProvider> _logger;

        // Regex to extract CRC32 from filename: matches [ABC12345] format
        private static readonly Regex Crc32Regex = new Regex(@"\[([A-F0-9]{8})\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="OnePaceEpisodeProvider"/> class.
        /// </summary>
        /// <param name="metadataService">Metadata service instance.</param>
        /// <param name="logger">Logger instance.</param>
        public OnePaceEpisodeProvider(OnePaceMetadataService metadataService, ILogger<OnePaceEpisodeProvider> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "One Pace";

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Searching for episode: {Name}, Season: {Season}, Episode: {Episode}",
                searchInfo.Name, searchInfo.ParentIndexNumber, searchInfo.IndexNumber);

            if (!IsOnePaceSeries(searchInfo))
            {
                return Enumerable.Empty<RemoteSearchResult>();
            }

            var metadata = await _metadataService.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.Arcs == null)
            {
                return Enumerable.Empty<RemoteSearchResult>();
            }

            var match = await FindEpisodeMatch(searchInfo, metadata).ConfigureAwait(false);
            if (match == null)
            {
                return Enumerable.Empty<RemoteSearchResult>();
            }

            return new[]
            {
                new RemoteSearchResult
                {
                    Name = $"{match.Value.arc.Title} - Episode {match.Value.episodeNumber}",
                    IndexNumber = int.Parse(match.Value.episodeNumber),
                    ParentIndexNumber = match.Value.arc.Part,
                    SearchProviderName = Name
                }
            };
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Getting metadata for episode: {Name}, Season: {Season}, Episode: {Episode}, Path: {Path}",
                info.Name, info.ParentIndexNumber, info.IndexNumber, info.Path);

            var result = new MetadataResult<Episode>();

            if (!IsOnePaceSeries(info))
            {
                return result;
            }

            var metadata = await _metadataService.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.Arcs == null)
            {
                _logger.LogWarning("Failed to fetch One Pace metadata for episode");
                return result;
            }

            var match = await FindEpisodeMatch(info, metadata).ConfigureAwait(false);
            if (match == null)
            {
                _logger.LogDebug("No matching episode found");
                return result;
            }

            var (arc, episodeNumber, episodeData) = match.Value;

            var episode = new Episode
            {
                Name = $"{arc.Title} - Episode {episodeNumber}",
                IndexNumber = int.Parse(episodeNumber),
                ParentIndexNumber = arc.Part,
                Overview = arc.Description // Use arc description since episodes don't have individual descriptions
            };

            // Parse runtime if available
            if (!string.IsNullOrWhiteSpace(episodeData.Length))
            {
                var runtime = ParseRuntime(episodeData.Length);
                if (runtime.HasValue)
                {
                    episode.RunTimeTicks = TimeSpan.FromMinutes(runtime.Value).Ticks;
                }
            }

            result.Item = episode;
            result.HasMetadata = true;
            result.Provider = Name;

            _logger.LogInformation("Successfully provided metadata for One Pace episode: {Arc} - {Episode}",
                arc.Title, episodeNumber);
            return result;
        }

        /// <summary>
        /// Finds a matching episode using hybrid matching strategy.
        /// </summary>
        private Task<(Models.Arc arc, string episodeNumber, Models.Episode episode)?> FindEpisodeMatch(
            EpisodeInfo info,
            Models.OnePaceData metadata)
        {
            var config = Plugin.Instance?.Configuration;
            var preferCrc32 = config?.PreferCrc32Matching ?? true;

            // Strategy 1: Try CRC32 matching if preferred and available
            if (preferCrc32 && !string.IsNullOrWhiteSpace(info.Path))
            {
                var crc32 = ExtractCrc32FromPath(info.Path);
                if (!string.IsNullOrWhiteSpace(crc32))
                {
                    _logger.LogDebug("Attempting CRC32 match with: {CRC32}", crc32);
                    var match = FindByCrc32(crc32, metadata.Arcs);
                    if (match != null)
                    {
                        _logger.LogInformation("Matched episode by CRC32: {CRC32} -> {Arc} Episode {Episode}",
                            crc32, match.Value.arc.Title, match.Value.episodeNumber);
                        return Task.FromResult(match);
                    }
                    _logger.LogDebug("No CRC32 match found, falling back to name matching");
                }
            }

            // Strategy 2: Fall back to season/episode number matching
            if (info.ParentIndexNumber.HasValue && info.IndexNumber.HasValue)
            {
                _logger.LogDebug("Attempting season/episode number match: Season {Season}, Episode {Episode}",
                    info.ParentIndexNumber, info.IndexNumber);
                var match = FindBySeasonEpisode(info.ParentIndexNumber.Value, info.IndexNumber.Value, metadata.Arcs);
                if (match != null)
                {
                    _logger.LogInformation("Matched episode by season/episode: S{Season}E{Episode} -> {Arc} Episode {EpNum}",
                        info.ParentIndexNumber, info.IndexNumber, match.Value.arc.Title, match.Value.episodeNumber);
                    return Task.FromResult(match);
                }
            }

            // Strategy 3: Try to match by path structure (folder name)
            if (!string.IsNullOrWhiteSpace(info.Path))
            {
                var match = FindByPathStructure(info.Path, info.IndexNumber, metadata.Arcs);
                if (match != null)
                {
                    _logger.LogInformation("Matched episode by path structure: {Arc} Episode {Episode}",
                        match.Value.arc.Title, match.Value.episodeNumber);
                    return Task.FromResult(match);
                }
            }

            _logger.LogWarning("Failed to match episode using any strategy");
            return Task.FromResult<(Models.Arc arc, string episodeNumber, Models.Episode episode)?>(null);
        }

        /// <summary>
        /// Extracts CRC32 checksum from file path.
        /// </summary>
        private string? ExtractCrc32FromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var fileName = Path.GetFileNameWithoutExtension(path);
            var match = Crc32Regex.Match(fileName);

            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }

            return null;
        }

        /// <summary>
        /// Finds episode by CRC32 checksum.
        /// </summary>
        private (Models.Arc arc, string episodeNumber, Models.Episode episode)? FindByCrc32(
            string crc32,
            List<Models.Arc> arcs)
        {
            foreach (var arc in arcs)
            {
                foreach (var kvp in arc.Episodes)
                {
                    var episode = kvp.Value;
                    // Check both regular and extended CRC32
                    if (episode.Crc32.Equals(crc32, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(episode.Crc32Extended) &&
                         episode.Crc32Extended.Equals(crc32, StringComparison.OrdinalIgnoreCase)))
                    {
                        return (arc, kvp.Key, episode);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds episode by season (arc part) and episode number.
        /// </summary>
        private (Models.Arc arc, string episodeNumber, Models.Episode episode)? FindBySeasonEpisode(
            int seasonNumber,
            int episodeNumber,
            List<Models.Arc> arcs)
        {
            // Find arc by part number (season number)
            var arc = arcs.FirstOrDefault(a => a.Part == seasonNumber);
            if (arc == null)
            {
                return null;
            }

            // Find episode by number
            var episodeKey = episodeNumber.ToString("D2"); // Format as 01, 02, etc.
            if (arc.Episodes.TryGetValue(episodeKey, out var episode))
            {
                return (arc, episodeKey, episode);
            }

            // Try without leading zero
            episodeKey = episodeNumber.ToString();
            if (arc.Episodes.TryGetValue(episodeKey, out episode))
            {
                return (arc, episodeKey, episode);
            }

            return null;
        }

        /// <summary>
        /// Finds episode by analyzing the path structure (folder names).
        /// </summary>
        private (Models.Arc arc, string episodeNumber, Models.Episode episode)? FindByPathStructure(
            string path,
            int? episodeNumber,
            List<Models.Arc> arcs)
        {
            if (!episodeNumber.HasValue)
            {
                return null;
            }

            // Extract folder name from path
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            var folderName = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return null;
            }

            // Try to find arc by folder name matching
            var arc = arcs.FirstOrDefault(a =>
                folderName.Contains(a.Title, StringComparison.OrdinalIgnoreCase));

            if (arc == null)
            {
                return null;
            }

            // Find episode by number
            var episodeKey = episodeNumber.Value.ToString("D2");
            if (arc.Episodes.TryGetValue(episodeKey, out var episode))
            {
                return (arc, episodeKey, episode);
            }

            episodeKey = episodeNumber.Value.ToString();
            if (arc.Episodes.TryGetValue(episodeKey, out episode))
            {
                return (arc, episodeKey, episode);
            }

            return null;
        }

        /// <summary>
        /// Parses runtime string in MM:SS format to total minutes.
        /// </summary>
        private double? ParseRuntime(string lengthString)
        {
            if (string.IsNullOrWhiteSpace(lengthString))
            {
                return null;
            }

            var parts = lengthString.Split(':');
            if (parts.Length != 2)
            {
                return null;
            }

            if (int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds))
            {
                return minutes + (seconds / 60.0);
            }

            return null;
        }

        /// <summary>
        /// Checks if this episode belongs to a One Pace series.
        /// </summary>
        private bool IsOnePaceSeries(EpisodeInfo info)
        {
            if (info.SeriesProviderIds != null && info.SeriesProviderIds.TryGetValue(Name, out var providerId))
            {
                return !string.IsNullOrWhiteSpace(providerId);
            }

            // Fallback: always return true since we only provide metadata for One Pace
            // The provider will only be invoked for items in a One Pace library
            return true;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
