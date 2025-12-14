// <copyright file="NoopLlmModerationProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     No-operation LLM moderation provider.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM01: Default LLM provider that does nothing.
    ///     Used when LLM moderation is disabled or unavailable.
    ///     Always returns Unknown verdict to allow other providers to decide.
    /// </remarks>
    public sealed class NoopLlmModerationProvider : ILlmModerationProvider
    {
        private readonly ILogger<NoopLlmModerationProvider> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NoopLlmModerationProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public NoopLlmModerationProvider(ILogger<NoopLlmModerationProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public string ProviderName => "NoopLlmModerationProvider";

        /// <inheritdoc/>
        public async Task<LlmModerationResponse> ModerateAsync(
            LlmModerationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Log the request for debugging (without sensitive content)
            _logger.LogDebug(
                "[NoopLlm] Processing moderation request {RequestId} for {ContentType} content from {Source}",
                request.RequestId,
                request.ContentType,
                request.Source);

            // Simulate some processing time
            await Task.Delay(1, cancellationToken);

            var response = new LlmModerationResponse
            {
                RequestId = request.RequestId,
                Verdict = ModerationVerdict.Unknown, // Pass to next provider
                Severity = LlmModeration.SeverityLevel.Safe,
                Categories = LlmModeration.ContentCategory.None,
                Confidence = 0.0, // No confidence
                Reasoning = "LLM moderation disabled or unavailable",
                Details = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["provider"] = ProviderName,
                    ["status"] = "noop"
                },
                ProcessingTime = TimeSpan.Zero,
                FromCache = false
            };

            _logger.LogDebug(
                "[NoopLlm] Completed moderation request {RequestId} with verdict {Verdict}",
                request.RequestId,
                response.Verdict);

            return response;
        }

        /// <inheritdoc/>
        public bool CanHandleContentType(LlmModeration.ContentType contentType)
        {
            // Noop provider can "handle" any content type (by doing nothing)
            return true;
        }

        /// <inheritdoc/>
        public async Task<LlmProviderHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            return new LlmProviderHealthStatus
            {
                IsHealthy = true,
                ProviderName = ProviderName,
                LastSuccessfulRequest = DateTimeOffset.UtcNow,
                Details = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["status"] = "always_healthy",
                    ["description"] = "No-operation provider, always available"
                }
            };
        }
    }
}

