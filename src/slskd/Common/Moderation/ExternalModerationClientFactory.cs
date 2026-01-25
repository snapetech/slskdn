// <copyright file="ExternalModerationClientFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Net.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Factory for creating external moderation clients based on configuration.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM03: Client factory for mode-based selection.
    ///     Creates Local, Remote, or Noop clients based on Mode setting.
    /// </remarks>
    public sealed class ExternalModerationClientFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<ExternalModerationOptions> _options;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ExternalModerationClientFactory"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="options">The external moderation options.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public ExternalModerationClientFactory(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<ExternalModerationOptions> options,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        ///     Creates an external moderation client based on the current configuration.
        /// </summary>
        /// <returns>The appropriate external moderation client implementation.</returns>
        public IExternalModerationClient CreateClient()
        {
            var opts = _options.CurrentValue;
            var mode = opts.Mode;

            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return new NoopExternalModerationClient();
            }

            var httpClient = _httpClientFactory.CreateClient("ExternalModeration");

            if (string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase))
            {
                var logger = _loggerFactory.CreateLogger<LocalExternalModerationClient>();
                return new LocalExternalModerationClient(httpClient, _options, logger);
            }

            if (string.Equals(mode, "Remote", StringComparison.OrdinalIgnoreCase))
            {
                var logger = _loggerFactory.CreateLogger<RemoteExternalModerationClient>();
                return new RemoteExternalModerationClient(httpClient, _options, logger);
            }

            // Fallback to Noop for invalid modes
            var fallbackLogger = _loggerFactory.CreateLogger<NoopExternalModerationClient>();
            fallbackLogger.LogWarning("Invalid external moderation mode '{Mode}', falling back to Off", mode);
            return new NoopExternalModerationClient();
        }

        /// <summary>
        ///     No-op external moderation client for when external moderation is disabled.
        /// </summary>
        public sealed class NoopExternalModerationClient : IExternalModerationClient
        {
            private readonly ILogger<NoopExternalModerationClient> _logger;

            public NoopExternalModerationClient()
            {
                // Create logger manually since we don't have ILoggerFactory in this context
                // In real usage, this would be injected or created by the factory
                _logger = null!; // Will be set by factory if needed
            }

            public NoopExternalModerationClient(ILogger<NoopExternalModerationClient> logger)
            {
                _logger = logger;
            }

            public Task<ModerationDecision> AnalyzeFileAsync(
                LocalFileMetadata file,
                CancellationToken cancellationToken = default)
            {
                _logger?.LogDebug("[NoopLlm] External moderation disabled, returning Unknown");
                return Task.FromResult(ModerationDecision.Unknown("external_moderation_disabled"));
            }
        }
    }
}


