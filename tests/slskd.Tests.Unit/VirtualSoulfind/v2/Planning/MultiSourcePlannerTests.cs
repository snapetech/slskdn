// <copyright file="MultiSourcePlannerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
            using var catalogueStore = new InMemoryCatalogueStore();
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
            using var catalogueStore = new InMemoryCatalogueStore();
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
            using var catalogueStore = new InMemoryCatalogueStore();
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
            using var catalogueStore = new InMemoryCatalogueStore();
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
            using var catalogueStore = new InMemoryCatalogueStore();
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
            using var catalogueStore = new InMemoryCatalogueStore();
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
