using System;
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
        private const string CacheFileName = "onepace-metadata-cache.json";
        private const string VersionFileName = "onepace-version.txt";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OnePaceMetadataService> _logger;
        private readonly string _cacheDirectory;

        private OnePaceData? _cachedData;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="OnePaceMetadataService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="applicationPaths">Application paths for cache directory.</param>
        public OnePaceMetadataService(
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
    }
}
