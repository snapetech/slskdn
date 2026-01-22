// <copyright file="ILlmModerationProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for LLM-powered content moderation providers.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM01 LLM Moderation Abstractions and Config.
    ///     Provides AI-powered content analysis for enhanced moderation decisions.
    /// </remarks>
    public interface ILlmModerationProvider
    {
        /// <summary>
        ///     Gets a value indicating whether this provider is available and configured.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        ///     Gets the name of this moderation provider.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        ///     Moderates content using LLM analysis.
        /// </summary>
        /// <param name="request">The moderation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The moderation response.</returns>
        /// <remarks>
        ///     This method should:
        ///     - Validate input parameters
        ///     - Check rate limits and availability
        ///     - Call the LLM service
        ///     - Parse and validate the response
        ///     - Handle errors gracefully
        ///     - Log appropriate information (without sensitive data)
        /// </remarks>
        Task<LlmModerationResponse> ModerateAsync(
            LlmModerationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Checks if this provider can handle the given content type.
        /// </summary>
        /// <param name="contentType">The content type to check.</param>
        /// <returns>True if this provider can handle the content type.</returns>
        bool CanHandleContentType(LlmModeration.ContentType contentType);

        /// <summary>
        ///     Gets health/status information about this provider.
        /// </summary>
        /// <returns>Health status information.</returns>
        Task<LlmProviderHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Health status information for an LLM moderation provider.
    /// </summary>
    public sealed class LlmProviderHealthStatus
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the provider is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        ///     Gets or sets the provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the last successful request timestamp.
        /// </summary>
        public DateTimeOffset? LastSuccessfulRequest { get; set; }

        /// <summary>
        ///     Gets or sets the last error timestamp.
        /// </summary>
        public DateTimeOffset? LastError { get; set; }

        /// <summary>
        ///     Gets or sets the last error message.
        /// </summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>
        ///     Gets or sets the average response time.
        /// </summary>
        public TimeSpan? AverageResponseTime { get; set; }

        /// <summary>
        ///     Gets or sets the current request rate (requests per minute).
        /// </summary>
        public double CurrentRequestRate { get; set; }

        /// <summary>
        ///     Gets or sets additional status details.
        /// </summary>
        public Dictionary<string, string> Details { get; set; } = new();
    }
}


