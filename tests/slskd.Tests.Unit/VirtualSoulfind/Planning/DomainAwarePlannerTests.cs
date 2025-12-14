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
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    /// <summary>
    ///     Tests for T-VC04: Domain-Aware Planner + Soulseek Gating.
    /// </summary>
    public class DomainAwarePlannerTests
    {
        private readonly Mock<ICatalogueStore> _catalogueStoreMock = new();
        private readonly Mock<ISourceRegistry> _sourceRegistryMock = new();
        private readonly List<IContentBackend> _backends = new();
        private readonly Mock<IModerationProvider> _moderationProviderMock = new();
        private readonly Mock<PeerReputationService> _peerReputationServiceMock = new();

        private MultiSourcePlanner CreatePlanner()
        {
            return new MultiSourcePlanner(
                _catalogueStoreMock.Object,
                _sourceRegistryMock.Object,
                _backends,
                _moderationProviderMock.Object,
                _peerReputationServiceMock.Object);
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
                TrackId = "test-track",
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
            // Arrange
            var musicBackendMock = new Mock<IContentBackend>();
            musicBackendMock.Setup(x => x.Type).Returns(ContentBackendType.Soulseek);
            musicBackendMock.Setup(x => x.SupportedDomain).Returns(ContentDomain.Music);
            musicBackendMock.Setup(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());

            var allDomainsBackendMock = new Mock<IContentBackend>();
            allDomainsBackendMock.Setup(x => x.Type).Returns(ContentBackendType.MeshDht);
            allDomainsBackendMock.Setup(x => x.SupportedDomain).Returns((ContentDomain?)null); // Supports all
            allDomainsBackendMock.Setup(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());

            var genericOnlyBackendMock = new Mock<IContentBackend>();
            genericOnlyBackendMock.Setup(x => x.Type).Returns(ContentBackendType.LocalLibrary);
            genericOnlyBackendMock.Setup(x => x.SupportedDomain).Returns(ContentDomain.GenericFile);
            genericOnlyBackendMock.Setup(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());

            _backends.AddRange(new[] { musicBackendMock.Object, allDomainsBackendMock.Object, genericOnlyBackendMock.Object });

            var planner = CreatePlanner();

            // Setup catalogue and other mocks
            var testTrack = new VirtualTrack { Id = "test-track-id" };
            _catalogueStoreMock
                .Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testTrack);
            _sourceRegistryMock
                .Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());
            _moderationProviderMock
                .Setup(x => x.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModerationDecision(ModerationVerdict.Allowed, "Test"));

            // Act - Plan for Music domain
            var musicDesiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.Music,
                DesiredTrackId = "music-id",
                TrackId = "test-track",
                Status = IntentStatus.Pending
            };
            var musicPlan = await planner.CreatePlanAsync(musicDesiredTrack);

            // Assert - Should query Music-only and all-domains backends, but not GenericFile-only
            musicBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
            allDomainsBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
            genericOnlyBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Never);

            // Act - Plan for GenericFile domain
            var genericDesiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.GenericFile,
                DesiredTrackId = "generic-id",
                TrackId = "test-track",
                Status = IntentStatus.Pending
            };
            var genericPlan = await planner.CreatePlanAsync(genericDesiredTrack);

            // Assert - Should query all-domains and GenericFile-only backends, but not Music-only
            musicBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once); // Still once
            allDomainsBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Exactly(2)); // Called again
            genericOnlyBackendMock.Verify(x => x.FindCandidatesAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void ApplyDomainRulesAndMode_EnforcesSoulseekGating()
        {
            // Arrange
            var planner = CreatePlanner();
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate { Backend = ContentBackendType.Soulseek, Uri = "test://soulseek" },
                new SourceCandidate { Backend = ContentBackendType.MeshDht, Uri = "test://mesh" },
                new SourceCandidate { Backend = ContentBackendType.LocalLibrary, Uri = "test://local" }
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

            // Setup mocks
            var testTrack = new VirtualTrack { Id = "test-track-id" };
            _catalogueStoreMock
                .Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testTrack);
            _sourceRegistryMock
                .Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());
            _moderationProviderMock
                .Setup(x => x.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModerationDecision(ModerationVerdict.Allowed, "Test"));

            // Act - Test with GenericFile domain
            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.GenericFile,
                DesiredTrackId = "test-id",
                TrackId = "test-track",
                Status = IntentStatus.Pending
            };
            var plan = await planner.CreatePlanAsync(desiredTrack);

            // Assert - Plan should be created (domain validation passes)
            Assert.NotEqual(PlanStatus.Failed, plan.Status);
            Assert.Equal(desiredTrack.Domain, plan.DesiredTrack.Domain);
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

            // Setup mocks
            var testTrack = new VirtualTrack { Id = "test-track-id" };
            _catalogueStoreMock
                .Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testTrack);
            _sourceRegistryMock
                .Setup(x => x.FindCandidatesForItemAsync(It.IsAny<ContentItemId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SourceCandidate>());
            _moderationProviderMock
                .Setup(x => x.CheckContentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModerationDecision(ModerationVerdict.Allowed, "Test"));

            var desiredTrack = new DesiredTrack
            {
                Domain = domain,
                DesiredTrackId = "test-id",
                TrackId = "test-track",
                Status = IntentStatus.Pending
            };

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

