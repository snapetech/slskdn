// <copyright file="HttpLlmModerationProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     HTTP-based LLM moderation provider for external AI services.
    /// </summary>
    /// <remarks>
    ///     T-MCP-LM01: HTTP client for LLM moderation APIs.
    ///     Supports OpenAI-compatible APIs and basic rate limiting.
    /// </remarks>
    public sealed class HttpLlmModerationProvider : ILlmModerationProvider, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<LlmModerationOptions> _options;
        private readonly ILogger<HttpLlmModerationProvider> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly Stopwatch _requestTimer;
        private readonly List<double> _responseTimes;
        private DateTimeOffset _lastSuccessfulRequest;
        private DateTimeOffset _lastError;
        private string? _lastErrorMessage;
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="HttpLlmModerationProvider"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for API calls.</param>
        /// <param name="options">The LLM moderation options.</param>
        /// <param name="logger">The logger.</param>
        public HttpLlmModerationProvider(
            HttpClient httpClient,
            IOptionsMonitor<LlmModerationOptions> options,
            ILogger<HttpLlmModerationProvider> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _rateLimiter = new SemaphoreSlim(1, 1);
            _requestTimer = new Stopwatch();
            _responseTimes = new List<double>();
            _lastSuccessfulRequest = DateTimeOffset.MinValue;
            _lastError = DateTimeOffset.MinValue;

            // Configure HTTP client
            _httpClient.Timeout = _options.CurrentValue.Timeout;
            if (!string.IsNullOrEmpty(_options.CurrentValue.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.CurrentValue.ApiKey);
            }
        }

        /// <inheritdoc/>
        public bool IsAvailable => !string.IsNullOrEmpty(_options.CurrentValue.Endpoint) &&
                                  !string.IsNullOrEmpty(_options.CurrentValue.ApiKey);

        /// <inheritdoc/>
        public string ProviderName => "HttpLlmModerationProvider";

        /// <inheritdoc/>
        public async Task<LlmModerationResponse> ModerateAsync(
            LlmModerationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var startTime = DateTimeOffset.UtcNow;
            _requestTimer.Restart();

            try
            {
                // Rate limiting
                await _rateLimiter.WaitAsync(cancellationToken);
                try
                {
                    // Check if we should make the request (basic rate limiting)
                    // In a production system, this would use a proper rate limiter

                    var response = await MakeModerationRequestAsync(request, cancellationToken);
                    var processingTime = _requestTimer.Elapsed;

                    // Track metrics
                    lock (_responseTimes)
                    {
                        _responseTimes.Add(processingTime.TotalMilliseconds);
                        if (_responseTimes.Count > 100) // Keep last 100 measurements
                        {
                            _responseTimes.RemoveAt(0);
                        }
                    }

                    _lastSuccessfulRequest = DateTimeOffset.UtcNow;

                    _logger.LogInformation(
                        "[HttpLlm] Moderation completed | RequestId={RequestId} | Verdict={Verdict} | Confidence={Confidence:F2} | Time={TimeMs}ms",
                        request.RequestId,
                        response.Verdict,
                        response.Confidence,
                        processingTime.TotalMilliseconds);

                    response.ProcessingTime = processingTime;
                    return response;
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                var processingTime = _requestTimer.Elapsed;
                _lastError = DateTimeOffset.UtcNow;
                _lastErrorMessage = ex.Message;

                _logger.LogError(ex,
                    "[HttpLlm] Moderation failed | RequestId={RequestId} | Time={TimeMs}ms | Error={Message}",
                    request.RequestId,
                    processingTime.TotalMilliseconds,
                    ex.Message);

                return new LlmModerationResponse
                {
                    RequestId = request.RequestId,
                    Verdict = ModerationVerdict.Unknown,
                    Severity = LlmModeration.SeverityLevel.Safe,
                    Categories = LlmModeration.ContentCategory.None,
                    Confidence = 0.0,
                    Reasoning = $"LLM service error: {ex.Message}",
                    Error = ex.Message,
                    ProcessingTime = processingTime,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
        }

        /// <inheritdoc/>
        public bool CanHandleContentType(LlmModeration.ContentType contentType)
        {
            // This provider can handle text-based content
            return contentType == LlmModeration.ContentType.Text ||
                   contentType == LlmModeration.ContentType.Metadata ||
                   contentType == LlmModeration.ContentType.FileContent;
        }

        /// <inheritdoc/>
        public async Task<LlmProviderHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // No async work needed for basic status

            double averageResponseTime = 0;
            lock (_responseTimes)
            {
                if (_responseTimes.Any())
                {
                    averageResponseTime = _responseTimes.Average();
                }
            }

            return new LlmProviderHealthStatus
            {
                IsHealthy = IsAvailable && _lastSuccessfulRequest > _lastError,
                ProviderName = ProviderName,
                LastSuccessfulRequest = _lastSuccessfulRequest > DateTimeOffset.MinValue ? _lastSuccessfulRequest : null,
                LastError = _lastError > DateTimeOffset.MinValue ? _lastError : null,
                LastErrorMessage = _lastErrorMessage,
                AverageResponseTime = averageResponseTime > 0 ? TimeSpan.FromMilliseconds(averageResponseTime) : null,
                CurrentRequestRate = CalculateCurrentRequestRate(),
                Details = new Dictionary<string, string>
                {
                    ["endpoint"] = _options.CurrentValue.Endpoint ?? "not_configured",
                    ["model"] = _options.CurrentValue.Model,
                    ["timeout_seconds"] = _options.CurrentValue.Timeout.TotalSeconds.ToString(),
                    ["min_confidence"] = _options.CurrentValue.MinConfidenceThreshold.ToString("F2")
                }
            };
        }

        private async Task<LlmModerationResponse> MakeModerationRequestAsync(
            LlmModerationRequest request,
            CancellationToken cancellationToken)
        {
            // Truncate content if too long
            var content = request.Content;
            if (content.Length > _options.CurrentValue.MaxContentLength)
            {
                content = content.Substring(0, _options.CurrentValue.MaxContentLength) + "...";
                _logger.LogDebug("[HttpLlm] Content truncated to {MaxLength} characters", _options.CurrentValue.MaxContentLength);
            }

            // Create OpenAI-compatible request
            var openAiRequest = new OpenAiModerationRequest
            {
                Model = _options.CurrentValue.Model,
                Messages = new[]
                {
                    new OpenAiMessage
                    {
                        Role = "system",
                        Content = GetSystemPrompt(request.ContentType)
                    },
                    new OpenAiMessage
                    {
                        Role = "user",
                        Content = $"Please analyze this content for moderation:\n\n{content}\n\nContext: {request.Context}\n\nMetadata: {string.Join(", ", request.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}"
                    }
                },
                Temperature = 0.1, // Low temperature for consistent results
                MaxTokens = 500
            };

            var response = await _httpClient.PostAsJsonAsync(
                _options.CurrentValue.Endpoint.TrimEnd('/') + "/chat/completions",
                openAiRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var openAiResponse = await response.Content.ReadFromJsonAsync<OpenAiModerationResponse>(
                cancellationToken: cancellationToken);

            if (openAiResponse?.Choices == null || !openAiResponse.Choices.Any())
            {
                throw new InvalidOperationException("Invalid response from LLM service");
            }

            var choice = openAiResponse.Choices.First();
            var analysis = ParseModerationAnalysis(choice.Message.Content);

            return new LlmModerationResponse
            {
                RequestId = request.RequestId,
                Verdict = analysis.Verdict,
                Severity = analysis.Severity,
                Categories = analysis.Categories,
                Confidence = analysis.Confidence,
                Reasoning = analysis.Reasoning,
                Details = analysis.Details,
                Timestamp = DateTimeOffset.UtcNow,
                FromCache = false
            };
        }

        private string GetSystemPrompt(LlmModeration.ContentType contentType)
        {
            return contentType switch
            {
                LlmModeration.ContentType.Text => @"
You are a content moderation AI. Analyze the provided text for inappropriate content.

Respond with a JSON object containing:
- verdict: 'Allowed', 'Blocked', or 'Quarantined'
- severity: 'Safe', 'Questionable', 'Inappropriate', or 'Harmful'
- categories: array of categories like ['HateSpeech', 'SexualContent', 'Violence']
- confidence: number between 0.0 and 1.0
- reasoning: brief explanation
- details: additional analysis details

Be conservative - when in doubt, choose lower severity levels.",

                LlmModeration.ContentType.FileContent => @"
You are a content moderation AI. Analyze file information for inappropriate content.

Consider filename, size, and metadata. Flag suspicious patterns.

Respond with JSON as described above.",

                LlmModeration.ContentType.Metadata => @"
You are a content moderation AI. Analyze metadata for inappropriate content.

Consider tags, descriptions, and other metadata fields.

Respond with JSON as described above.",

                _ => "Analyze the content for moderation concerns and respond with JSON."
            };
        }

        private ModerationAnalysis ParseModerationAnalysis(string llmResponse)
        {
            try
            {
                // Try to parse JSON response from LLM
                using var doc = JsonDocument.Parse(llmResponse);
                var root = doc.RootElement;

                var verdictStr = root.GetProperty("verdict").GetString() ?? "Unknown";
                var severityStr = root.GetProperty("severity").GetString() ?? "Safe";
                var confidence = root.GetProperty("confidence").GetDouble();
                var reasoning = root.GetProperty("reasoning").GetString() ?? "LLM analysis";
                var details = new Dictionary<string, string>();

                // Parse categories
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

                // Parse additional details
                if (root.TryGetProperty("details", out var detailsElement) && detailsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in detailsElement.EnumerateObject())
                    {
                        details[property.Name] = property.Value.ToString();
                    }
                }

                return new ModerationAnalysis
                {
                    Verdict = Enum.Parse<ModerationVerdict>(verdictStr),
                    Severity = Enum.Parse<LlmModeration.SeverityLevel>(severityStr),
                    Categories = categories,
                    Confidence = Math.Clamp(confidence, 0.0, 1.0),
                    Reasoning = reasoning,
                    Details = details
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HttpLlm] Failed to parse LLM response, using safe defaults");

                // Return safe defaults if parsing fails
                return new ModerationAnalysis
                {
                    Verdict = ModerationVerdict.Unknown,
                    Severity = LlmModeration.SeverityLevel.Safe,
                    Categories = LlmModeration.ContentCategory.None,
                    Confidence = 0.0,
                    Reasoning = "Failed to parse LLM response",
                    Details = new Dictionary<string, string> { ["parse_error"] = ex.Message }
                };
            }
        }

        private double CalculateCurrentRequestRate()
        {
            // Simple calculation - in production, this would use a proper rate tracking system
            return 0.0; // Placeholder
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
            public LlmModeration.SeverityLevel Severity { get; set; }
            public LlmModeration.ContentCategory Categories { get; set; }
            public double Confidence { get; set; }
            public string Reasoning { get; set; } = string.Empty;
            public Dictionary<string, string> Details { get; set; } = new();
        }

        private sealed class OpenAiModerationRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public OpenAiMessage[] Messages { get; set; } = Array.Empty<OpenAiMessage>();

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }
        }

        private sealed class OpenAiMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private sealed class OpenAiModerationResponse
        {
            [JsonPropertyName("choices")]
            public OpenAiChoice[] Choices { get; set; } = Array.Empty<OpenAiChoice>();
        }

        private sealed class OpenAiChoice
        {
            [JsonPropertyName("message")]
            public OpenAiResponseMessage Message { get; set; } = new();
        }

        private sealed class OpenAiResponseMessage
        {
            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }
    }
}

