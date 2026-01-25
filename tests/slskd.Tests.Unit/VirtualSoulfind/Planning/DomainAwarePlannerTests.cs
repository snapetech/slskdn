// <copyright file="DomainAwarePlannerTests.cs" company="slskdN Team">
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
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     Tests for T-VC04: Domain-Aware Planner + Soulseek Gating.
    /// </summary>
    public class DomainAwarePlannerTests
    {
        private const string TestTrackId = "550e8400-e29b-41d4-a716-446655440000";

        private readonly Mock<ICatalogueStore> _catalogueStoreMock = new();
        private readonly Mock<ISourceRegistry> _sourceRegistryMock = new();
        private readonly List<IContentBackend> _backends = new();
        private readonly Mock<IModerationProvider> _moderationProviderMock = new();
        private readonly Mock<IPeerReputationStore> _peerReputationStoreMock = new();

        private MultiSourcePlanner CreatePlanner()
        {
            _peerReputationStoreMock.Setup(s => s.IsPeerBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var peerRep = new PeerReputationService(new Mock<ILogger<PeerReputationService>>().Object, _peerReputationStoreMock.Object);
            return new MultiSourcePlanner(
                _catalogueStoreMock.Object,
                _sourceRegistryMock.Object,
                _backends,
                _moderationProviderMock.Object,
                peerRep);
        }

        [Fact]
        public async Task CreatePlanAsync_WithInvalidDomain_ReturnsFailedPlan()
        {
            // Arrange
            var planner = CreatePlanner();
            var invalidDesiredTrack = new DesiredTrack
            {
                Domain = (ContentDomain)999, // Invalid domain
                DesiredTrackId = "test-id",
                TrackId = TestTrackId,
                Status = IntentStatus.Pending
            };

            // Act
            var plan = await planner.CreatePlanAsync(invalidDesiredTrack);

            // Assert
            Assert.Equal(PlanStatus.Failed, plan.Status);
            Assert.Contains("Domain validation failed", plan.ErrorMessage);
        }

        [Fact]
        public async Task CreatePlanAsync_QueriesOnlyBackendsSupportingDomain()
        {
            // Arrange - Backend.SupportedDomain != domain is skipped; null is not "all domains"
            var musicBackendMock = new Mock<IContentBackend>();
            musicBackendMock.Setup(x => x.Type).Returns(ContentBackendType.Soulseek);
            musicBackendMock.Setup(x => x.SupportedDomain).Returns(ContentDomain.Music);
            musicBackendMock.Setup(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());

            var genericOnlyBackendMock = new Mock<IContentBackend>();
            genericOnlyBackendMock.Setup(x => x.Type).Returns(ContentBackendType.LocalLibrary);
            genericOnlyBackendMock.Setup(x => x.SupportedDomain).Returns(ContentDomain.GenericFile);
            genericOnlyBackendMock.Setup(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());

            _backends.AddRange(new[] { musicBackendMock.Object, genericOnlyBackendMock.Object });

            var planner = CreatePlanner();

            var testTrack = new Track { TrackId = TestTrackId, ReleaseId = "r1", TrackNumber = 1, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            _catalogueStoreMock.Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(testTrack);
            _sourceRegistryMock.Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SourceCandidate>());
            _moderationProviderMock.Setup(x => x.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ModerationDecision.Allow("Test"));

            // Act - Plan for Music domain (only Music backend is queried)
            var musicDesiredTrack = new DesiredTrack { Domain = ContentDomain.Music, DesiredTrackId = "music-id", TrackId = TestTrackId, Status = IntentStatus.Pending };
            await planner.CreatePlanAsync(musicDesiredTrack);

            musicBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
            genericOnlyBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Never);

            // Act - Plan for GenericFile domain (only GenericFile backend is queried)
            var genericDesiredTrack = new DesiredTrack { Domain = ContentDomain.GenericFile, DesiredTrackId = "generic-id", TrackId = TestTrackId, Status = IntentStatus.Pending };
            await planner.CreatePlanAsync(genericDesiredTrack);

            musicBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
            genericOnlyBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void ApplyDomainRulesAndMode_EnforcesSoulseekGating()
        {
            // Arrange
            var planner = CreatePlanner();
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate { Backend = ContentBackendType.Soulseek, BackendRef = "slsk" },
                new SourceCandidate { Backend = ContentBackendType.MeshDht, BackendRef = "mesh" },
                new SourceCandidate { Backend = ContentBackendType.LocalLibrary, BackendRef = "local" }
            };

            // Act - Test Music domain (should allow Soulseek)
            var musicMethod = typeof(MultiSourcePlanner).GetMethod("ApplyDomainRulesAndMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var musicResult = musicMethod.Invoke(planner, new object[] { ContentDomain.Music, PlanningMode.SoulseekFriendly, candidates });

            // Act - Test GenericFile domain (should block Soulseek)
            var genericResult = musicMethod.Invoke(planner, new object[] { ContentDomain.GenericFile, PlanningMode.SoulseekFriendly, candidates });

            // Assert
            var musicFiltered = (musicResult as IEnumerable<SourceCandidate>).ToList();
            var genericFiltered = (genericResult as IEnumerable<SourceCandidate>).ToList();

            // Music domain should include Soulseek
            Assert.Contains(musicFiltered, c => c.Backend == ContentBackendType.Soulseek);

            // GenericFile domain should exclude Soulseek
            Assert.DoesNotContain(genericFiltered, c => c.Backend == ContentBackendType.Soulseek);

            // Both should include non-Soulseek backends
            Assert.Contains(musicFiltered, c => c.Backend == ContentBackendType.MeshDht);
            Assert.Contains(musicFiltered, c => c.Backend == ContentBackendType.LocalLibrary);
            Assert.Contains(genericFiltered, c => c.Backend == ContentBackendType.MeshDht);
            Assert.Contains(genericFiltered, c => c.Backend == ContentBackendType.LocalLibrary);
        }

        [Fact]
        public async Task CreatePlanAsync_PropagatesDomainFromDesiredTrack()
        {
            // Arrange
            var planner = CreatePlanner();

            var testTrack = new Track { TrackId = TestTrackId, ReleaseId = "r1", TrackNumber = 1, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            _catalogueStoreMock.Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(testTrack);
            _sourceRegistryMock.Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SourceCandidate>());
            _moderationProviderMock.Setup(x => x.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ModerationDecision.Allow("Test"));

            var desiredTrack = new DesiredTrack { Domain = ContentDomain.GenericFile, DesiredTrackId = "test-id", TrackId = TestTrackId, Status = IntentStatus.Pending };
            var plan = await planner.CreatePlanAsync(desiredTrack);

            // Assert - Plan created; domain from DesiredTrack was used (no Failed with "Domain validation failed"); success path does not set plan.DesiredTrack
            Assert.NotEqual(PlanStatus.Failed, plan.Status);
            Assert.Equal(desiredTrack.TrackId, plan.TrackId);
        }

        [Theory]
        [InlineData(ContentDomain.Music, true)]
        [InlineData(ContentDomain.GenericFile, false)]
        public async Task SoulseekBackendAvailability_RespectsDomainRestrictions(ContentDomain domain, bool shouldBeAvailable)
        {
            // Arrange
            var soulseekBackendMock = new Mock<IContentBackend>();
            soulseekBackendMock.Setup(x => x.Type).Returns(ContentBackendType.Soulseek);
            soulseekBackendMock.Setup(x => x.SupportedDomain).Returns(ContentDomain.Music); // Only Music
            soulseekBackendMock.Setup(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());

            _backends.Add(soulseekBackendMock.Object);
            var planner = CreatePlanner();

            var testTrack = new Track { TrackId = TestTrackId, ReleaseId = "r1", TrackNumber = 1, Title = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            _catalogueStoreMock.Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(testTrack);
            _sourceRegistryMock.Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SourceCandidate>());
            _moderationProviderMock.Setup(x => x.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ModerationDecision.Allow("Test"));

            var desiredTrack = new DesiredTrack { Domain = domain, DesiredTrackId = "test-id", TrackId = TestTrackId, Status = IntentStatus.Pending };

            // Act
            await planner.CreatePlanAsync(desiredTrack);

            // Assert
            if (shouldBeAvailable)
            {
                soulseekBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            else
            {
                soulseekBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }
    }
}


