// <copyright file="MultiSourcePlanner.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Common.Moderation;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Multi-source planner - the "brain" of VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     The planner implements the core domain rules:
    ///     
    ///     **Domain Rules:**
    ///     - Soulseek ONLY for ContentDomain.Music
    ///     - Non-music (GenericFile, Book, Movie, Tv) ONLY uses Mesh/DHT/Torrent/HTTP/LAN
    ///     
    ///     **MCP Integration:**
    ///     - ALL source candidates filtered through IModerationProvider.CheckContentIdAsync
    ///     - Blocked/Quarantined sources NEVER included in plans
    ///     
    ///     **Backend Selection:**
    ///     - Respects PlanningMode (OfflinePlanning/MeshOnly/SoulseekFriendly)
    ///     - Orders by: LocalLibrary → Mesh/DHT → HTTP → Soulseek (if allowed)
    ///     - Within backend: order by TrustScore DESC, then ExpectedQuality DESC
    ///     
    ///     **Caps & Budgets:**
    ///     - No plan validation against caps yet (future: integrate work budget checker)
    ///     - Relies on resolver/executor to respect H-08 Soulseek caps
    ///     
    ///     This is the foundation. SqliteCatalogueStore, real backends, and
    ///     work budget integration come in later phases.
    /// </remarks>
    public sealed class MultiSourcePlanner : IPlanner
    {
        private readonly ICatalogueStore _catalogueStore;
        private readonly ISourceRegistry _sourceRegistry;
        private readonly IEnumerable<IContentBackend> _backends;
        private readonly IModerationProvider _moderationProvider;
        private readonly PeerReputationService _peerReputationService;
        private readonly PlanningMode _defaultMode;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MultiSourcePlanner"/> class.
        /// </summary>
        /// <param name="catalogueStore">The catalogue store.</param>
        /// <param name="sourceRegistry">The source registry.</param>
        /// <param name="backends">Available content backends.</param>
        /// <param name="moderationProvider">The moderation provider for filtering.</param>
        /// <param name="defaultMode">Default planning mode (defaults to SoulseekFriendly).</param>
        public MultiSourcePlanner(
            ICatalogueStore catalogueStore,
            ISourceRegistry sourceRegistry,
            IEnumerable<IContentBackend> backends,
            IModerationProvider moderationProvider,
            PeerReputationService peerReputationService,
            PlanningMode defaultMode = PlanningMode.SoulseekFriendly)
        {
            _catalogueStore = catalogueStore ?? throw new ArgumentNullException(nameof(catalogueStore));
            _sourceRegistry = sourceRegistry ?? throw new ArgumentNullException(nameof(sourceRegistry));
            _backends = backends ?? throw new ArgumentNullException(nameof(backends));
            _moderationProvider = moderationProvider ?? throw new ArgumentNullException(nameof(moderationProvider));
            _peerReputationService = peerReputationService ?? throw new ArgumentNullException(nameof(peerReputationService));
            _defaultMode = defaultMode;
        }

        /// <inheritdoc/>
        public async Task<TrackAcquisitionPlan> CreatePlanAsync(
            DesiredTrack desiredTrack,
            PlanningMode? mode = null,
            CancellationToken cancellationToken = default)
        {
            // H-VF01: Validate ContentDomain before planning
            if (!VirtualSoulfindValidation.IsValidContentDomain(desiredTrack.Domain, out var domainError))
            {
                return new TrackAcquisitionPlan
                {
                    DesiredTrack = desiredTrack,
                    Status = PlanStatus.Failed,
                    ErrorMessage = $"Domain validation failed: {domainError}",
                    Steps = Array.Empty<PlanStep>(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow,
                };
            }

            var effectiveMode = mode ?? _defaultMode;

            // Step 1: Look up track in catalogue to get ContentDomain
            var track = await _catalogueStore.FindTrackByIdAsync(desiredTrack.TrackId, cancellationToken);
            if (track == null)
            {
                // No track in catalogue = no plan
                return new TrackAcquisitionPlan
                {
                    TrackId = desiredTrack.TrackId,
                    Mode = effectiveMode,
                    Steps = Array.Empty<PlanStep>(),
                    CreatedAt = DateTimeOffset.UtcNow,
                };
            }

            // H-VF01: Use domain from DesiredTrack instead of hardcoding
            var domain = desiredTrack.Domain;

            // Step 2: Query source registry for existing candidates
            // Convert TrackId (string) to ContentItemId for v1 compatibility
            // Future: unified ID system
            ContentItemId itemId;
            try
            {
                itemId = ContentItemId.Parse(desiredTrack.TrackId);
            }
            catch
            {
                // Invalid TrackId format
                return new TrackAcquisitionPlan
                {
                    TrackId = desiredTrack.TrackId,
                    Mode = effectiveMode,
                    Steps = Array.Empty<PlanStep>(),
                    CreatedAt = DateTimeOffset.UtcNow,
                };
            }

            var registryCandidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                cancellationToken);

            // Step 3: Query backends for additional candidates (if they support FindCandidatesAsync)
            var backendCandidates = new List<SourceCandidate>();
            foreach (var backend in _backends)
            {
                if (backend.SupportedDomain != domain)
                {
                    continue; // Skip backends that don't support this domain
                }

                try
                {
                    var candidates = await backend.FindCandidatesAsync(
                        itemId,
                        cancellationToken);
                    backendCandidates.AddRange(candidates);
                }
                catch
                {
                    // Backend query failed; skip
                }
            }

            // Step 4: Merge + dedupe candidates
            var allCandidates = registryCandidates
                .Concat(backendCandidates)
                .GroupBy(c => $"{c.Backend}:{c.BackendRef}")
                .Select(g => g.First()) // Take first from each group (deduplication)
                .ToList();

            // Step 5: Filter through MCP (CheckContentIdAsync for Music domain)
            var filteredCandidates = await FilterThroughModerationAsync(
                desiredTrack.TrackId,
                domain,
                allCandidates,
                cancellationToken);

            // Step 6: Apply domain rules + planning mode
            var allowedCandidates = ApplyDomainRulesAndMode(
                domain,
                effectiveMode,
                filteredCandidates);

            // Step 7: Order by backend preference, then trust/quality
            var orderedCandidates = OrderCandidatesByPreference(allowedCandidates);

            // Step 8: Build plan steps
            var steps = BuildPlanSteps(orderedCandidates);

            return new TrackAcquisitionPlan
            {
                TrackId = desiredTrack.TrackId,
                Mode = effectiveMode,
                Steps = steps,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }

        /// <inheritdoc/>
        public Task<bool> ValidatePlanAsync(
            TrackAcquisitionPlan plan,
            CancellationToken cancellationToken = default)
        {
            // For v2 Phase 1: basic validation (plan must have steps)
            // Future: check work budgets, backend availability, per-backend caps
            return Task.FromResult(plan.IsExecutable);
        }

        // ========== Private Helper Methods ==========

        private async Task<List<SourceCandidate>> FilterThroughModerationAsync(
            string trackId,
            ContentDomain domain,
            List<SourceCandidate> candidates,
            CancellationToken cancellationToken)
        {
            var filtered = new List<SourceCandidate>();

            foreach (var candidate in candidates)
            {
                try
                {
                    // Step 1: Check through MCP using contentId as string
                    var decision = await _moderationProvider.CheckContentIdAsync(
                        trackId,
                        cancellationToken);

                    if (decision.Verdict == ModerationVerdict.Blocked ||
                        decision.Verdict == ModerationVerdict.Quarantined)
                    {
                        // Skip blocked/quarantined content
                        continue;
                    }

                    // Step 2: Check peer reputation (T-MCP04)
                    if (!string.IsNullOrWhiteSpace(candidate.PeerId))
                    {
                        var isAllowed = await _peerReputationService.IsPeerAllowedForPlanningAsync(
                            candidate.PeerId,
                            cancellationToken);

                        if (!isAllowed)
                        {
                            // Skip banned peers
                            continue;
                        }
                    }

                    filtered.Add(candidate);
                }
                catch
                {
                    // MCP or reputation check failed; skip this candidate (fail-safe)
                    continue;
                }
            }

            return filtered;
        }

        private List<SourceCandidate> ApplyDomainRulesAndMode(
            ContentDomain domain,
            PlanningMode mode,
            List<SourceCandidate> candidates)
        {
            return candidates.Where(c =>
            {
                // Rule 1: Soulseek ONLY for Music domain
                if (c.Backend == ContentBackendType.Soulseek && domain != ContentDomain.Music)
                {
                    return false;
                }

                // Rule 2: Planning mode restrictions
                switch (mode)
                {
                    case PlanningMode.OfflinePlanning:
                        // Only local library
                        return c.Backend == ContentBackendType.LocalLibrary;

                    case PlanningMode.MeshOnly:
                        // No Soulseek
                        return c.Backend != ContentBackendType.Soulseek;

                    case PlanningMode.SoulseekFriendly:
                        // Everything allowed (with caps)
                        return true;

                    default:
                        return true;
                }
            }).ToList();
        }

        private List<SourceCandidate> OrderCandidatesByPreference(List<SourceCandidate> candidates)
        {
            // Backend priority order:
            // 1. LocalLibrary (instant, free)
            // 2. MeshDht (decentralized, scalable)
            // 3. Http (reliable, direct)
            // 4. Lan (fast, local network)
            // 5. Torrent (good for large files, slower startup)
            // 6. Soulseek (last resort for Music, with caps)

            var backendPriority = new Dictionary<ContentBackendType, int>
            {
                { ContentBackendType.LocalLibrary, 1 },
                { ContentBackendType.MeshDht, 2 },
                { ContentBackendType.Http, 3 },
                { ContentBackendType.Lan, 4 },
                { ContentBackendType.Torrent, 5 },
                { ContentBackendType.Soulseek, 6 },
            };

            return candidates
                .OrderBy(c => backendPriority.GetValueOrDefault(c.Backend, 999))
                .ThenByDescending(c => c.TrustScore)
                .ThenByDescending(c => c.ExpectedQuality)
                .ToList();
        }

        private List<PlanStep> BuildPlanSteps(List<SourceCandidate> orderedCandidates)
        {
            if (orderedCandidates.Count == 0)
            {
                return new List<PlanStep>();
            }

            // Group by backend
            var steps = orderedCandidates
                .GroupBy(c => c.Backend)
                .Select(g => new PlanStep
                {
                    Backend = g.Key,
                    Candidates = g.ToList(),
                    MaxParallel = g.Key == ContentBackendType.LocalLibrary ? 1 : 3,
                    Timeout = GetBackendTimeout(g.Key),
                    FallbackMode = g.Key == ContentBackendType.Soulseek
                        ? PlanStepFallbackMode.Cascade // Try Soulseek sources one at a time
                        : PlanStepFallbackMode.FanOut, // Try mesh/torrent in parallel
                })
                .ToList();

            return steps;
        }

        private TimeSpan GetBackendTimeout(ContentBackendType backend)
        {
            return backend switch
            {
                ContentBackendType.LocalLibrary => TimeSpan.FromSeconds(5),
                ContentBackendType.MeshDht => TimeSpan.FromSeconds(30),
                ContentBackendType.Http => TimeSpan.FromSeconds(60),
                ContentBackendType.Lan => TimeSpan.FromSeconds(15),
                ContentBackendType.Torrent => TimeSpan.FromMinutes(5),
                ContentBackendType.Soulseek => TimeSpan.FromMinutes(2),
                _ => TimeSpan.FromMinutes(1),
            };
        }
    }

    // NOTE: SourceCandidate is defined in slskd.VirtualSoulfind.v2.Sources namespace
    // The duplicate definition below has been commented out to avoid conflicts
    /*
    /// <summary>
    /// Represents a source candidate for multi-source downloads.
    /// </summary>
    public record SourceCandidate
    {
        /// <summary>
        /// Gets the peer ID of the source.
        /// </summary>
        public string PeerId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the backend type for this source.
        /// </summary>
        public ContentBackendType Backend { get; init; }

        /// <summary>
        /// Gets the file path or resource identifier.
        /// </summary>
        public string ResourcePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the quality score for this source (0-100).
        /// </summary>
        public int QualityScore { get; init; }

        /// <summary>
        /// Gets the trust score for this source's peer (0-100).
        /// </summary>
        public int TrustScore { get; init; }

        /// <summary>
        /// Gets the expected quality level for this source.
        /// </summary>
        public int ExpectedQuality { get; init; }
    }
    */
}
