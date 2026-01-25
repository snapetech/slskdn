// <copyright file="VirtualSoulfindV2IntegrationTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Integration
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Moderation;
    using slskd.Shares;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Matching;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     End-to-end integration tests for VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     These tests verify that all the v2 components work together correctly:
    ///     - Catalogue → Intent → Planner → Backends → Match Engine
    ///     
    ///     This is the "smoke test" that proves the foundation is solid.
    /// </remarks>
    public class VirtualSoulfindV2IntegrationTests
    {
        [Fact]
        public async Task EndToEnd_LocalFileExists_PlanIncludesLocal()
        {
            // Arrange: Set up a complete v2 stack
            var catalogueStore = new InMemoryCatalogueStore();
            var sourceRegistry = new InMemorySourceRegistry();
            
            // Create artist, release, and track in catalogue
            var artistId = "artist:pink-floyd";
            await catalogueStore.UpsertArtistAsync(new Artist
            {
                ArtistId = artistId,
                Name = "Pink Floyd",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var releaseId = "release:dsotm-1973";
            await catalogueStore.UpsertReleaseAsync(new Release
            {
                ReleaseId = releaseId,
                ReleaseGroupId = "rg:dsotm",
                Title = "The Dark Side of the Moon",
                Year = 1973,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = releaseId,
                TrackNumber = 5,
                Title = "Time",
                DurationSeconds = 414,
                MusicBrainzRecordingId = "mbid-time-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // LocalLibraryBackend has SupportedDomain=null and is skipped by the planner; use sourceRegistry to supply a LocalLibrary candidate
            var now = DateTimeOffset.UtcNow;
            var itemId = ContentItemId.Parse(trackId);
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "local:1",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = trackId,
                ExpectedQuality = 100,
                TrustScore = 1.0f,
                LastValidatedAt = now,
                LastSeenAt = now,
                IsPreferred = true,
            });

            var mockMcp = new Mock<IModerationProvider>();
            mockMcp.Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Allow("test"));

            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);

            var planner = new MultiSourcePlanner(
                catalogueStore,
                sourceRegistry,
                Array.Empty<IContentBackend>(),
                mockMcp.Object,
                peerRep,
                PlanningMode.SoulseekFriendly);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "intent:1",
                TrackId = trackId,
                Priority = IntentPriority.High,
                Status = IntentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };

            // Act: Generate plan
            var plan = await planner.CreatePlanAsync(desiredTrack);

            Assert.True(plan.IsExecutable);
            Assert.NotEmpty(plan.Steps);
            var localStep = plan.Steps.FirstOrDefault(s => s.Backend == ContentBackendType.LocalLibrary);
            Assert.NotNull(localStep);
            Assert.Single(localStep.Candidates);
            var candidate = localStep.Candidates.First();
            Assert.Equal(1.0f, candidate.TrustScore);
            Assert.Equal(100, candidate.ExpectedQuality);
            Assert.True(candidate.IsPreferred);
        }

        [Fact]
        public async Task EndToEnd_MCPBlocksContent_PlanIsEmpty()
        {
            // Arrange: Same setup but MCP blocks the content
            var catalogueStore = new InMemoryCatalogueStore();
            var sourceRegistry = new InMemorySourceRegistry();
            
            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Blocked Track",
                DurationSeconds = 200,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var itemId = ContentItemId.Parse(trackId);
            var now2 = DateTimeOffset.UtcNow;
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "local:1",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = trackId,
                ExpectedQuality = 100,
                TrustScore = 1.0f,
                LastValidatedAt = now2,
                LastSeenAt = now2,
            });

            // MCP BLOCKS this content; candidate is filtered out, plan is empty
            var mockMcp = new Mock<IModerationProvider>();
            mockMcp.Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Block("Prohibited content"));

            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);

            var planner = new MultiSourcePlanner(
                catalogueStore,
                sourceRegistry,
                Array.Empty<IContentBackend>(),
                mockMcp.Object,
                peerRep,
                PlanningMode.SoulseekFriendly);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "intent:1",
                TrackId = trackId,
                Priority = IntentPriority.High,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var plan = await planner.CreatePlanAsync(desiredTrack);

            // MCP blocked everything; no backends with SupportedDomain=Music contributed, plan is empty
            Assert.False(plan.IsExecutable);
            Assert.Empty(plan.Steps);
        }

        [Fact]
        public async Task EndToEnd_MatchEngine_VerifiesCorrectFile()
        {
            // Arrange: Match engine with catalogue
            var catalogueStore = new InMemoryCatalogueStore();
            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Comfortably Numb",
                DurationSeconds = 382,
                MusicBrainzRecordingId = "mbid-numb-123",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var track = await catalogueStore.FindTrackByIdAsync(trackId);
            var matchEngine = new SimpleMatchEngine();

            // Candidate file with matching MBID and duration
            var goodCandidate = new CandidateFileMetadata
            {
                Filename = "Comfortably_Numb.flac",
                Extension = ".flac",
                Size = 35_000_000,
                DurationSeconds = 384,
                Embedded = new EmbeddedMetadata
                {
                    Title = "Comfortably Numb",
                    MusicBrainzRecordingId = "mbid-numb-123",
                },
            };

            // Act
            var matchResult = await matchEngine.MatchAsync(track, goodCandidate);
            var verifyResult = await matchEngine.VerifyAsync(track, goodCandidate);

            // Assert
            Assert.Equal(MatchConfidence.Strong, matchResult.Confidence);
            Assert.True(matchResult.IsStrong);
            Assert.Equal(MatchConfidence.Strong, verifyResult.Confidence);
            Assert.True(verifyResult.IsStrong);
        }

        [Fact]
        public async Task EndToEnd_OfflinePlanning_OnlyLocalSources()
        {
            // Arrange: Multiple backends, but OfflinePlanning mode
            var catalogueStore = new InMemoryCatalogueStore();
            var sourceRegistry = new InMemorySourceRegistry();
            
            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Test Track",
                DurationSeconds = 200,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            // Add both local and non-local sources
            var itemId = ContentItemId.Parse(trackId);
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "local:1",
                ItemId = itemId,
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = trackId,
                ExpectedQuality = 100,
                TrustScore = 1.0f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });

            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "mesh:1",
                ItemId = itemId,
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh:content:abc",
                ExpectedQuality = 90,
                TrustScore = 0.8f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });

            var mockMcp = new Mock<IModerationProvider>();
            mockMcp.Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Allow("test"));

            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);

            var planner = new MultiSourcePlanner(
                catalogueStore,
                sourceRegistry,
                Array.Empty<IContentBackend>(),
                mockMcp.Object,
                peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "intent:1",
                TrackId = trackId,
                Priority = IntentPriority.High,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var plan = await planner.CreatePlanAsync(desiredTrack, PlanningMode.OfflinePlanning);

            // Assert: Only local backend in plan
            Assert.True(plan.IsExecutable);
            Assert.Single(plan.Steps);
            Assert.Equal(ContentBackendType.LocalLibrary, plan.Steps[0].Backend);
        }
    }
}
