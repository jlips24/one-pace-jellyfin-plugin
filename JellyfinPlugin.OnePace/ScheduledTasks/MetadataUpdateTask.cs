using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JellyfinPlugin.OnePace.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinPlugin.OnePace.ScheduledTasks
{
    /// <summary>
    /// Scheduled task to automatically update One Pace metadata.
    /// </summary>
    public class MetadataUpdateTask : IScheduledTask
    {
        private readonly OnePaceMetadataService _metadataService;
        private readonly ILogger<MetadataUpdateTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataUpdateTask"/> class.
        /// </summary>
        /// <param name="metadataService">Metadata service instance.</param>
        /// <param name="logger">Logger instance.</param>
        public MetadataUpdateTask(OnePaceMetadataService metadataService, ILogger<MetadataUpdateTask> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Update One Pace Metadata";

        /// <inheritdoc />
        public string Description => "Checks for and downloads updated metadata from the One Pace GitHub repository.";

        /// <inheritdoc />
        public string Category => "One Pace";

        /// <inheritdoc />
        public string Key => "OnePaceMetadataUpdate";

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting One Pace metadata update task");

            try
            {
                progress?.Report(0);

                var config = Plugin.Instance?.Configuration;
                if (config?.EnableAutoUpdate != true)
                {
                    _logger.LogInformation("Auto-update is disabled, skipping metadata update");
                    progress?.Report(100);
                    return;
                }

                progress?.Report(25);

                // Force refresh the metadata
                var metadata = await _metadataService.GetMetadataAsync(forceRefresh: true, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                progress?.Report(75);

                if (metadata != null)
                {
                    _logger.LogInformation("Successfully updated One Pace metadata (Version: {Version}, Arcs: {ArcCount})",
                        metadata.LastUpdateTimestamp, metadata.Arcs?.Count ?? 0);
                }
                else
                {
                    _logger.LogWarning("Failed to update One Pace metadata");
                }

                progress?.Report(100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating One Pace metadata");
                throw;
            }
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var config = Plugin.Instance?.Configuration;
            var intervalHours = config?.AutoUpdateIntervalHours ?? 6;

            // Run daily at 3 AM by default, or based on configured interval
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(intervalHours).Ticks
                }
            };
        }
    }
}
