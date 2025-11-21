using MediaBrowser.Model.Plugins;

namespace JellyfinPlugin.OnePace
{
    /// <summary>
    /// Plugin configuration for One Pace metadata provider.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the cache duration in hours for metadata.
        /// Default is 24 hours.
        /// </summary>
        public int CacheDurationHours { get; set; } = 24;

        /// <summary>
        /// Gets or sets a value indicating whether auto-update is enabled.
        /// When enabled, the plugin will automatically check for and download
        /// new metadata from the GitHub repository.
        /// </summary>
        public bool EnableAutoUpdate { get; set; } = true;

        /// <summary>
        /// Gets or sets the auto-update check interval in hours.
        /// Default is 6 hours.
        /// </summary>
        public int AutoUpdateIntervalHours { get; set; } = 6;

        /// <summary>
        /// Gets or sets a value indicating whether to prefer CRC32 matching.
        /// When enabled, the plugin will first try to match episodes by CRC32
        /// checksums in the filename before falling back to name matching.
        /// </summary>
        public bool PreferCrc32Matching { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to download poster images.
        /// </summary>
        public bool EnablePosterDownload { get; set; } = true;
    }
}
