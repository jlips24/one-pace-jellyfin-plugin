using System;
using System.Collections.Generic;
using System.Net.Http;
using JellyfinPlugin.OnePace.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace JellyfinPlugin.OnePace
{
    /// <summary>
    /// One Pace metadata plugin for Jellyfin.
    /// Fetches metadata from the community-maintained GitHub repository.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            // Initialize the metadata service singleton
            var logger = loggerFactory.CreateLogger<OnePaceMetadataService>();
            OnePaceMetadataService.Initialize(httpClientFactory, logger, applicationPaths);
        }

        /// <inheritdoc />
        public override string Name => "One Pace";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("a7f2e6d4-8c3b-4a1f-9e5d-2b8c7a4f1e3d");

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
                }
            };
        }
    }
}
