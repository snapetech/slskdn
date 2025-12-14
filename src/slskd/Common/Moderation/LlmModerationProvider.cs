// <copyright file="LlmModerationProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     LLM-powered moderation provider that implements <see cref="IModerationProvider"/>.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM02: LlmModerationProvider & Composite Integration.
    ///     Bridges the main moderation system with AI-powered content analysis.
    ///     Used as the final check in the moderation pipeline after deterministic providers.
    /// </remarks>
    public sealed class LlmModerationProvider : IModerationProvider
    {
        private readonly IOptionsMonitor<LlmModerationOptions> _options;
        private readonly ILlmModerationProvider _llmProvider;
        private readonly ILogger<LlmModerationProvider> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LlmModerationProvider"/> class.
        /// </summary>
        /// <param name="options">The LLM moderation options.</param>
        /// <param name="llmProvider">The underlying LLM provider.</param>
        /// <param name="logger">The logger.</param>
        public LlmModerationProvider(
            IOptionsMonitor<LlmModerationOptions> options,
            ILlmModerationProvider llmProvider,
            ILogger<LlmModerationProvider> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ModerationDecision> CheckLocalFileAsync(
            LocalFileMetadata file,
            CancellationToken cancellationToken = default)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var opts = _options.CurrentValue;

            // Check if LLM moderation is enabled and available
            if (!opts.Enabled || !_llmProvider.IsAvailable)
            {
                _logger.LogDebug("[LlmProvider] LLM moderation disabled or unavailable, skipping file check");
                return ModerationDecision.Unknown("llm_disabled_or_unavailable");
            }

            // Check if provider can handle file content
            if (!_llmProvider.CanHandleContentType(LlmModeration.ContentType.FileContent))
            {
                _logger.LogDebug("[LlmProvider] LLM provider cannot handle file content, skipping");
                return ModerationDecision.Unknown("llm_cannot_handle_content_type");
            }

            try
            {
                // Create sanitized LLM request with data minimization
                var llmRequest = CreateLlmRequestFromFile(file);

                _logger.LogInformation(
                    "[LlmProvider] Analyzing file | Id={Id} | Size={Size} | Type={ContentType}",
                    file.Id,
                    file.SizeBytes,
                    llmRequest.ContentType);

                // Call LLM provider
                var llmResponse = await _llmProvider.ModerateAsync(llmRequest, cancellationToken);

                // Transform LLM response to moderation decision
                var decision = CreateDecisionFromLlmResponse(llmResponse, file.Id);

                _logger.LogInformation(
                    "[LlmProvider] File analysis complete | Id={Id} | Verdict={Verdict} | Confidence={Confidence:F2} | Categories={Categories}",
                    file.Id,
                    decision.Verdict,
                    llmResponse.Confidence,
                    llmResponse.Categories);

                return decision;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LlmProvider] LLM analysis failed for file | Id={Id}", file.Id);

                // Apply failsafe behavior
                var fallback = opts.FallbackBehavior;
                if (fallback == "block")
                {
                    _logger.LogWarning("[LlmProvider] Failsafe mode 'block' activated for file | Id={Id}", file.Id);
                    return ModerationDecision.Block("llm_analysis_failed_failsafe_block");
                }

                // For "allow" or "pass_to_next_provider", return unknown
                return ModerationDecision.Unknown("llm_analysis_failed");
            }
        }

        /// <inheritdoc/>
        public async Task<ModerationDecision> CheckContentIdAsync(
            string contentId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                throw new ArgumentException("Content ID cannot be null or empty.", nameof(contentId));
            }

            var opts = _options.CurrentValue;

            // Check if LLM moderation is enabled and available
            if (!opts.Enabled || !_llmProvider.IsAvailable)
            {
                _logger.LogDebug("[LlmProvider] LLM moderation disabled or unavailable, skipping content ID check");
                return ModerationDecision.Unknown("llm_disabled_or_unavailable");
            }

            // Check if provider can handle metadata content
            if (!_llmProvider.CanHandleContentType(LlmModeration.ContentType.Metadata))
            {
                _logger.LogDebug("[LlmProvider] LLM provider cannot handle metadata, skipping");
                return ModerationDecision.Unknown("llm_cannot_handle_content_type");
            }

            try
            {
                // Create LLM request for content ID analysis
                var llmRequest = new LlmModerationRequest
                {
                    ContentType = LlmModeration.ContentType.Metadata,
                    Content = $"Content ID: {SanitizeContentId(contentId)}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["content_id"] = SanitizeContentId(contentId),
                        ["content_id_length"] = contentId.Length.ToString()
                    },
                    Context = "content_id_analysis",
                    Source = "content_moderation_pipeline"
                };

                _logger.LogInformation(
                    "[LlmProvider] Analyzing content ID | Id={ContentId}",
                    SanitizeContentId(contentId));

                // Call LLM provider
                var llmResponse = await _llmProvider.ModerateAsync(llmRequest, cancellationToken);

                // Transform LLM response to moderation decision
                var decision = CreateDecisionFromLlmResponse(llmResponse, contentId);

                _logger.LogInformation(
                    "[LlmProvider] Content ID analysis complete | Verdict={Verdict} | Confidence={Confidence:F2}",
                    decision.Verdict,
                    llmResponse.Confidence);

                return decision;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LlmProvider] LLM analysis failed for content ID | Id={ContentId}", SanitizeContentId(contentId));

                // Apply failsafe behavior
                var fallback = opts.FallbackBehavior;
                if (fallback == "block")
                {
                    return ModerationDecision.Block("llm_analysis_failed_failsafe_block");
                }

                return ModerationDecision.Unknown("llm_analysis_failed");
            }
        }

        /// <summary>
        ///     Creates an LLM moderation request from file metadata with data minimization and sanitization.
        /// </summary>
        /// <param name="file">The file metadata.</param>
        /// <returns>The LLM moderation request.</returns>
        private LlmModerationRequest CreateLlmRequestFromFile(LocalFileMetadata file)
        {
            // Data minimization: only include necessary, sanitized information
            var sanitizedFilename = SanitizeFilename(file.Id);
            var content = BuildFileContentDescription(file, sanitizedFilename);
            var metadata = BuildFileMetadata(file, sanitizedFilename);

            return new LlmModerationRequest
            {
                ContentType = LlmModeration.ContentType.FileContent,
                Content = content,
                Metadata = metadata,
                Context = "file_upload_moderation",
                Source = "local_file_system"
            };
        }

        /// <summary>
        ///     Builds a content description from file metadata.
        /// </summary>
        /// <param name="file">The file metadata.</param>
        /// <param name="sanitizedFilename">The sanitized filename.</param>
        /// <returns>The content description.</returns>
        private string BuildFileContentDescription(LocalFileMetadata file, string sanitizedFilename)
        {
            var parts = new List<string>
            {
                $"Filename: {sanitizedFilename}",
                $"Size: {file.SizeBytes} bytes"
            };

            if (!string.IsNullOrEmpty(file.MediaInfo))
            {
                parts.Add($"Media Info: {TruncateContent(file.MediaInfo, 200)}");
            }

            // Truncate total content if too long
            var content = string.Join(", ", parts);
            return TruncateContent(content, _options.CurrentValue.MaxContentLength);
        }

        /// <summary>
        ///     Builds metadata dictionary from file information.
        /// </summary>
        /// <param name="file">The file metadata.</param>
        /// <param name="sanitizedFilename">The sanitized filename.</param>
        /// <returns>The metadata dictionary.</returns>
        private Dictionary<string, string> BuildFileMetadata(LocalFileMetadata file, string sanitizedFilename)
        {
            var metadata = new Dictionary<string, string>
            {
                ["filename"] = sanitizedFilename,
                ["size_bytes"] = file.SizeBytes.ToString(),
                ["file_extension"] = GetFileExtension(sanitizedFilename),
                ["media_info_length"] = file.MediaInfo?.Length.ToString() ?? "0"
            };

            // Add truncated hash (first 16 chars for identification without full exposure)
            if (!string.IsNullOrEmpty(file.PrimaryHash))
            {
                metadata["hash_prefix"] = file.PrimaryHash.Length > 16
                    ? file.PrimaryHash.Substring(0, 16) + "..."
                    : file.PrimaryHash;
            }

            return metadata;
        }

        /// <summary>
        ///     Creates a ModerationDecision from an LLM moderation response.
        /// </summary>
        /// <param name="llmResponse">The LLM response.</param>
        /// <param name="itemId">The item identifier for logging.</param>
        /// <returns>The moderation decision.</returns>
        private ModerationDecision CreateDecisionFromLlmResponse(LlmModerationResponse llmResponse, string itemId)
        {
            var opts = _options.CurrentValue;

            // Check confidence threshold
            if (llmResponse.Confidence < opts.MinConfidenceThreshold)
            {
                _logger.LogDebug(
                    "[LlmProvider] LLM confidence {Confidence:F2} below threshold {Threshold:F2}, returning unknown | ItemId={ItemId}",
                    llmResponse.Confidence,
                    opts.MinConfidenceThreshold,
                    SanitizeItemId(itemId));

                return ModerationDecision.Unknown($"llm_confidence_too_low_{llmResponse.Confidence:F2}");
            }

            // Map LLM verdict to moderation decision
            var verdict = llmResponse.Verdict;
            var reason = $"llm:{llmResponse.Reasoning}";
            var provider = "provider:llm";

            // Add category information to reason if present
            if (llmResponse.Categories != LlmModeration.ContentCategory.None)
            {
                reason += $"_categories:{llmResponse.Categories}";
            }

            switch (verdict)
            {
                case ModerationVerdict.Blocked:
                    return ModerationDecision.Block(reason, provider);

                case ModerationVerdict.Quarantined:
                    return ModerationDecision.Quarantine(reason, provider);

                case ModerationVerdict.Allowed:
                    // For allowed content, we still want to track the LLM analysis
                    return ModerationDecision.Allow(reason, provider);

                case ModerationVerdict.Unknown:
                default:
                    return ModerationDecision.Unknown($"llm_unknown_{llmResponse.Confidence:F2}", provider);
            }
        }

        /// <summary>
        ///     Sanitizes a filename by removing path information and sensitive data.
        /// </summary>
        /// <param name="filename">The filename to sanitize.</param>
        /// <returns>The sanitized filename.</returns>
        private static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return "unknown";
            }

            // Remove path information - keep only the filename
            var lastSeparator = filename.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSeparator >= 0)
            {
                filename = filename.Substring(lastSeparator + 1);
            }

            // Limit length to prevent abuse
            if (filename.Length > 255)
            {
                filename = filename.Substring(0, 252) + "...";
            }

            return filename;
        }

        /// <summary>
        ///     Sanitizes a content ID for logging (removes sensitive patterns).
        /// </summary>
        /// <param name="contentId">The content ID to sanitize.</param>
        /// <returns>The sanitized content ID.</returns>
        private static string SanitizeContentId(string contentId)
        {
            if (string.IsNullOrEmpty(contentId) || contentId.Length <= 8)
            {
                return contentId;
            }

            // For long content IDs, show prefix + suffix to maintain some identification value
            return contentId.Substring(0, 4) + "..." + contentId.Substring(contentId.Length - 4);
        }

        /// <summary>
        ///     Sanitizes an item ID for logging.
        /// </summary>
        /// <param name="itemId">The item ID to sanitize.</param>
        /// <returns>The sanitized item ID.</returns>
        private static string SanitizeItemId(string itemId)
        {
            return SanitizeFilename(itemId); // Reuse filename sanitization
        }

        /// <summary>
        ///     Gets the file extension from a filename.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The file extension (lowercase, without dot).</returns>
        private static string GetFileExtension(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return "unknown";
            }

            var lastDot = filename.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < filename.Length - 1)
            {
                return filename.Substring(lastDot + 1).ToLowerInvariant();
            }

            return "no_extension";
        }

        /// <inheritdoc />
        public Task ReportContentAsync(string contentId, ModerationReport report, CancellationToken ct)
        {
            // LLM provider doesn't handle content reporting - delegate to other providers
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ReportPeerAsync(string peerId, PeerReport report, CancellationToken ct)
        {
            // LLM provider doesn't handle peer reporting - delegate to other providers
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Truncates content to a maximum length.
        /// </summary>
        /// <param name="content">The content to truncate.</param>
        /// <param name="maxLength">The maximum length.</param>
        /// <returns>The truncated content.</returns>
        private static string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            {
                return content;
            }

            return content.Substring(0, maxLength - 3) + "...";
        }
    }
}
