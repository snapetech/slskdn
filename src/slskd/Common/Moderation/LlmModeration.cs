// <copyright file="LlmModeration.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     LLM-powered moderation capabilities for slskdN.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM01: LLM Moderation Abstractions & Config.
    ///     Provides AI-powered content analysis for advanced moderation decisions.
    /// </remarks>
    public static class LlmModeration
    {
        /// <summary>
        ///     Types of content that can be moderated by LLM.
        /// </summary>
        public enum ContentType
        {
            /// <summary>
            ///     File content (binary data).
            /// </summary>
            FileContent,

            /// <summary>
            ///     Text content (chat messages, filenames, descriptions).
            /// </summary>
            Text,

            /// <summary>
            ///     Metadata (tags, descriptions, comments).
            /// </summary>
            Metadata,

            /// <summary>
            ///     User profile information.
            /// </summary>
            UserProfile,

            /// <summary>
            ///     Social content (posts, comments, shares).
            /// </summary>
            SocialContent
        }

        /// <summary>
        ///     Severity levels for moderation decisions.
        /// </summary>
        public enum SeverityLevel
        {
            /// <summary>
            ///     Content is safe and appropriate.
            /// </summary>
            Safe,

            /// <summary>
            ///     Content may be questionable but not clearly inappropriate.
            /// </summary>
            Questionable,

            /// <summary>
            ///     Content is inappropriate and should be blocked.
            /// </summary>
            Inappropriate,

            /// <summary>
            ///     Content is harmful and should be quarantined.
            /// </summary>
            Harmful
        }

        /// <summary>
        ///     Categories of inappropriate content.
        /// </summary>
        [Flags]
        public enum ContentCategory
        {
            /// <summary>
            ///     No issues detected.
            /// </summary>
            None = 0,

            /// <summary>
            ///     Contains hate speech or discriminatory content.
            /// </summary>
            HateSpeech = 1 << 0,

            /// <summary>
            ///     Contains explicit sexual content.
            /// </summary>
            SexualContent = 1 << 1,

            /// <summary>
            ///     Contains violence or gore.
            /// </summary>
            Violence = 1 << 2,

            /// <summary>
            ///     Contains illegal content (copyright infringement, etc.).
            /// </summary>
            Illegal = 1 << 3,

            /// <summary>
            ///     Contains spam or manipulative content.
            /// </summary>
            Spam = 1 << 4,

            /// <summary>
            ///     Contains malware or security threats.
            /// </summary>
            Malware = 1 << 5,

            /// <summary>
            ///     Contains private or personal information.
            /// </summary>
            PersonalInfo = 1 << 6,

            /// <summary>
            ///     Other categories of inappropriate content.
            /// </summary>
            Other = 1 << 7
        }
    }

    /// <summary>
    ///     Request for LLM-powered content moderation.
    /// </summary>
    public sealed class LlmModerationRequest
    {
        /// <summary>
        ///     Gets or sets the unique identifier for this request.
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        ///     Gets or sets the type of content being moderated.
        /// </summary>
        public LlmModeration.ContentType ContentType { get; set; }

        /// <summary>
        ///     Gets or sets the primary content to analyze.
        /// </summary>
        /// <remarks>
        ///     For text content, this is the text itself.
        ///     For files, this may be extracted text or a description.
        ///     Limited to reasonable size to prevent abuse.
        /// </remarks>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets additional metadata about the content.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        ///     Gets or sets the context in which the content appears.
        /// </summary>
        /// <remarks>
        ///     Provides additional context like "chat message", "filename", "user bio", etc.
        /// </remarks>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the source of the content (anonymized).
        /// </summary>
        /// <remarks>
        ///     Used for logging and debugging, should not contain PII.
        /// </remarks>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the timestamp when the request was created.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Response from LLM-powered content moderation.
    /// </summary>
    public sealed class LlmModerationResponse
    {
        /// <summary>
        ///     Gets or sets the request ID this response corresponds to.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the overall moderation decision.
        /// </summary>
        public ModerationVerdict Verdict { get; set; } = ModerationVerdict.Unknown;

        /// <summary>
        ///     Gets or sets the severity level of any issues found.
        /// </summary>
        public LlmModeration.SeverityLevel Severity { get; set; } = LlmModeration.SeverityLevel.Safe;

        /// <summary>
        ///     Gets or sets the categories of inappropriate content detected.
        /// </summary>
        public LlmModeration.ContentCategory Categories { get; set; } = LlmModeration.ContentCategory.None;

        /// <summary>
        ///     Gets or sets the confidence score (0.0 to 1.0).
        /// </summary>
        /// <remarks>
        ///     Higher values indicate higher confidence in the decision.
        ///     Used for fallback logic and threshold decisions.
        /// </remarks>
        public double Confidence { get; set; }

        /// <summary>
        ///     Gets or sets the reasoning for the decision.
        /// </summary>
        /// <remarks>
        ///     Human-readable explanation of why this decision was made.
        ///     Should be sanitized and not contain sensitive information.
        /// </remarks>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets additional analysis details.
        /// </summary>
        public Dictionary<string, string> Details { get; set; } = new();

        /// <summary>
        ///     Gets or sets the timestamp when the response was generated.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Gets or sets the processing time.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this response came from cache.
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        ///     Gets or sets error information if the moderation failed.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    ///     Configuration for LLM moderation.
    /// </summary>
    public sealed class LlmModerationOptions
    {
        /// <summary>
        ///     Gets or sets a value indicating whether LLM moderation is enabled.
        /// </summary>
        /// <remarks>
        ///     Default: false (LLM moderation disabled by default for safety).
        /// </remarks>
        public bool Enabled { get; set; } = false;

        /// <summary>
        ///     Gets or sets the LLM endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the API key for authentication.
        /// </summary>
        /// <remarks>
        ///     Should be stored securely, not in plain text config.
        /// </remarks>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the model name to use.
        /// </summary>
        /// <remarks>
        ///     Examples: "gpt-4", "claude-3", "llama-2-70b", etc.
        /// </remarks>
        public string Model { get; set; } = "gpt-4";

        /// <summary>
        ///     Gets or sets the minimum confidence threshold for decisions.
        /// </summary>
        /// <remarks>
        ///     Decisions below this threshold will fallback to other moderation methods.
        ///     Range: 0.0 to 1.0, default: 0.8
        /// </remarks>
        public double MinConfidenceThreshold { get; set; } = 0.8;

        /// <summary>
        ///     Gets or sets the timeout for LLM requests.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        ///     Gets or sets the maximum content length to send to LLM.
        /// </summary>
        /// <remarks>
        ///     Prevents abuse by limiting input size.
        /// </remarks>
        public int MaxContentLength { get; set; } = 10000;

        /// <summary>
        ///     Gets or sets the cache TTL for moderation results.
        /// </summary>
        /// <remarks>
        ///     Reduces API calls for repeated content.
        /// </remarks>
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        ///     Gets or sets the fallback behavior when LLM is unavailable.
        /// </summary>
        /// <remarks>
        ///     Options: "allow", "block", "pass_to_next_provider"
        /// </remarks>
        public string FallbackBehavior { get; set; } = "pass_to_next_provider";

        /// <summary>
        ///     Gets or sets the rate limiting configuration.
        /// </summary>
        public LlmRateLimitOptions RateLimiting { get; set; } = new();
    }

    /// <summary>
    ///     Rate limiting configuration for LLM moderation.
    /// </summary>
    public sealed class LlmRateLimitOptions
    {
        /// <summary>
        ///     Gets or sets the maximum requests per minute.
        /// </summary>
        public int RequestsPerMinute { get; set; } = 60;

        /// <summary>
        ///     Gets or sets the maximum requests per hour.
        /// </summary>
        public int RequestsPerHour { get; set; } = 1000;

        /// <summary>
        ///     Gets or sets the burst limit.
        /// </summary>
        public int BurstLimit { get; set; } = 10;
    }
}
