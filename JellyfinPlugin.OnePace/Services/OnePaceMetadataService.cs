using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JellyfinPlugin.OnePace.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace JellyfinPlugin.OnePace.Services
{
    /// <summary>
    /// Service for fetching and caching One Pace metadata from GitHub.
    /// </summary>
    public class OnePaceMetadataService
    {
        private const string DataUrl = "https://raw.githubusercontent.com/ladyisatis/one-pace-metadata/main/data.json";
        private const string StatusUrl = "https://raw.githubusercontent.com/ladyisatis/one-pace-metadata/main/status.json";
        private const string EpisodeUrlTemplate = "https://raw.githubusercontent.com/ladyisatis/one-pace-metadata/main/episodes/{0}.yml";
        private const string CacheFileName = "onepace-metadata-cache.json";
        private const string VersionFileName = "onepace-version.txt";

        private static OnePaceMetadataService? _instance;
        private static readonly object _instanceLock = new object();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OnePaceMetadataService> _logger;
        private readonly string _cacheDirectory;

        private OnePaceData? _cachedData;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, EpisodeDetails> _cachedEpisodeDetails = new Dictionary<string, EpisodeDetails>();

        /// <summary>
        /// Gets the singleton instance of the metadata service.
        /// </summary>
        public static OnePaceMetadataService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            throw new InvalidOperationException("OnePaceMetadataService has not been initialized. Call Initialize() first.");
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes the singleton instance.
        /// </summary>
        public static void Initialize(IHttpClientFactory httpClientFactory, ILogger<OnePaceMetadataService> logger, IApplicationPaths applicationPaths)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new OnePaceMetadataService(httpClientFactory, logger, applicationPaths);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnePaceMetadataService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="applicationPaths">Application paths for cache directory.</param>
        private OnePaceMetadataService(
            IHttpClientFactory httpClientFactory,
            ILogger<OnePaceMetadataService> logger,
            IApplicationPaths applicationPaths)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cacheDirectory = Path.Combine(applicationPaths.CachePath, "onepace");

            // Ensure cache directory exists
            Directory.CreateDirectory(_cacheDirectory);
        }

        /// <summary>
        /// Gets the cached metadata or fetches it if cache is invalid.
        /// </summary>
        /// <param name="forceRefresh">Force refresh from remote source.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>One Pace metadata.</returns>
        public async Task<OnePaceData?> GetMetadataAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var config = Plugin.Instance?.Configuration;
                var cacheDuration = TimeSpan.FromHours(config?.CacheDurationHours ?? 24);

                // Check if we have valid cached data
                if (!forceRefresh &&
                    _cachedData != null &&
                    DateTime.UtcNow - _lastCacheTime < cacheDuration)
                {
                    _logger.LogDebug("Returning cached One Pace metadata");
                    return _cachedData;
                }

                // Try to load from file cache first
                if (!forceRefresh && TryLoadFromFileCache(out var fileData))
                {
                    _cachedData = fileData;
                    _lastCacheTime = DateTime.UtcNow;
                    _logger.LogInformation("Loaded One Pace metadata from file cache");
                    return _cachedData;
                }

                // Check version before downloading
                if (!forceRefresh && await ShouldUpdateMetadataAsync(cancellationToken).ConfigureAwait(false))
                {
                    _logger.LogInformation("New One Pace metadata version available, downloading...");
                }
                else if (!forceRefresh)
                {
                    _logger.LogDebug("One Pace metadata is up to date");
                    if (_cachedData != null)
                    {
                        return _cachedData;
                    }
                }

                // Fetch fresh data from GitHub
                _cachedData = await FetchMetadataFromGitHubAsync(cancellationToken).ConfigureAwait(false);

                if (_cachedData != null)
                {
                    _lastCacheTime = DateTime.UtcNow;
                    SaveToFileCache(_cachedData);
                    SaveCurrentVersion(_cachedData.LastUpdateTimestamp);
                    _logger.LogInformation("Successfully fetched and cached One Pace metadata");
                }

                return _cachedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching One Pace metadata");
                return _cachedData; // Return cached data if available
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Fetches metadata directly from GitHub.
        /// </summary>
        private async Task<OnePaceData?> FetchMetadataFromGitHubAsync(CancellationToken cancellationToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                _logger.LogDebug("Fetching One Pace metadata from {Url}", DataUrl);

                var response = await httpClient.GetAsync(DataUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var data = await JsonSerializer.DeserializeAsync<OnePaceData>(jsonStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully fetched One Pace metadata (Version: {Version})", data?.LastUpdateTimestamp);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch One Pace metadata from GitHub");
                throw;
            }
        }

        /// <summary>
        /// Checks if metadata should be updated based on version.
        /// </summary>
        private async Task<bool> ShouldUpdateMetadataAsync(CancellationToken cancellationToken)
        {
            try
            {
                var currentVersion = GetCachedVersion();
                if (currentVersion == 0)
                {
                    return true; // No cached version, need to download
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await httpClient.GetAsync(StatusUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var status = await JsonSerializer.DeserializeAsync<MetadataStatus>(jsonStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (status != null && status.Version > currentVersion)
                {
                    _logger.LogInformation("New version available: {NewVersion} (current: {CurrentVersion})", status.Version, currentVersion);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check metadata version");
                return false; // Don't update if we can't check
            }
        }

        /// <summary>
        /// Tries to load metadata from file cache.
        /// </summary>
        private bool TryLoadFromFileCache(out OnePaceData? data)
        {
            data = null;
            var cacheFile = Path.Combine(_cacheDirectory, CacheFileName);

            if (!File.Exists(cacheFile))
            {
                return false;
            }

            try
            {
                var fileInfo = new FileInfo(cacheFile);
                var config = Plugin.Instance?.Configuration;
                var cacheDuration = TimeSpan.FromHours(config?.CacheDurationHours ?? 24);

                // Check if file cache is still valid
                if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > cacheDuration)
                {
                    _logger.LogDebug("File cache expired");
                    return false;
                }

                var json = File.ReadAllText(cacheFile);
                data = JsonSerializer.Deserialize<OnePaceData>(json);
                return data != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load metadata from file cache");
                return false;
            }
        }

        /// <summary>
        /// Saves metadata to file cache.
        /// </summary>
        private void SaveToFileCache(OnePaceData data)
        {
            try
            {
                var cacheFile = Path.Combine(_cacheDirectory, CacheFileName);
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(cacheFile, json);
                _logger.LogDebug("Saved metadata to file cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save metadata to file cache");
            }
        }

        /// <summary>
        /// Gets the cached version number.
        /// </summary>
        private int GetCachedVersion()
        {
            var versionFile = Path.Combine(_cacheDirectory, VersionFileName);
            if (!File.Exists(versionFile))
            {
                return 0;
            }

            try
            {
                var versionText = File.ReadAllText(versionFile).Trim();
                return int.TryParse(versionText, out var version) ? version : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Saves the current version timestamp.
        /// </summary>
        private void SaveCurrentVersion(double timestamp)
        {
            try
            {
                var versionFile = Path.Combine(_cacheDirectory, VersionFileName);
                File.WriteAllText(versionFile, ((int)timestamp).ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save version info");
            }
        }

        /// <summary>
        /// Gets detailed episode metadata including title and description.
        /// </summary>
        /// <param name="crc32">The CRC32 checksum of the episode.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Episode details or null if not found.</returns>
        public async Task<EpisodeDetails?> GetEpisodeDetailsAsync(string crc32, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(crc32))
            {
                return null;
            }

            // Check cache first
            if (_cachedEpisodeDetails.TryGetValue(crc32, out var cached))
            {
                return cached;
            }

            try
            {
                var url = string.Format(EpisodeUrlTemplate, crc32.ToUpperInvariant());
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                _logger.LogDebug("Fetching episode details for CRC32: {CRC32}", crc32);

                var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Episode details not found for CRC32: {CRC32}", crc32);
                    return null;
                }

                var yamlContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // Parse YAML to JSON (simple approach for this structure)
                var details = ParseEpisodeYaml(yamlContent);

                if (details != null)
                {
                    _cachedEpisodeDetails[crc32] = details;
                    _logger.LogInformation("Fetched episode details: {Title}", details.Title);
                }

                return details;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch episode details for CRC32: {CRC32}", crc32);
                return null;
            }
        }

        /// <summary>
        /// Parses episode YAML content to EpisodeDetails object.
        /// </summary>
        private EpisodeDetails? ParseEpisodeYaml(string yamlContent)
        {
            try
            {
                var details = new EpisodeDetails();

                foreach (var line in yamlContent.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('#') || string.IsNullOrWhiteSpace(trimmed))
                    {
                        continue;
                    }

                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex <= 0)
                    {
                        continue;
                    }

                    var key = trimmed.Substring(0, colonIndex).Trim();
                    var value = trimmed.Substring(colonIndex + 1).Trim().Trim('"', '\'');

                    switch (key)
                    {
                        case "arc":
                            if (int.TryParse(value, out var arc)) details.Arc = arc;
                            break;
                        case "episode":
                            if (int.TryParse(value, out var episode)) details.EpisodeNumber = episode;
                            break;
                        case "title":
                            details.Title = value;
                            break;
                        case "originaltitle":
                            details.OriginalTitle = value;
                            break;
                        case "description":
                            details.Description = value;
                            break;
                        case "chapters":
                            details.Chapters = value;
                            break;
                        case "episodes":
                            details.AnimeEpisodes = value;
                            break;
                        case "released":
                            details.Released = value;
                            break;
                    }
                }

                return string.IsNullOrWhiteSpace(details.Title) ? null : details;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse episode YAML");
                return null;
            }
        }
    }
}
