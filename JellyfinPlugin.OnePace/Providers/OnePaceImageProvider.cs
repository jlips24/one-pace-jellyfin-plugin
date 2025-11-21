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
            // Support Season entities (arcs) for One Pace
            if (item is Season season)
            {
                var seriesName = season.SeriesName ?? string.Empty;
                var normalized = seriesName.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
                return normalized.Contains("onepace");
            }

            return false;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            // Only provide Primary (poster) images for seasons
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

            if (item is not Season season)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var metadata = await OnePaceMetadataService.Instance.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.Arcs == null || string.IsNullOrWhiteSpace(metadata.BaseUrl))
            {
                _logger.LogWarning("Failed to fetch One Pace metadata for images");
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

            // Check if poster is available
            if (string.IsNullOrWhiteSpace(arc.Poster))
            {
                _logger.LogDebug("No poster available for arc: {Arc}", arc.Title);
                return Enumerable.Empty<RemoteImageInfo>();
            }

            // Construct full poster URL
            var posterUrl = $"{metadata.BaseUrl.TrimEnd('/')}/{arc.Poster.TrimStart('/')}";

            _logger.LogInformation("Found poster for arc {Arc}: {Url}", arc.Title, posterUrl);

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
