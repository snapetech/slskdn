// <copyright file="LocalExternalModerationClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Local external moderation client for on-premise LLM services.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM03: Local LLM client with IPC/HTTP support for on-premise deployment.
    ///     Calls local LLM endpoints with enhanced security for internal networks.
    /// </remarks>
    public sealed class LocalExternalModerationClient : IExternalModerationClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<ExternalModerationOptions> _options;
        private readonly ILogger<LocalExternalModerationClient> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LocalExternalModerationClient"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for local API calls.</param>
        /// <param name="options">The external moderation options.</param>
        /// <param name="logger">The logger.</param>
        public LocalExternalModerationClient(
            HttpClient httpClient,
            IOptionsMonitor<ExternalModerationOptions> options,
            ILogger<LocalExternalModerationClient> logger)
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
                _logger.LogDebug("[LocalLlm] External moderation disabled, skipping");
                return ModerationDecision.Unknown("external_moderation_disabled");
            }

            // Validate local endpoint (more permissive than remote)
            if (!ValidateLocalEndpoint(opts, out var endpointError))
            {
                _logger.LogWarning("[LocalLlm] Endpoint validation failed: {Error}", endpointError);
                return ModerationDecision.Unknown("endpoint_configuration_invalid");
            }

            try
            {
                // Rate limiting for local calls (still important to prevent abuse)
                await _rateLimiter.WaitAsync(cancellationToken);
                try
                {
                    // Create moderation request (can include more data for local services)
                    var request = CreateModerationRequest(file);

                    // Make HTTP call to local LLM service
                    var response = await CallLocalLlmApiAsync(request, opts, cancellationToken);

                    // Parse response and create decision
                    var decision = ParseLlmResponse(response, file.Id);

                    _logger.LogInformation(
                        "[LocalLlm] Local file analysis complete | Id={Id} | Verdict={Verdict}",
                        file.Id,
                        decision.Verdict);

                    return decision;
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LocalLlm] Local LLM call failed for file | Id={Id}", file.Id);
                return ModerationDecision.Unknown("local_llm_error");
            }
        }

        private bool ValidateLocalEndpoint(ExternalModerationOptions opts, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(opts.Endpoint))
            {
                error = "Local endpoint is not configured";
                return false;
            }

            if (!Uri.TryCreate(opts.Endpoint, UriKind.Absolute, out var uri))
            {
                error = $"Endpoint is not a valid absolute URI: {opts.Endpoint}";
                return false;
            }

            // For local endpoints, allow HTTP (localhost/127.0.0.1) and HTTPS
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                error = $"Local endpoint must use HTTP or HTTPS: {opts.Endpoint}";
                return false;
            }

            // Allow localhost, 127.0.0.1, and local network ranges for local services
            var host = uri.Host.ToLowerInvariant();
            var allowedLocalHosts = new[] { "localhost", "127.0.0.1", "::1" };

            if (!allowedLocalHosts.Contains(host) &&
                !IsLocalNetworkIp(host) &&
                !opts.AllowedDomains.Any(allowed =>
                    allowed.Equals(host, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Local endpoint host '{host}' is not allowed. Use localhost, 127.0.0.1, or configure in AllowedDomains";
                return false;
            }

            return true;
        }

        private static bool IsLocalNetworkIp(string host)
        {
            // Check if it's a local network IP (192.168.x.x, 10.x.x.x, 172.16-31.x.x)
            if (IPAddress.TryParse(host, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                if (bytes.Length == 4) // IPv4
                {
                    return (bytes[0] == 192 && bytes[1] == 168) || // 192.168.x.x
                           (bytes[0] == 10) ||                      // 10.x.x.x
                           (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31); // 172.16-31.x.x
                }
            }

            return false;
        }

        private LlmModerationRequest CreateModerationRequest(LocalFileMetadata file)
        {
            // For local services, we can include more detailed information
            // since it's running on the same infrastructure
            return new LlmModerationRequest
            {
                ContentType = LlmModeration.ContentType.FileContent,
                Content = BuildDetailedContentDescription(file),
                Metadata = BuildDetailedMetadata(file),
                Context = "local_file_content_moderation",
                Source = "slskdn_local_moderation"
            };
        }

        private string BuildDetailedContentDescription(LocalFileMetadata file)
        {
            var parts = new List<string>
            {
                $"Filename: {file.Id}",
                $"Size: {file.SizeBytes} bytes"
            };

            if (!string.IsNullOrEmpty(file.MediaInfo))
            {
                parts.Add($"Media Info: {file.MediaInfo}");
            }

            if (!string.IsNullOrEmpty(file.PrimaryHash))
            {
                // Include partial hash for duplicate detection (first 16 chars)
                var partialHash = file.PrimaryHash.Length > 16
                    ? file.PrimaryHash.Substring(0, 16) + "..."
                    : file.PrimaryHash;
                parts.Add($"Content Hash: {partialHash}");
            }

            return string.Join(" | ", parts);
        }

        private Dictionary<string, string> BuildDetailedMetadata(LocalFileMetadata file)
        {
            var metadata = new Dictionary<string, string>
            {
                ["filename"] = file.Id,
                ["size_bytes"] = file.SizeBytes.ToString(),
                ["media_info"] = file.MediaInfo ?? string.Empty,
                ["has_hash"] = (!string.IsNullOrEmpty(file.PrimaryHash)).ToString()
            };

            // Add file extension if detectable
            var extension = GetFileExtension(file.Id);
            if (!string.IsNullOrEmpty(extension))
            {
                metadata["file_extension"] = extension;
            }

            return metadata;
        }

        private async Task<LlmApiResponse> CallLocalLlmApiAsync(
            LlmModerationRequest request,
            ExternalModerationOptions opts,
            CancellationToken cancellationToken)
        {
            // For local services, we can use a simpler protocol
            // This could be customized based on the local LLM service
            var localRequest = new LocalModerationRequest
            {
                Content = request.Content,
                Metadata = request.Metadata,
                Context = request.Context,
                MaxResponseLength = 500
            };

            var response = await _httpClient.PostAsJsonAsync(
                opts.Endpoint!.TrimEnd('/') + "/moderate",
                localRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<LocalModerationResponse>(
                cancellationToken: cancellationToken);

            if (apiResponse == null)
            {
                throw new InvalidOperationException("Invalid response from local LLM service");
            }

            return new LlmApiResponse
            {
                Verdict = Enum.Parse<ModerationVerdict>(apiResponse.Verdict ?? "Unknown"),
                Confidence = apiResponse.Confidence,
                Reasoning = apiResponse.Reasoning ?? "Local LLM analysis",
                Categories = ParseCategories(apiResponse.Categories),
                ProcessingTimeMs = apiResponse.ProcessingTimeMs
            };
        }

        private LlmModeration.ContentCategory ParseCategories(string[]? categories)
        {
            if (categories == null || categories.Length == 0)
            {
                return LlmModeration.ContentCategory.None;
            }

            var result = LlmModeration.ContentCategory.None;
            foreach (var category in categories)
            {
                if (Enum.TryParse<LlmModeration.ContentCategory>(category, out var cat))
                {
                    result |= cat;
                }
            }

            return result;
        }

        private ModerationDecision ParseLlmResponse(LlmApiResponse response, string fileId)
        {
            // For local services, be more permissive with confidence thresholds
            var confidenceThreshold = 0.5; // Lower threshold for local services

            if (response.Confidence < confidenceThreshold)
            {
                return ModerationDecision.Unknown($"local_llm_confidence_too_low_{response.Confidence:F2}");
            }

            var reason = $"local_llm:{response.Reasoning}";
            var provider = "provider:local_llm";

            switch (response.Verdict)
            {
                case ModerationVerdict.Blocked:
                    return ModerationDecision.Block(reason, provider);

                case ModerationVerdict.Quarantined:
                    return ModerationDecision.Quarantine(reason, provider);

                case ModerationVerdict.Allowed:
                    return ModerationDecision.Allow(reason, provider);

                default:
                    return ModerationDecision.Unknown($"local_llm_{response.Verdict.ToString().ToLowerInvariant()}", provider);
            }
        }

        private static string GetFileExtension(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return string.Empty;
            }

            var lastDot = filename.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < filename.Length - 1)
            {
                return filename.Substring(lastDot + 1).ToLowerInvariant();
            }

            return string.Empty;
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

        private sealed class LlmApiResponse
        {
            public ModerationVerdict Verdict { get; set; }
            public double Confidence { get; set; }
            public string Reasoning { get; set; } = string.Empty;
            public LlmModeration.ContentCategory Categories { get; set; }
            public int ProcessingTimeMs { get; set; }
        }

        private sealed class LocalModerationRequest
        {
            public string Content { get; set; } = string.Empty;
            public Dictionary<string, string> Metadata { get; set; } = new();
            public string Context { get; set; } = string.Empty;
            public int MaxResponseLength { get; set; }
        }

        private sealed class LocalModerationResponse
        {
            public string? Verdict { get; set; }
            public double Confidence { get; set; }
            public string? Reasoning { get; set; }
            public string[]? Categories { get; set; }
            public int ProcessingTimeMs { get; set; }
        }
    }
}
