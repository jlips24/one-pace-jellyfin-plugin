using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// Provides season-level metadata for One Pace arcs.
    /// </summary>
    public class OnePaceSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly OnePaceMetadataService _metadataService;
        private readonly ILogger<OnePaceSeasonProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnePaceSeasonProvider"/> class.
        /// </summary>
        /// <param name="metadataService">Metadata service instance.</param>
        /// <param name="logger">Logger instance.</param>
        public OnePaceSeasonProvider(OnePaceMetadataService metadataService, ILogger<OnePaceSeasonProvider> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "One Pace";

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Searching for season: {Name}, Index: {Index}", searchInfo.Name, searchInfo.IndexNumber);

            if (!IsOnePaceSeries(searchInfo))
            {
                return Enumerable.Empty<RemoteSearchResult>();
            }

            var metadata = await _metadataService.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.Arcs == null)
            {
                return Enumerable.Empty<RemoteSearchResult>();
            }

            // Try to find the arc by season number (part number)
            if (searchInfo.IndexNumber.HasValue)
            {
                var arc = metadata.Arcs.FirstOrDefault(a => a.Part == searchInfo.IndexNumber.Value);
                if (arc != null)
                {
                    return new[]
                    {
                        new RemoteSearchResult
                        {
                            Name = arc.Title,
                            IndexNumber = arc.Part,
                            SearchProviderName = Name,
                            Overview = arc.Description
                        }
                    };
                }
            }

            // Try to find by name
            if (!string.IsNullOrWhiteSpace(searchInfo.Name))
            {
                var arc = metadata.Arcs.FirstOrDefault(a =>
                    a.Title.Equals(searchInfo.Name, StringComparison.OrdinalIgnoreCase));

                if (arc != null)
                {
                    return new[]
                    {
                        new RemoteSearchResult
                        {
                            Name = arc.Title,
                            IndexNumber = arc.Part,
                            SearchProviderName = Name,
                            Overview = arc.Description
                        }
                    };
                }
            }

            return Enumerable.Empty<RemoteSearchResult>();
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Getting metadata for season: {Name}, Index: {Index}", info.Name, info.IndexNumber);

            var result = new MetadataResult<Season>();

            if (!IsOnePaceSeries(info))
            {
                return result;
            }

            var metadata = await _metadataService.GetMetadataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata?.Arcs == null)
            {
                _logger.LogWarning("Failed to fetch One Pace metadata for season");
                return result;
            }

            Models.Arc? arc = null;

            // Try to find the arc by season number (part number)
            if (info.IndexNumber.HasValue)
            {
                arc = metadata.Arcs.FirstOrDefault(a => a.Part == info.IndexNumber.Value);
            }

            // Try to find by name if not found by index
            if (arc == null && !string.IsNullOrWhiteSpace(info.Name))
            {
                arc = metadata.Arcs.FirstOrDefault(a =>
                    a.Title.Equals(info.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (arc == null)
            {
                _logger.LogDebug("No matching arc found for season {Name} (Index: {Index})", info.Name, info.IndexNumber);
                return result;
            }

            var season = new Season
            {
                Name = arc.Title,
                IndexNumber = arc.Part,
                Overview = arc.Description
            };

            result.Item = season;
            result.HasMetadata = true;
            result.Provider = Name;

            _logger.LogInformation("Successfully provided metadata for One Pace season: {Title} (Part {Part})", arc.Title, arc.Part);
            return result;
        }

        /// <summary>
        /// Checks if this season belongs to a One Pace series.
        /// </summary>
        private bool IsOnePaceSeries(SeasonInfo info)
        {
            if (info.SeriesProviderIds != null && info.SeriesProviderIds.TryGetValue(Name, out var providerId))
            {
                return !string.IsNullOrWhiteSpace(providerId);
            }

            // Also check series name as fallback
            var seriesName = info.Name ?? string.Empty;
            var normalized = seriesName.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
            return normalized.Contains("onepace");
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
