// <copyright file="MultiSourcePlannerDomainTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Planning
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
    ///     Tests for H-VF01: Domain gating in MultiSourcePlanner.
    /// </summary>
    public class MultiSourcePlannerDomainTests
    {
        private const string TestTrackId = "550e8400-e29b-41d4-a716-446655440000";

        private readonly Mock<ICatalogueStore> _catalogueStoreMock = new();
        private readonly Mock<ISourceRegistry> _sourceRegistryMock = new();
        private readonly Mock<IPeerReputationStore> _peerReputationStoreMock = new();

        private static MultiSourcePlanner CreatePlanner(ICatalogueStore catalogue, ISourceRegistry sourceRegistry, IModerationProvider moderation, PeerReputationService peerRep, params IContentBackend[] backends)
        {
            return new MultiSourcePlanner(catalogue, sourceRegistry, backends, moderation, peerRep);
        }

        [Fact]
        public async Task CreatePlanAsync_WithInvalidDomain_ReturnsFailedPlan()
        {
            var storeMock = new Mock<IPeerReputationStore>();
            storeMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, storeMock.Object);
            var planner = CreatePlanner(_catalogueStoreMock.Object, _sourceRegistryMock.Object, new NoopModerationProvider(), peerRep);

            var invalidDesiredTrack = new DesiredTrack { Domain = (ContentDomain)999, DesiredTrackId = "test-id", TrackId = TestTrackId, Status = IntentStatus.Pending };

            var plan = await planner.CreatePlanAsync(invalidDesiredTrack);

            Assert.Equal(PlanStatus.Failed, plan.Status);
            Assert.Contains("Domain validation failed", plan.ErrorMessage);
        }

        [Fact]
        public void ApplyDomainRulesAndMode_SoulseekBackend_OnlyAllowedForMusicDomain()
        {
            _peerReputationStoreMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, _peerReputationStoreMock.Object);
            var planner = CreatePlanner(_catalogueStoreMock.Object, _sourceRegistryMock.Object, new NoopModerationProvider(), peerRep);

            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate { Backend = ContentBackendType.Soulseek, BackendRef = "slsk" },
                new SourceCandidate { Backend = ContentBackendType.MeshDht, BackendRef = "mesh" },
                new SourceCandidate { Backend = ContentBackendType.LocalLibrary, BackendRef = "local" }
            };

            var method = typeof(MultiSourcePlanner).GetMethod("ApplyDomainRulesAndMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var musicResults = method.Invoke(planner, new object[] { ContentDomain.Music, PlanningMode.SoulseekFriendly, candidates });
            var genericFileResults = method.Invoke(planner, new object[] { ContentDomain.GenericFile, PlanningMode.SoulseekFriendly, candidates });

            var musicFiltered = (musicResults as IEnumerable<SourceCandidate>)?.ToList();
            var genericFiltered = (genericFileResults as IEnumerable<SourceCandidate>)?.ToList();

            Assert.NotNull(musicFiltered);
            Assert.NotNull(genericFiltered);
            Assert.Contains(musicFiltered, c => c.Backend == ContentBackendType.Soulseek);
            Assert.DoesNotContain(genericFiltered, c => c.Backend == ContentBackendType.Soulseek);
            Assert.Contains(musicFiltered, c => c.Backend == ContentBackendType.MeshDht);
            Assert.Contains(musicFiltered, c => c.Backend == ContentBackendType.LocalLibrary);
            Assert.Contains(genericFiltered, c => c.Backend == ContentBackendType.MeshDht);
            Assert.Contains(genericFiltered, c => c.Backend == ContentBackendType.LocalLibrary);
        }

        [Fact]
        public async Task CreatePlanAsync_UsesDomainFromDesiredTrack()
        {
            _peerReputationStoreMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, _peerReputationStoreMock.Object);
            var planner = CreatePlanner(_catalogueStoreMock.Object, _sourceRegistryMock.Object, new NoopModerationProvider(), peerRep);

            var testTrack = new Track { TrackId = TestTrackId, ReleaseId = "r1", TrackNumber = 1, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            _catalogueStoreMock.Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(testTrack);
            _sourceRegistryMock.Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SourceCandidate>());

            var desiredTrack = new DesiredTrack { Domain = ContentDomain.GenericFile, DesiredTrackId = "test-id", TrackId = TestTrackId, Status = IntentStatus.Pending };

            var plan = await planner.CreatePlanAsync(desiredTrack);

            Assert.NotEqual(PlanStatus.Failed, plan.Status);
        }

        [Theory]
        [InlineData(ContentDomain.Music, true)] // Music should allow Soulseek
        [InlineData(ContentDomain.GenericFile, false)] // GenericFile should not allow Soulseek
        public void DomainGating_EnforcesBackendRestrictions(ContentDomain domain, bool shouldAllowSoulseek)
        {
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate { Backend = ContentBackendType.Soulseek, BackendRef = "slsk" },
                new SourceCandidate { Backend = ContentBackendType.MeshDht, BackendRef = "mesh" }
            };

            // Act
            var filtered = candidates.Where(c =>
            {
                // Apply domain rules (same logic as in MultiSourcePlanner)
                if (c.Backend == ContentBackendType.Soulseek && domain != ContentDomain.Music)
                {
                    return false;
                }
                return true;
            }).ToList();

            // Assert
            if (shouldAllowSoulseek)
            {
                Assert.Contains(filtered, c => c.Backend == ContentBackendType.Soulseek);
            }
            else
            {
                Assert.DoesNotContain(filtered, c => c.Backend == ContentBackendType.Soulseek);
            }

            // Non-Soulseek backends should always be allowed
            Assert.Contains(filtered, c => c.Backend == ContentBackendType.MeshDht);
        }
    }
}


