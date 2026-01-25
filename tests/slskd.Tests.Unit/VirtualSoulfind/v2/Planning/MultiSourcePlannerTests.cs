// <copyright file="MultiSourcePlannerTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.Common.Moderation;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     Tests for T-V2-P2-02: Multi-Source Planner.
    /// </summary>
    public class MultiSourcePlannerTests
    {
        [Fact]
        public async Task CreatePlan_NoTrackInCatalogue_ReturnsEmptyPlan()
        {
            // Arrange
            var catalogueStore = new InMemoryCatalogueStore();
            var sourceRegistry = new InMemorySourceRegistry();
            var backends = Array.Empty<IContentBackend>();
            var mcp = new NoopModerationProvider();
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = new MultiSourcePlanner(catalogueStore, sourceRegistry, backends, mcp, peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt:1",
                TrackId = Guid.NewGuid().ToString(), // Track not in catalogue
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            var plan = await planner.CreatePlanAsync(desiredTrack);

            // Assert
            Assert.False(plan.IsExecutable);
            Assert.Empty(plan.Steps);
        }

        [Fact]
        public async Task CreatePlan_LocalCandidates_OrderedFirst()
        {
            // Arrange
            var catalogueStore = new InMemoryCatalogueStore();
            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Test Track",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var sourceRegistry = new InMemorySourceRegistry();
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "sc:1",
                ItemId = ContentItemId.Parse(trackId),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "slsk:user1:file1",
                ExpectedQuality = 85,
                TrustScore = 0.7f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "sc:2",
                ItemId = ContentItemId.Parse(trackId),
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = "local:/music/track.flac",
                ExpectedQuality = 100,
                TrustScore = 1.0f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });

            var backends = Array.Empty<IContentBackend>();
            var mcp = new NoopModerationProvider();
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = new MultiSourcePlanner(catalogueStore, sourceRegistry, backends, mcp, peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt:1",
                TrackId = trackId,
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            var plan = await planner.CreatePlanAsync(desiredTrack);

            // Assert
            Assert.True(plan.IsExecutable);
            Assert.Equal(2, plan.Steps.Count);
            Assert.Equal(ContentBackendType.LocalLibrary, plan.Steps[0].Backend);
            Assert.Equal(ContentBackendType.Soulseek, plan.Steps[1].Backend);
        }

        [Fact]
        public async Task CreatePlan_OfflinePlanning_OnlyLocal()
        {
            // Arrange
            var catalogueStore = new InMemoryCatalogueStore();
            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Test Track",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var sourceRegistry = new InMemorySourceRegistry();
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "sc:1",
                ItemId = ContentItemId.Parse(trackId),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "slsk:user1:file1",
                ExpectedQuality = 85,
                TrustScore = 0.7f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "sc:2",
                ItemId = ContentItemId.Parse(trackId),
                Backend = ContentBackendType.LocalLibrary,
                BackendRef = "local:/music/track.flac",
                ExpectedQuality = 100,
                TrustScore = 1.0f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });

            var backends = Array.Empty<IContentBackend>();
            var mcp = new NoopModerationProvider();
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = new MultiSourcePlanner(catalogueStore, sourceRegistry, backends, mcp, peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt:1",
                TrackId = trackId,
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            var plan = await planner.CreatePlanAsync(desiredTrack, PlanningMode.OfflinePlanning);

            // Assert
            Assert.True(plan.IsExecutable);
            Assert.Single(plan.Steps);
            Assert.Equal(ContentBackendType.LocalLibrary, plan.Steps[0].Backend);
        }

        [Fact]
        public async Task CreatePlan_MeshOnly_NoSoulseek()
        {
            // Arrange
            var catalogueStore = new InMemoryCatalogueStore();
            var trackId = ContentItemId.NewId().ToString();
            await catalogueStore.UpsertTrackAsync(new Track
            {
                TrackId = trackId,
                ReleaseId = "rel:1",
                TrackNumber = 1,
                Title = "Test Track",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var sourceRegistry = new InMemorySourceRegistry();
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "sc:1",
                ItemId = ContentItemId.Parse(trackId),
                Backend = ContentBackendType.Soulseek,
                BackendRef = "slsk:user1:file1",
                ExpectedQuality = 85,
                TrustScore = 0.7f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });
            await sourceRegistry.UpsertCandidateAsync(new SourceCandidate
            {
                Id = "sc:2",
                ItemId = ContentItemId.Parse(trackId),
                Backend = ContentBackendType.MeshDht,
                BackendRef = "mesh:content:abcd1234",
                ExpectedQuality = 90,
                TrustScore = 0.8f,
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });

            var backends = Array.Empty<IContentBackend>();
            var mcp = new NoopModerationProvider();
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = new MultiSourcePlanner(catalogueStore, sourceRegistry, backends, mcp, peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt:1",
                TrackId = trackId,
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            var plan = await planner.CreatePlanAsync(desiredTrack, PlanningMode.MeshOnly);

            // Assert
            Assert.True(plan.IsExecutable);
            Assert.Single(plan.Steps);
            Assert.Equal(ContentBackendType.MeshDht, plan.Steps[0].Backend);
        }

        [Fact]
        public async Task ValidatePlan_EmptyPlan_ReturnsFalse()
        {
            // Arrange
            var catalogueStore = new InMemoryCatalogueStore();
            var sourceRegistry = new InMemorySourceRegistry();
            var backends = Array.Empty<IContentBackend>();
            var mcp = new NoopModerationProvider();
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = new MultiSourcePlanner(catalogueStore, sourceRegistry, backends, mcp, peerRep);

            var plan = new TrackAcquisitionPlan
            {
                TrackId = "track:1",
                Mode = PlanningMode.SoulseekFriendly,
                Steps = Array.Empty<PlanStep>(),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            var isValid = await planner.ValidatePlanAsync(plan);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidatePlan_WithSteps_ReturnsTrue()
        {
            // Arrange
            var catalogueStore = new InMemoryCatalogueStore();
            var sourceRegistry = new InMemorySourceRegistry();
            var backends = Array.Empty<IContentBackend>();
            var mcp = new NoopModerationProvider();
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = new MultiSourcePlanner(catalogueStore, sourceRegistry, backends, mcp, peerRep);

            var plan = new TrackAcquisitionPlan
            {
                TrackId = "track:1",
                Mode = PlanningMode.SoulseekFriendly,
                Steps = new List<PlanStep>
                {
                    new PlanStep
                    {
                        Backend = ContentBackendType.LocalLibrary,
                        Candidates = new List<SourceCandidate>(),
                        MaxParallel = 1,
                        Timeout = TimeSpan.FromSeconds(5),
                        FallbackMode = PlanStepFallbackMode.Cascade,
                    },
                },
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // Act
            var isValid = await planner.ValidatePlanAsync(plan);

            // Assert
            Assert.True(isValid);
        }
    }
}
