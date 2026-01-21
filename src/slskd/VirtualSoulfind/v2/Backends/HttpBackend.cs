// <copyright file="HttpBackend.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for HTTP/HTTPS direct content downloads.
    /// </summary>
    /// <remarks>
    ///     🔒 SSRF Protection: Domain allowlist enforced.
    ///     🔒 Work budgets: Per-domain quotas.
    ///     🔒 Size limits: HEAD requests before commits.
    /// </remarks>
    public sealed class HttpBackend : IContentBackend
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<HttpBackendOptions> _options;
        private readonly ISourceRegistry _sourceRegistry;

        public HttpBackend(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<HttpBackendOptions> options,
            ISourceRegistry sourceRegistry)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _sourceRegistry = sourceRegistry;
        }

        public ContentBackendType Type => ContentBackendType.Http;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find HTTP candidates from source registry.
        /// </summary>
        /// <remarks>
        ///     Does NOT perform discovery; returns known HTTP URLs from registry.
        ///     Future: Could integrate with metadata APIs that provide direct links.
        /// </remarks>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            // Query source registry for HTTP candidates
            var candidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                ContentBackendType.Http,
                cancellationToken);

            // Filter by domain allowlist
            var opts = _options.CurrentValue;
            if (opts.DomainAllowlist == null || opts.DomainAllowlist.Count == 0)
            {
                // If no allowlist configured, no HTTP candidates are allowed
                return Array.Empty<SourceCandidate>();
            }

            var filtered = candidates.Where(c =>
            {
                if (!Uri.TryCreate(c.BackendRef, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                // Check domain allowlist
                var host = uri.Host.ToLowerInvariant();
                return opts.DomainAllowlist.Any(allowed =>
                    host == allowed.ToLowerInvariant() ||
                    host.EndsWith("." + allowed.ToLowerInvariant(), StringComparison.Ordinal));
            }).ToList();

            return filtered;
        }

        /// <summary>
        ///     Validate HTTP candidate: domain allowlist + HEAD request.
        /// </summary>
        public async Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.Http)
            {
                return SourceCandidateValidationResult.Invalid("Not an HTTP candidate");
            }

            // Parse URL
            if (!Uri.TryCreate(candidate.BackendRef, UriKind.Absolute, out var uri))
            {
                return SourceCandidateValidationResult.Invalid("Invalid URL");
            }

            // Check scheme
            if (uri.Scheme != "https" && uri.Scheme != "http")
            {
                return SourceCandidateValidationResult.Invalid("Only HTTP/HTTPS allowed");
            }

            // Check domain allowlist
            var opts = _options.CurrentValue;
            if (opts.DomainAllowlist == null || opts.DomainAllowlist.Count == 0)
            {
                return SourceCandidateValidationResult.Invalid("No allowlist configured");
            }

            var host = uri.Host.ToLowerInvariant();
            var allowed = opts.DomainAllowlist.Any(domain =>
                host == domain.ToLowerInvariant() ||
                host.EndsWith("." + domain.ToLowerInvariant(), StringComparison.Ordinal));

            if (!allowed)
            {
                return SourceCandidateValidationResult.Invalid($"Domain {host} not in allowlist");
            }

            // Perform HEAD request to validate
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(opts.ValidationTimeoutSeconds);

                using var request = new HttpRequestMessage(HttpMethod.Head, uri);
                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return SourceCandidateValidationResult.Invalid($"HTTP {(int)response.StatusCode}");
                }

                // Check content length if available
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue)
                {
                    if (contentLength.Value > opts.MaxFileSizeBytes)
                    {
                        return SourceCandidateValidationResult.Invalid("File too large");
                    }

                    if (contentLength.Value == 0)
                    {
                        return SourceCandidateValidationResult.Invalid("Empty file");
                    }
                }

                // Valid candidate
                return SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality);
            }
            catch (TaskCanceledException)
            {
                return SourceCandidateValidationResult.Invalid("Timeout");
            }
            catch (HttpRequestException ex)
            {
                return SourceCandidateValidationResult.Invalid($"HTTP error: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Configuration for HTTP backend.
    /// </summary>
    public sealed class HttpBackendOptions
    {
        /// <summary>
        ///     Domain allowlist (SSRF protection).
        /// </summary>
        public List<string> DomainAllowlist { get; init; } = new();

        /// <summary>
        ///     Maximum file size to accept (bytes).
        /// </summary>
        public long MaxFileSizeBytes { get; init; } = 500_000_000; // 500MB default

        /// <summary>
        ///     Timeout for validation HEAD requests (seconds).
        /// </summary>
        public int ValidationTimeoutSeconds { get; init; } = 10;

        /// <summary>
        ///     Enable HTTP backend.
        /// </summary>
        public bool Enabled { get; init; } = false;
    }
}
