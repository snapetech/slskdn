// <copyright file="WebDavBackend.cs" company="slskd Team">
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
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for WebDAV (PROPFIND, GET) content sources.
    /// </summary>
    /// <remarks>
    ///     Uses registry for candidates; BackendRef = full WebDAV URL. Domain allowlist (SSRF);
    ///     optional Basic or Bearer auth. Validation via HTTP HEAD.
    /// </remarks>
    public sealed class WebDavBackend : IContentFetchBackend
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<WebDavBackendOptions> _options;
        private readonly ISourceRegistry _sourceRegistry;

        public WebDavBackend(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<WebDavBackendOptions> options,
            ISourceRegistry sourceRegistry)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sourceRegistry = sourceRegistry ?? throw new ArgumentNullException(nameof(sourceRegistry));
        }

        public ContentBackendType Type => ContentBackendType.WebDav;

        public ContentDomain? SupportedDomain => null;

        /// <summary>
        ///     Find WebDAV candidates from source registry; filter by domain allowlist.
        /// </summary>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Array.Empty<SourceCandidate>();
            }

            var candidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                ContentBackendType.WebDav,
                cancellationToken);

            if (opts.DomainAllowlist == null || opts.DomainAllowlist.Count == 0)
            {
                return Array.Empty<SourceCandidate>();
            }

            var filtered = candidates.Where(c =>
            {
                if (!Uri.TryCreate(c.BackendRef, UriKind.Absolute, out var uri))
                    return false;
                if (uri.Scheme != "https" && uri.Scheme != "http")
                    return false;
                var host = uri.Host.ToLowerInvariant();
                return opts.DomainAllowlist.Any(allowed =>
                    host == allowed.ToLowerInvariant() ||
                    host.EndsWith("." + allowed.ToLowerInvariant(), StringComparison.Ordinal));
            }).ToList();

            return filtered;
        }

        /// <summary>
        ///     Validate WebDAV candidate: allowlist, HEAD request, size limits. Applies Basic/Bearer if configured.
        /// </summary>
        public async Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.WebDav)
            {
                return SourceCandidateValidationResult.Invalid("Not a WebDAV candidate");
            }

            if (!Uri.TryCreate(candidate.BackendRef, UriKind.Absolute, out var uri))
            {
                return SourceCandidateValidationResult.Invalid("Invalid URL");
            }

            if (uri.Scheme != "https" && uri.Scheme != "http")
            {
                return SourceCandidateValidationResult.Invalid("Only HTTP/HTTPS allowed");
            }

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return SourceCandidateValidationResult.Invalid("WebDAV backend disabled");
            }

            if (opts.DomainAllowlist == null || opts.DomainAllowlist.Count == 0)
            {
                return SourceCandidateValidationResult.Invalid("No allowlist configured");
            }

            var host = uri.Host.ToLowerInvariant();
            if (!opts.DomainAllowlist.Any(d => host == d.ToLowerInvariant() || host.EndsWith("." + d.ToLowerInvariant(), StringComparison.Ordinal)))
            {
                return SourceCandidateValidationResult.Invalid($"Domain {host} not in allowlist");
            }

            try
            {
                using var http = _httpClientFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(opts.ValidationTimeoutSeconds);

                using var request = new HttpRequestMessage(HttpMethod.Head, uri);
                ApplyAuth(request, opts);

                using var response = await http.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return SourceCandidateValidationResult.Invalid($"HTTP {(int)response.StatusCode}");
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue)
                {
                    if (contentLength.Value > opts.MaxFileSizeBytes)
                        return SourceCandidateValidationResult.Invalid("File too large");
                    if (contentLength.Value == 0)
                        return SourceCandidateValidationResult.Invalid("Empty file");
                }

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

        private static void ApplyAuth(HttpRequestMessage request, WebDavBackendOptions opts)
        {
            if (!string.IsNullOrEmpty(opts.BearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.BearerToken);
                return;
            }

            if (!string.IsNullOrEmpty(opts.Username) && !string.IsNullOrEmpty(opts.Password))
            {
                var value = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(opts.Username + ":" + opts.Password));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", value);
            }
        }

        /// <inheritdoc />
        public async Task FetchToStreamAsync(
            SourceCandidate candidate,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.WebDav)
                throw new ArgumentException("Not a WebDAV candidate", nameof(candidate));

            if (!Uri.TryCreate(candidate.BackendRef, UriKind.Absolute, out var uri))
                throw new ArgumentException("Invalid URL", nameof(candidate));

            if (uri.Scheme != "https" && uri.Scheme != "http")
                throw new ArgumentException("Only HTTP/HTTPS allowed", nameof(candidate));

            var opts = _options.CurrentValue;
            if (!opts.Enabled || opts.DomainAllowlist == null || opts.DomainAllowlist.Count == 0)
                throw new InvalidOperationException("WebDAV disabled or no allowlist");

            var host = uri.Host.ToLowerInvariant();
            if (!opts.DomainAllowlist.Any(d => host == d.ToLowerInvariant() || host.EndsWith("." + d.ToLowerInvariant(), StringComparison.Ordinal)))
                throw new InvalidOperationException($"Domain {host} not in allowlist");

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(Math.Max(opts.ValidationTimeoutSeconds * 3, 60));

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyAuth(request, opts);

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > opts.MaxFileSizeBytes)
                throw new InvalidOperationException("File too large");
            if (contentLength.HasValue && contentLength.Value == 0)
                throw new InvalidOperationException("Empty file");

            await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
            await CopyWithLimitAsync(src, destination, opts.MaxFileSizeBytes, cancellationToken);
        }

        private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
        {
            var buf = new byte[81920];
            long total = 0;
            int r;
            while ((r = await source.ReadAsync(buf, ct)) > 0)
            {
                total += r;
                if (total > maxBytes)
                    throw new InvalidOperationException("File too large");
                await destination.WriteAsync(buf.AsMemory(0, r), ct);
            }
        }
    }

    /// <summary>
    ///     Configuration for WebDAV backend.
    /// </summary>
    public sealed class WebDavBackendOptions
    {
        /// <summary>Enable WebDAV backend.</summary>
        public bool Enabled { get; init; } = false;

        /// <summary>Domain allowlist (SSRF protection).</summary>
        public List<string> DomainAllowlist { get; init; } = new();

        /// <summary>Maximum file size (bytes).</summary>
        public long MaxFileSizeBytes { get; init; } = 500_000_000;

        /// <summary>Validation HEAD timeout (seconds).</summary>
        public int ValidationTimeoutSeconds { get; init; } = 15;

        /// <summary>Basic auth username (optional).</summary>
        public string? Username { get; init; }

        /// <summary>Basic auth password (optional).</summary>
        public string? Password { get; init; }

        /// <summary>Bearer token (optional; overrides Basic if set).</summary>
        public string? BearerToken { get; init; }
    }
}
