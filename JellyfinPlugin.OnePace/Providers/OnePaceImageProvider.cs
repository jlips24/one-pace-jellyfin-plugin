using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JellyfinPlugin.OnePace.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyfinPlugin.OnePace.Providers
{
    /// <summary>
    /// Provides poster images for One Pace seasons (arcs).
    /// </summary>
    public class OnePaceImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OnePaceImageProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnePaceImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="logger">Logger instance.</param>
        public OnePaceImageProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<OnePaceImageProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "One Pace";

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            // Support Series and Season entities for One Pace
            if (item is Series series)
            {
                // Check if this series has our provider ID
                if (series.ProviderIds != null && series.ProviderIds.ContainsKey(Name))
                {
                    return true;
                }

                // Fallback to name matching
                var normalized = series.Name?.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty) ?? string.Empty;
                return normalized.Contains("onepace") || normalized.Contains("onepiece");
            }

            if (item is Season season)
            {
                // Check if the parent series has our provider ID
                if (season.Series?.ProviderIds != null && season.Series.ProviderIds.ContainsKey(Name))
                {
                    return true;
                }

                // Fallback to name matching
                var seriesName = season.SeriesName ?? string.Empty;
                var normalized = seriesName.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
                return normalized.Contains("onepace") || normalized.Contains("onepiece");
            }

            return false;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            // Provide Primary (poster) images for both series and seasons
            return new[] { ImageType.Primary };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Getting images for item: {Name}, Type: {Type}", item.Name, item.GetType().Name);

            var config = Plugin.Instance?.Configuration;
            if (config?.EnablePosterDownload != true)
            {
                _logger.LogDebug("Poster download is disabled in configuration");
                return Enumerable.Empty<RemoteImageInfo>();
            }

            // Handle Series (show) posters
            if (item is Series)
            {
                return GetSeriesImages();
            }

            // Handle Season (arc) posters
            if (item is Season season)
            {
                return await GetSeasonImagesAsync(season, cancellationToken).ConfigureAwait(false);
            }

            return Enumerable.Empty<RemoteImageInfo>();
        }

        /// <summary>
        /// Gets series-level poster images from SpykerNZ repository.
        /// </summary>
        private IEnumerable<RemoteImageInfo> GetSeriesImages()
        {
            const string BaseUrl = "https://raw.githubusercontent.com/SpykerNZ/one-pace-for-plex/main/One%20Pace";

            _logger.LogInformation("Providing One Pace series poster from SpykerNZ repository");

            return new[]
            {
                new RemoteImageInfo
                {
                    Url = $"{BaseUrl}/poster.png",
                    Type = ImageType.Primary,
                    ProviderName = Name
                }
            };
        }

        /// <summary>
        /// Gets season-level poster images from SpykerNZ repository.
        /// </summary>
        private async Task<IEnumerable<RemoteImageInfo>> GetSeasonImagesAsync(Season season, CancellationToken cancellationToken)
        {
            const string BaseUrl = "https://raw.githubusercontent.com/SpykerNZ/one-pace-for-plex/main/One%20Pace";

            // Need to get season number from metadata
            var metadata = await OnePaceMetadataService.Instance.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.Arcs == null)
            {
                _logger.LogWarning("Failed to fetch One Pace metadata for season images");
                return Enumerable.Empty<RemoteImageInfo>();
            }

            Models.Arc? arc = null;

            // Try to find the arc by season index
            if (season.IndexNumber.HasValue)
            {
                arc = metadata.Arcs.FirstOrDefault(a => a.Part == season.IndexNumber.Value);
            }

            // Try to find by name if not found by index
            if (arc == null && !string.IsNullOrWhiteSpace(season.Name))
            {
                arc = metadata.Arcs.FirstOrDefault(a =>
                    a.Title.Equals(season.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (arc == null)
            {
                _logger.LogDebug("No matching arc found for season {Name} (Index: {Index})", season.Name, season.IndexNumber);
                return Enumerable.Empty<RemoteImageInfo>();
            }

            // Construct SpykerNZ poster URL
            string posterUrl;
            if (arc.Part == 0)
            {
                // Specials season
                posterUrl = $"{BaseUrl}/season-specials-poster.png";
            }
            else
            {
                // Regular season (01-36)
                posterUrl = $"{BaseUrl}/season{arc.Part:D2}-poster.png";
            }

            _logger.LogInformation("Found poster for arc {Arc} (Part {Part}): {Url}", arc.Title, arc.Part, posterUrl);

            return new[]
            {
                new RemoteImageInfo
                {
                    Url = posterUrl,
                    Type = ImageType.Primary,
                    ProviderName = Name
                }
            };
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Fetching image from URL: {Url}", url);

            var httpClient = _httpClientFactory.CreateClient();
            return httpClient.GetAsync(url, cancellationToken);
        }
    }
}
