// <copyright file="SoulseekBackend.cs" company="slskd Team">
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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Soulseek;
    using slskd.Common.Security;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Matching;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Content backend for searching the Soulseek network.
    /// </summary>
    /// <remarks>
    ///     This is THE PRIMARY backend for music content in VirtualSoulfind v2.
    ///
    ///     Key features:
    ///     - Full Soulseek network search integration
    ///     - Rate limiting via ISoulseekSafetyLimiter (H-08)
    ///     - Quality-based candidate scoring
    ///     - Music domain restriction
    ///     - Configurable search depth and timeouts
    ///
    ///     Security:
    ///     - Enforces MaxSearchesPerMinute from H-08
    ///     - Cannot be used for non-Music domains
    ///     - All searches are rate-limited and tracked
    /// </remarks>
    public sealed class SoulseekBackend : IContentBackend
    {
        private readonly ISoulseekClient _soulseekClient;
        private readonly ISoulseekSafetyLimiter _safetyLimiter;
        private readonly ICatalogueStore _catalogueStore;
        private readonly ILogger<SoulseekBackend> _logger;
        private readonly IOptionsMonitor<SoulseekBackendOptions> _options;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SoulseekBackend"/> class.
        /// </summary>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="safetyLimiter">The safety limiter (H-08).</param>
        /// <param name="catalogueStore">The catalogue store.</param>
        /// <param name="options">Backend options.</param>
        /// <param name="logger">Logger instance.</param>
        public SoulseekBackend(
            ISoulseekClient soulseekClient,
            ISoulseekSafetyLimiter safetyLimiter,
            ICatalogueStore catalogueStore,
            IOptionsMonitor<SoulseekBackendOptions> options,
            ILogger<SoulseekBackend> logger)
        {
            _soulseekClient = soulseekClient ?? throw new ArgumentNullException(nameof(soulseekClient));
            _safetyLimiter = safetyLimiter ?? throw new ArgumentNullException(nameof(safetyLimiter));
            _catalogueStore = catalogueStore ?? throw new ArgumentNullException(nameof(catalogueStore));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public ContentBackendType Type => ContentBackendType.Soulseek;

        /// <inheritdoc/>
        public ContentDomain? SupportedDomain => ContentDomain.Music;

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            var opts = _options.CurrentValue;

            if (!opts.Enabled)
            {
                _logger.LogDebug("Soulseek backend is disabled");
                return Array.Empty<SourceCandidate>();
            }

            // Safety limiter check (H-08) - THIS IS CRITICAL!
            if (!_safetyLimiter.TryConsumeSearch("virtualsoulfind-v2"))
            {
                _logger.LogWarning("Soulseek search rate limit exceeded (H-08), skipping search for {ItemId}", itemId);
                return Array.Empty<SourceCandidate>();
            }

            var searchQuery = await BuildSearchQueryAsync(itemId, cancellationToken);

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                _logger.LogDebug("Could not build metadata-backed Soulseek query for {ItemId}", itemId);
                return Array.Empty<SourceCandidate>();
            }

            try
            {
                _logger.LogDebug("Searching Soulseek for: {Query}", searchQuery);

                // Perform the search with configured timeout and response limits
                var searchOptions = new SearchOptions(
                    searchTimeout: opts.SearchTimeoutSeconds * 1000,
                    responseLimit: opts.MaxResponsesPerSearch,
                    filterResponses: true,
                    minimumResponseFileCount: 1,
                    minimumPeerUploadSpeed: opts.MinimumUploadSpeed);

                var results = await _soulseekClient.SearchAsync(
                    SearchQuery.FromText(searchQuery),
                    options: searchOptions,
                    cancellationToken: cancellationToken);

                if (results.Responses == null || !results.Responses.Any())
                {
                    _logger.LogDebug("No Soulseek results for: {Query}", searchQuery);
                    return Array.Empty<SourceCandidate>();
                }

                _logger.LogInformation(
                    "Found {Count} Soulseek responses with {FileCount} total files for {Query}",
                    results.Responses.Count,
                    results.Responses.Sum(r => r.FileCount),
                    searchQuery);

                // Convert responses to SourceCandidates
                var candidates = new List<SourceCandidate>();

                foreach (var response in results.Responses.Take(opts.MaxCandidatesPerItem))
                {
                    foreach (var file in response.Files.Take(opts.MaxFilesPerResponse))
                    {
                        var quality = QualityScorer.ScoreMusicQuality(
                            file.Extension ?? string.Empty,
                            file.Size,
                            file.BitRate);

                        // Trust score based on upload speed and queue length
                        var trustScore = CalculateTrustScore(response, opts);

                        var candidate = new SourceCandidate
                        {
                            Id = Guid.NewGuid().ToString(),
                            ItemId = itemId,
                            Backend = ContentBackendType.Soulseek,

                            // BackendRef format: "username|filename"
                            BackendRef = $"{response.Username}|{file.Filename}",
                            TrustScore = trustScore / 100.0f, // Normalize to 0-1
                            ExpectedQuality = quality / 100.0f, // Normalize to 0-1
                            LastSeenAt = DateTimeOffset.UtcNow,
                        };

                        candidates.Add(candidate);
                    }
                }

                // Order by quality then trust
                var ordered = candidates
                    .OrderByDescending(c => c.ExpectedQuality)
                    .ThenByDescending(c => c.TrustScore)
                    .Take(opts.MaxCandidatesPerItem)
                    .ToList();

                _logger.LogDebug("Returning {Count} Soulseek candidates for {ItemId}", ordered.Count, itemId);

                return ordered;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Soulseek search cancelled for {ItemId}", itemId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Soulseek for {ItemId}", itemId);
                return Array.Empty<SourceCandidate>();
            }
        }

        /// <inheritdoc/>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            var opts = _options.CurrentValue;

            // Check backend type
            if (candidate.Backend != ContentBackendType.Soulseek)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not a Soulseek candidate"));
            }

            // Check enabled status
            if (!opts.Enabled)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Soulseek backend disabled"));
            }

            // Validate BackendRef format: "username|filename"
            if (string.IsNullOrWhiteSpace(candidate.BackendRef) || !candidate.BackendRef.Contains('|'))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Invalid BackendRef format"));
            }

            var parts = candidate.BackendRef.Split('|', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Invalid username or filename"));
            }

            // Check trust score threshold (normalized to 0-1 range)
            var minTrustNormalized = opts.MinimumTrustScore / 100.0f;
            if (candidate.TrustScore < minTrustNormalized)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid(
                    $"Trust score {candidate.TrustScore:F2} below minimum {minTrustNormalized:F2}"));
            }

            // Soulseek candidates are "valid" if they pass these checks
            // Actual download attempt will happen in the Resolver
            _logger.LogDebug(
                "Validated Soulseek candidate: {Username}/{Filename} (trust: {Trust}, quality: {Quality})",
                parts[0],
                parts[1],
                candidate.TrustScore,
                candidate.ExpectedQuality);

            return Task.FromResult(SourceCandidateValidationResult.Valid(
                candidate.TrustScore,
                candidate.ExpectedQuality));
        }

        /// <summary>
        ///     Builds a search query from catalogue metadata for the content item.
        /// </summary>
        /// <remarks>
        ///     Soulseek text search should never issue opaque GUIDs when local metadata exists.
        /// </remarks>
        private async Task<string> BuildSearchQueryAsync(ContentItemId itemId, CancellationToken cancellationToken)
        {
            var track = await _catalogueStore.FindTrackByIdAsync(itemId.ToString(), cancellationToken);
            if (track == null)
            {
                return string.Empty;
            }

            Release? release = null;
            ReleaseGroup? releaseGroup = null;
            Artist? artist = null;

            if (!string.IsNullOrWhiteSpace(track.ReleaseId))
            {
                release = await _catalogueStore.FindReleaseByIdAsync(track.ReleaseId, cancellationToken);
            }

            if (release != null && !string.IsNullOrWhiteSpace(release.ReleaseGroupId))
            {
                releaseGroup = await _catalogueStore.FindReleaseGroupByIdAsync(release.ReleaseGroupId, cancellationToken);
            }

            if (releaseGroup != null && !string.IsNullOrWhiteSpace(releaseGroup.ArtistId))
            {
                artist = await _catalogueStore.FindArtistByIdAsync(releaseGroup.ArtistId, cancellationToken);
            }

            var terms = new List<string>();
            AddSearchTerm(terms, artist?.Name);
            AddSearchTerm(terms, track.Title);

            if (terms.Count < 2)
            {
                AddSearchTerm(terms, release?.Title);
                AddSearchTerm(terms, releaseGroup?.Title);
            }

            return string.Join(" ", terms);
        }

        private static void AddSearchTerm(ICollection<string> terms, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var cleaned = value
                .Replace("/", " ", StringComparison.Ordinal)
                .Replace("\\", " ", StringComparison.Ordinal)
                .Replace("\"", " ", StringComparison.Ordinal)
                .Trim();

            if (!string.IsNullOrWhiteSpace(cleaned) &&
                !terms.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
            {
                terms.Add(cleaned);
            }
        }

        /// <summary>
        ///     Calculates a trust score for a Soulseek search response.
        /// </summary>
        /// <remarks>
        ///     Trust score factors:
        ///     - Upload speed (higher = better)
        ///     - Queue length (shorter = better)
        ///     - Free upload slot (bonus)
        /// </remarks>
        private float CalculateTrustScore(SearchResponse response, SoulseekBackendOptions opts)
        {
            float score = 50.0f;

            // Upload speed factor (0-30 points)
            // 10 MB/s+.
            if (response.UploadSpeed >= 10_000_000)
            {
                score += 30;
            }

            // 5 MB/s+.
            else if (response.UploadSpeed >= 5_000_000)
            {
                score += 20;
            }

            // 1 MB/s+.
            else if (response.UploadSpeed >= 1_000_000)
            {
                score += 10;
            }

            // Queue length factor (0-10 points, penalize long queues)
            if (response.QueueLength == 0)
            {
                score += 10;
            }
            else if (response.QueueLength <= 5)
            {
                score += 5;
            }
            else if (response.QueueLength > 20)
            {
                score -= 10; // Penalty
            }

            // Free slot bonus
            if (response.HasFreeUploadSlot)
            {
                score += 10;
            }

            return Math.Clamp(score, 0, 100);
        }
    }

    /// <summary>
    ///     Configuration options for the Soulseek backend.
    /// </summary>
    public sealed class SoulseekBackendOptions
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the Soulseek backend is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets the search timeout in seconds.
        /// </summary>
        public int SearchTimeoutSeconds { get; set; } = 15;

        /// <summary>
        ///     Gets or sets the maximum number of responses to collect per search.
        /// </summary>
        public int MaxResponsesPerSearch { get; set; } = 100;

        /// <summary>
        ///     Gets or sets the maximum number of files to consider per response.
        /// </summary>
        public int MaxFilesPerResponse { get; set; } = 50;

        /// <summary>
        ///     Gets or sets the maximum number of candidates to return per item.
        /// </summary>
        public int MaxCandidatesPerItem { get; set; } = 20;

        /// <summary>
        ///     Gets or sets the minimum upload speed (bytes/sec) to consider.
        /// </summary>
        public int MinimumUploadSpeed { get; set; } = 100_000; // 100 KB/s

        /// <summary>
        ///     Gets or sets the minimum trust score to accept.
        /// </summary>
        public float MinimumTrustScore { get; set; } = 30.0f;
    }
}
