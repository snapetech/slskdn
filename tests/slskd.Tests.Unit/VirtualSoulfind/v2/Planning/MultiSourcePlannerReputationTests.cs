// <copyright file="MultiSourcePlannerReputationTests.cs" company="slskdN Team">
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
    ///     Tests for T-MCP04: MultiSourcePlanner reputation integration.
    ///     The planner only runs peer reputation checks for Soulseek candidates (BackendRef = "peerId|filepath").
    /// </summary>
    public class MultiSourcePlannerReputationTests
    {
        [Fact]
        public async Task CreatePlanAsync_WithBannedSoulseekPeer_ExcludesBannedPeerFromPlan()
        {
            var trackId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(trackId);
            var goodPeer = "good-peer";
            var bannedPeer = "banned-peer";
            var now = DateTimeOffset.UtcNow;

            var catalogueStoreMock = new Mock<ICatalogueStore>();
            catalogueStoreMock
                .Setup(c => c.FindTrackByIdAsync(trackId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Track
                {
                    TrackId = trackId,
                    ReleaseId = "rel-1",
                    TrackNumber = 1,
                    Title = "Test Track",
                    CreatedAt = now,
                    UpdatedAt = now,
                });

            var sourceRegistryMock = new Mock<ISourceRegistry>();
            sourceRegistryMock
                .Setup(s => s.FindCandidatesForItemAsync(itemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SourceCandidate>
                {
                    new SourceCandidate
                    {
                        Id = "sc-1",
                        ItemId = itemId,
                        Backend = ContentBackendType.Soulseek,
                        BackendRef = $"{goodPeer}|/music/track.flac",
                        ExpectedQuality = 0.8f,
                        TrustScore = 0.9f,
                        LastValidatedAt = now,
                        LastSeenAt = now,
                    },
                    new SourceCandidate
                    {
                        Id = "sc-2",
                        ItemId = itemId,
                        Backend = ContentBackendType.Soulseek,
                        BackendRef = $"{bannedPeer}|/music/other.flac",
                        ExpectedQuality = 0.7f,
                        TrustScore = 0.8f,
                        LastValidatedAt = now,
                        LastSeenAt = now,
                    },
                });

            var moderationMock = new Mock<IModerationProvider>();
            moderationMock
                .Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Allow("test"));

            var storeMock = new Mock<IPeerReputationStore>();
            storeMock
                .Setup(s => s.IsPeerBannedAsync(goodPeer, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            storeMock
                .Setup(s => s.IsPeerBannedAsync(bannedPeer, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var peerRep = new PeerReputationService(
                new Mock<ILogger<PeerReputationService>>().Object,
                storeMock.Object);

            var planner = new MultiSourcePlanner(
                catalogueStoreMock.Object,
                sourceRegistryMock.Object,
                Array.Empty<IContentBackend>(),
                moderationMock.Object,
                peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt-1",
                TrackId = trackId,
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var plan = await planner.CreatePlanAsync(desiredTrack);

            Assert.True(plan.IsExecutable);
            var soulseekStep = plan.Steps.FirstOrDefault(s => s.Backend == ContentBackendType.Soulseek);
            Assert.NotNull(soulseekStep);
            Assert.Single(soulseekStep.Candidates);
            Assert.StartsWith($"{goodPeer}|", soulseekStep.Candidates[0].BackendRef);
        }

        [Fact]
        public async Task CreatePlanAsync_WithAllSoulseekPeersBanned_ReturnsEmptyPlan()
        {
            var trackId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(trackId);
            var bannedPeer1 = "banned-peer-1";
            var bannedPeer2 = "banned-peer-2";
            var now = DateTimeOffset.UtcNow;

            var catalogueStoreMock = new Mock<ICatalogueStore>();
            catalogueStoreMock
                .Setup(c => c.FindTrackByIdAsync(trackId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Track
                {
                    TrackId = trackId,
                    ReleaseId = "rel-1",
                    TrackNumber = 1,
                    Title = "Test Track",
                    CreatedAt = now,
                    UpdatedAt = now,
                });

            var sourceRegistryMock = new Mock<ISourceRegistry>();
            sourceRegistryMock
                .Setup(s => s.FindCandidatesForItemAsync(itemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SourceCandidate>
                {
                    new SourceCandidate
                    {
                        Id = "sc-1",
                        ItemId = itemId,
                        Backend = ContentBackendType.Soulseek,
                        BackendRef = $"{bannedPeer1}|/path.flac",
                        ExpectedQuality = 0.8f,
                        TrustScore = 0.9f,
                        LastValidatedAt = now,
                        LastSeenAt = now,
                    },
                    new SourceCandidate
                    {
                        Id = "sc-2",
                        ItemId = itemId,
                        Backend = ContentBackendType.Soulseek,
                        BackendRef = $"{bannedPeer2}|/other.flac",
                        ExpectedQuality = 0.7f,
                        TrustScore = 0.8f,
                        LastValidatedAt = now,
                        LastSeenAt = now,
                    },
                });

            var moderationMock = new Mock<IModerationProvider>();
            moderationMock
                .Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Allow("test"));

            var storeMock = new Mock<IPeerReputationStore>();
            storeMock
                .Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var peerRep = new PeerReputationService(
                new Mock<ILogger<PeerReputationService>>().Object,
                storeMock.Object);

            var planner = new MultiSourcePlanner(
                catalogueStoreMock.Object,
                sourceRegistryMock.Object,
                Array.Empty<IContentBackend>(),
                moderationMock.Object,
                peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt-1",
                TrackId = trackId,
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var plan = await planner.CreatePlanAsync(desiredTrack);

            Assert.Empty(plan.Steps);
        }

        [Fact]
        public async Task CreatePlanAsync_WithPeerReputationCheckThrowing_ExcludesCandidate()
        {
            var trackId = ContentItemId.NewId().ToString();
            var itemId = ContentItemId.Parse(trackId);
            var peerId = "peer-123";
            var now = DateTimeOffset.UtcNow;

            var catalogueStoreMock = new Mock<ICatalogueStore>();
            catalogueStoreMock
                .Setup(c => c.FindTrackByIdAsync(trackId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Track
                {
                    TrackId = trackId,
                    ReleaseId = "rel-1",
                    TrackNumber = 1,
                    Title = "Test Track",
                    CreatedAt = now,
                    UpdatedAt = now,
                });

            var sourceRegistryMock = new Mock<ISourceRegistry>();
            sourceRegistryMock
                .Setup(s => s.FindCandidatesForItemAsync(itemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SourceCandidate>
                {
                    new SourceCandidate
                    {
                        Id = "sc-1",
                        ItemId = itemId,
                        Backend = ContentBackendType.Soulseek,
                        BackendRef = $"{peerId}|/path.flac",
                        ExpectedQuality = 0.8f,
                        TrustScore = 0.9f,
                        LastValidatedAt = now,
                        LastSeenAt = now,
                    },
                });

            var moderationMock = new Mock<IModerationProvider>();
            moderationMock
                .Setup(m => m.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Allow("test"));

            var storeMock = new Mock<IPeerReputationStore>();
            storeMock
                .Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Reputation check failed"));

            var peerRep = new PeerReputationService(
                new Mock<ILogger<PeerReputationService>>().Object,
                storeMock.Object);

            var planner = new MultiSourcePlanner(
                catalogueStoreMock.Object,
                sourceRegistryMock.Object,
                Array.Empty<IContentBackend>(),
                moderationMock.Object,
                peerRep);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "dt-1",
                TrackId = trackId,
                Priority = IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var plan = await planner.CreatePlanAsync(desiredTrack);

            Assert.Empty(plan.Steps);
        }
    }
}
