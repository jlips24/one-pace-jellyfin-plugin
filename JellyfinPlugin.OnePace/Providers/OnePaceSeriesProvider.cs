using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JellyfinPlugin.OnePace.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyfinPlugin.OnePace.Providers
{
    /// <summary>
    /// Provides series-level metadata for One Pace.
    /// </summary>
    public class OnePaceSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
    {
        private readonly OnePaceMetadataService _metadataService;
        private readonly ILogger<OnePaceSeriesProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnePaceSeriesProvider"/> class.
        /// </summary>
        /// <param name="metadataService">Metadata service instance.</param>
        /// <param name="logger">Logger instance.</param>
        public OnePaceSeriesProvider(OnePaceMetadataService metadataService, ILogger<OnePaceSeriesProvider> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "One Pace";

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Searching for series: {Name}", searchInfo.Name);

            // Check if this is One Pace
            if (!IsOnePace(searchInfo.Name))
            {
                _logger.LogDebug("Series name '{Name}' does not match One Pace", searchInfo.Name);
                return Enumerable.Empty<RemoteSearchResult>();
            }

            var metadata = await _metadataService.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.TvShow == null)
            {
                _logger.LogWarning("Failed to fetch One Pace metadata");
                return Enumerable.Empty<RemoteSearchResult>();
            }

            var result = new RemoteSearchResult
            {
                Name = metadata.TvShow.Title,
                PremiereDate = ParseDate(metadata.TvShow.Premiered),
                ProductionYear = int.TryParse(metadata.TvShow.Year, out var year) ? year : null,
                SearchProviderName = Name,
                Overview = metadata.TvShow.Plot
            };

            return new[] { result };
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Getting metadata for series: {Name}", info.Name);

            var result = new MetadataResult<Series>();

            // Check if this is One Pace
            if (!IsOnePace(info.Name))
            {
                _logger.LogDebug("Series name '{Name}' does not match One Pace", info.Name);
                return result;
            }

            var metadata = await _metadataService.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.TvShow == null)
            {
                _logger.LogWarning("Failed to fetch One Pace metadata");
                return result;
            }

            var tvShow = metadata.TvShow;
            var series = new Series
            {
                Name = tvShow.Title,
                OriginalTitle = tvShow.OriginalTitle,
                SortName = tvShow.SortTitle,
                Overview = tvShow.Plot,
                PremiereDate = ParseDate(tvShow.Premiered),
                ProductionYear = int.TryParse(tvShow.Year, out var year) ? year : null,
                OfficialRating = tvShow.CustomRating,
                CommunityRating = null,
                Status = ParseStatus(tvShow.Status)
            };

            // Add genres
            foreach (var genre in tvShow.Genres)
            {
                series.AddGenre(genre);
            }

            result.Item = series;
            result.HasMetadata = true;
            result.Provider = Name;

            _logger.LogInformation("Successfully provided metadata for One Pace series");
            return result;
        }

        /// <summary>
        /// Checks if the series name matches One Pace.
        /// </summary>
        private bool IsOnePace(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var normalized = name.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
            return normalized.Contains("onepace") || normalized == "onepace";
        }

        /// <summary>
        /// Parses a date string in YYYY-MM-DD format.
        /// </summary>
        private DateTime? ParseDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            return null;
        }

        /// <summary>
        /// Parses series status string to SeriesStatus enum.
        /// </summary>
        private SeriesStatus? ParseStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            return status.ToLowerInvariant() switch
            {
                "continuing" => SeriesStatus.Continuing,
                "ended" => SeriesStatus.Ended,
                _ => null
            };
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
