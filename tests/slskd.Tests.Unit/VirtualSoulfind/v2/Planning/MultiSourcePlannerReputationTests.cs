// <copyright file="MultiSourcePlannerReputationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.v2.Planning
{
    using System.Collections.Generic;
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
    ///     Tests for T-MCP04: MultiSourcePlanner reputation integration.
    /// </summary>
    public class MultiSourcePlannerReputationTests
    {
        private readonly Mock<ICatalogueStore> _catalogueStoreMock;
        private readonly Mock<ISourceRegistry> _sourceRegistryMock;
        private readonly Mock<IModerationProvider> _moderationProviderMock;
        private readonly Mock<PeerReputationService> _peerReputationServiceMock;
        private readonly List<IContentBackend> _backends;
        private readonly MultiSourcePlanner _planner;

        public MultiSourcePlannerReputationTests()
        {
            _catalogueStoreMock = new Mock<ICatalogueStore>();
            _sourceRegistryMock = new Mock<ISourceRegistry>();
            _moderationProviderMock = new Mock<IModerationProvider>();
            _peerReputationServiceMock = new Mock<PeerReputationService>();
            _backends = new List<IContentBackend>();

            _planner = new MultiSourcePlanner(
                _catalogueStoreMock.Object,
                _sourceRegistryMock.Object,
                _backends,
                _moderationProviderMock.Object,
                _peerReputationServiceMock.Object);
        }

        [Fact]
        public async Task CreatePlanAsync_WithBannedPeer_ExcludesBannedPeerFromPlan()
        {
            // Arrange
            var desiredTrack = new DesiredTrack("track-123");
            var goodPeer = "good-peer";
            var bannedPeer = "banned-peer";

            // Setup catalogue store
            var track = new Track("track-123", ContentDomain.Music, "Test Track", "Test Artist");
            _catalogueStoreMock.Setup(c => c.FindTrackByIdAsync(desiredTrack.TrackId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            // Setup source registry with candidates from both peers
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate(goodPeer, ContentBackendType.Mesh, 0.8f, 0.9f),
                new SourceCandidate(bannedPeer, ContentBackendType.Mesh, 0.7f, 0.8f)
            };
            _sourceRegistryMock.Setup(s => s.FindCandidatesAsync(track, It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidates);

            // Setup moderation (allow all)
            _moderationProviderMock.Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModerationDecision(ModerationVerdict.Allowed, "test"));

            // Setup reputation service (ban the banned peer)
            _peerReputationServiceMock.Setup(r => r.IsPeerAllowedForPlanningAsync(goodPeer, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _peerReputationServiceMock.Setup(r => r.IsPeerAllowedForPlanningAsync(bannedPeer, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var plan = await _planner.CreatePlanAsync(desiredTrack);

            // Assert
            Assert.Single(plan.Sources); // Only one source should remain
            Assert.Equal(goodPeer, plan.Sources[0].PeerId);
        }

        [Fact]
        public async Task CreatePlanAsync_WithAllPeersBanned_ReturnsEmptyPlan()
        {
            // Arrange
            var desiredTrack = new DesiredTrack("track-123");
            var bannedPeer1 = "banned-peer-1";
            var bannedPeer2 = "banned-peer-2";

            // Setup catalogue store
            var track = new Track("track-123", ContentDomain.Music, "Test Track", "Test Artist");
            _catalogueStoreMock.Setup(c => c.FindTrackByIdAsync(desiredTrack.TrackId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            // Setup source registry with banned peers
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate(bannedPeer1, ContentBackendType.Mesh, 0.8f, 0.9f),
                new SourceCandidate(bannedPeer2, ContentBackendType.Mesh, 0.7f, 0.8f)
            };
            _sourceRegistryMock.Setup(s => s.FindCandidatesAsync(track, It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidates);

            // Setup moderation (allow all)
            _moderationProviderMock.Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModerationDecision(ModerationVerdict.Allowed, "test"));

            // Setup reputation service (ban all peers)
            _peerReputationServiceMock.Setup(r => r.IsPeerAllowedForPlanningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var plan = await _planner.CreatePlanAsync(desiredTrack);

            // Assert
            Assert.Empty(plan.Sources); // No sources should remain
        }

        [Fact]
        public async Task CreatePlanAsync_WithPeerReputationCheckFailure_IncludesCandidate()
        {
            // Arrange - reputation check throws exception (fail-safe behavior)
            var desiredTrack = new DesiredTrack("track-123");
            var peerId = "peer-123";

            // Setup catalogue store
            var track = new Track("track-123", ContentDomain.Music, "Test Track", "Test Artist");
            _catalogueStoreMock.Setup(c => c.FindTrackByIdAsync(desiredTrack.TrackId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            // Setup source registry
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate(peerId, ContentBackendType.Mesh, 0.8f, 0.9f)
            };
            _sourceRegistryMock.Setup(s => s.FindCandidatesAsync(track, It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidates);

            // Setup moderation (allow all)
            _moderationProviderMock.Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModerationDecision(ModerationVerdict.Allowed, "test"));

            // Setup reputation service to throw exception (fail-safe)
            _peerReputationServiceMock.Setup(r => r.IsPeerAllowedForPlanningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("Reputation check failed"));

            // Act
            var plan = await _planner.CreatePlanAsync(desiredTrack);

            // Assert
            Assert.Single(plan.Sources); // Candidate should still be included (fail-safe)
            Assert.Equal(peerId, plan.Sources[0].PeerId);
        }
    }
}

