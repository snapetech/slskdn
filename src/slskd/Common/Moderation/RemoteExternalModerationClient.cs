// <copyright file="RemoteExternalModerationClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Remote external moderation client for LLM-powered content analysis.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM03: Remote LLM client implementation with SSRF protection.
    ///     Calls remote LLM APIs for content moderation with security controls.
    /// </remarks>
    public sealed class RemoteExternalModerationClient : IExternalModerationClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<ExternalModerationOptions> _options;
        private readonly ILogger<RemoteExternalModerationClient> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RemoteExternalModerationClient"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for API calls.</param>
        /// <param name="options">The external moderation options.</param>
        /// <param name="logger">The logger.</param>
        public RemoteExternalModerationClient(
            HttpClient httpClient,
            IOptionsMonitor<ExternalModerationOptions> options,
            ILogger<RemoteExternalModerationClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _rateLimiter = new SemaphoreSlim(1, 1);
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.CurrentValue.TimeoutSeconds);
        }

        /// <inheritdoc/>
        public async Task<ModerationDecision> AnalyzeFileAsync(
            LocalFileMetadata file,
            CancellationToken cancellationToken = default)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var opts = _options.CurrentValue;

            // Check if external moderation is enabled
            if (!opts.Enabled)
            {
                _logger.LogDebug("[RemoteLlm] External moderation disabled, skipping");
                return ModerationDecision.Unknown("external_moderation_disabled");
            }

            // Validate endpoint and domain allowlist
            if (!ValidateEndpoint(opts, out var endpointError))
            {
                _logger.LogWarning("[RemoteLlm] Endpoint validation failed: {Error}", endpointError);
                return ModerationDecision.Unknown("endpoint_configuration_invalid");
            }

            try
            {
                // Rate limiting
                await _rateLimiter.WaitAsync(cancellationToken);
                try
                {
                    // Create sanitized moderation request
                    var request = CreateModerationRequest(file);

                    // Make HTTP call to LLM service
                    var response = await CallLlmApiAsync(request, opts, cancellationToken);

                    // Parse response and create decision
                    var decision = ParseLlmResponse(response, file.Id);

                    _logger.LogInformation(
                        "[RemoteLlm] File analysis complete | Id={Id} | Verdict={Verdict} | ResponseTime={Time}ms",
                        file.Id,
                        decision.Verdict,
                        response.ProcessingTimeMs);

                    return decision;
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RemoteLlm] LLM API call failed for file | Id={Id}", file.Id);
                return ModerationDecision.Unknown("llm_api_error");
            }
        }

        private bool ValidateEndpoint(ExternalModerationOptions opts, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(opts.Endpoint))
            {
                error = "Endpoint is not configured";
                return false;
            }

            if (!Uri.TryCreate(opts.Endpoint, UriKind.Absolute, out var uri))
            {
                error = $"Endpoint is not a valid absolute URI: {opts.Endpoint}";
                return false;
            }

            if (uri.Scheme != "https")
            {
                error = $"Endpoint must use HTTPS: {opts.Endpoint}";
                return false;
            }

            // SSRF protection: check domain allowlist
            var host = uri.Host.ToLowerInvariant();
            if (!opts.AllowedDomains.Any(allowed =>
                allowed.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Domain '{host}' is not in allowed domains list";
                return false;
            }

            return true;
        }

        private LlmModerationRequest CreateModerationRequest(LocalFileMetadata file)
        {
            // Sanitize and minimize data sent to external service
            var sanitizedFilename = SanitizeFilename(file.Id);

            return new LlmModerationRequest
            {
                ContentType = LlmModeration.ContentType.FileContent,
                Content = $"Filename: {sanitizedFilename}, Size: {file.SizeBytes} bytes, MediaInfo: {TruncateContent(file.MediaInfo ?? "Unknown", 200)}",
                Metadata = new Dictionary<string, string>
                {
                    ["filename"] = sanitizedFilename,
                    ["size_bytes"] = file.SizeBytes.ToString(),
                    ["file_extension"] = GetFileExtension(sanitizedFilename),
                    ["media_info_preview"] = TruncateContent(file.MediaInfo ?? string.Empty, 100)
                },
                Context = "file_content_moderation",
                Source = "slskdn_external_moderation"
            };
        }

        private async Task<LlmApiResponse> CallLlmApiAsync(
            LlmModerationRequest request,
            ExternalModerationOptions opts,
            CancellationToken cancellationToken)
        {
            // Create OpenAI-compatible request
            var openAiRequest = new OpenAiModerationRequest
            {
                Model = "gpt-4", // Could be configurable
                Messages = new[]
                {
                    new OpenAiMessage
                    {
                        Role = "system",
                        Content = @"You are a content moderation AI. Analyze the provided file information for inappropriate content.

Respond with a JSON object containing:
- verdict: 'Allowed', 'Blocked', or 'Quarantined'
- confidence: number between 0.0 and 1.0
- reasoning: brief explanation (max 200 chars)
- categories: array of categories if inappropriate

Be conservative - when in doubt, choose 'Allowed' with low confidence."
                    },
                    new OpenAiMessage
                    {
                        Role = "user",
                        Content = $"Please moderate this file:\n\n{request.Content}\n\nMetadata: {string.Join(", ", request.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}"
                    }
                },
                Temperature = 0.1,
                MaxTokens = 300
            };

            var response = await _httpClient.PostAsJsonAsync(
                opts.Endpoint!.TrimEnd('/') + "/chat/completions",
                openAiRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<OpenAiModerationResponse>(
                cancellationToken: cancellationToken);

            if (apiResponse?.Choices == null || !apiResponse.Choices.Any())
            {
                throw new InvalidOperationException("Invalid response from LLM API");
            }

            var choice = apiResponse.Choices.First();
            var analysis = ParseApiResponse(choice.Message.Content);

            return new LlmApiResponse
            {
                Verdict = analysis.Verdict,
                Confidence = analysis.Confidence,
                Reasoning = analysis.Reasoning,
                Categories = analysis.Categories,
                ProcessingTimeMs = 0 // Could track this if needed
            };
        }

        private ModerationAnalysis ParseApiResponse(string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var verdictStr = root.GetProperty("verdict").GetString() ?? "Unknown";
                var confidence = root.GetProperty("confidence").GetDouble();
                var reasoning = root.GetProperty("reasoning").GetString() ?? "LLM analysis";

                var categories = LlmModeration.ContentCategory.None;
                if (root.TryGetProperty("categories", out var categoriesElement) && categoriesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var category in categoriesElement.EnumerateArray())
                    {
                        var categoryStr = category.GetString();
                        if (Enum.TryParse<LlmModeration.ContentCategory>(categoryStr, out var cat))
                        {
                            categories |= cat;
                        }
                    }
                }

                return new ModerationAnalysis
                {
                    Verdict = Enum.Parse<ModerationVerdict>(verdictStr),
                    Confidence = Math.Clamp(confidence, 0.0, 1.0),
                    Reasoning = reasoning.Length > 200 ? reasoning.Substring(0, 200) : reasoning,
                    Categories = categories
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RemoteLlm] Failed to parse API response, using safe defaults");
                return new ModerationAnalysis
                {
                    Verdict = ModerationVerdict.Unknown,
                    Confidence = 0.0,
                    Reasoning = "Failed to parse API response",
                    Categories = LlmModeration.ContentCategory.None
                };
            }
        }

        private ModerationDecision ParseLlmResponse(LlmApiResponse response, string fileId)
        {
            // Apply confidence threshold
            var opts = _options.CurrentValue;
            var confidenceThreshold = 0.7; // Could be configurable

            if (response.Confidence < confidenceThreshold)
            {
                return ModerationDecision.Unknown($"llm_confidence_too_low_{response.Confidence:F2}");
            }

            var reason = $"llm:{response.Reasoning}";
            var provider = "provider:remote_llm";

            switch (response.Verdict)
            {
                case ModerationVerdict.Blocked:
                    return ModerationDecision.Block(reason, provider);

                case ModerationVerdict.Quarantined:
                    return ModerationDecision.Quarantine(reason, provider);

                case ModerationVerdict.Allowed:
                    return ModerationDecision.Allow(reason);

                default:
                    return ModerationDecision.Unknown($"llm_{response.Verdict.ToString().ToLowerInvariant()}");
            }
        }

        private static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return "unknown_file";
            }

            // Remove path information
            var lastSeparator = filename.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSeparator >= 0)
            {
                filename = filename.Substring(lastSeparator + 1);
            }

            // Limit length
            if (filename.Length > 255)
            {
                filename = filename.Substring(0, 252) + "...";
            }

            return filename;
        }

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

        private static string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            {
                return content;
            }

            return content.Substring(0, maxLength - 3) + "...";
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _rateLimiter.Dispose();
            _disposed = true;
        }

        private sealed class ModerationAnalysis
        {
            public ModerationVerdict Verdict { get; set; }
            public double Confidence { get; set; }
            public string Reasoning { get; set; } = string.Empty;
            public LlmModeration.ContentCategory Categories { get; set; }
        }

        private sealed class LlmApiResponse
        {
            public ModerationVerdict Verdict { get; set; }
            public double Confidence { get; set; }
            public string Reasoning { get; set; } = string.Empty;
            public LlmModeration.ContentCategory Categories { get; set; }
            public int ProcessingTimeMs { get; set; }
        }

        private sealed class OpenAiModerationRequest
        {
            public string Model { get; set; } = string.Empty;
            public OpenAiMessage[] Messages { get; set; } = Array.Empty<OpenAiMessage>();
            public double Temperature { get; set; }
            public int MaxTokens { get; set; }
        }

        private sealed class OpenAiMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        private sealed class OpenAiModerationResponse
        {
            public OpenAiChoice[] Choices { get; set; } = Array.Empty<OpenAiChoice>();
        }

        private sealed class OpenAiChoice
        {
            public OpenAiResponseMessage Message { get; set; } = new();
        }

        private sealed class OpenAiResponseMessage
        {
            public string Content { get; set; } = string.Empty;
        }
    }
}
