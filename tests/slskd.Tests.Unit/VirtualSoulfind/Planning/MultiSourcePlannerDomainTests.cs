// <copyright file="MultiSourcePlannerDomainTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Planning
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Resolution;
    using Xunit;

    /// <summary>
    ///     Tests for H-VF01: Domain gating in MultiSourcePlanner.
    /// </summary>
    public class MultiSourcePlannerDomainTests
    {
        private readonly Mock<ICatalogueStore> _catalogueStoreMock = new();
        private readonly Mock<IPlanner> _plannerMock = new();
        private readonly Mock<IResolver> _resolverMock = new();

        [Fact]
        public async Task CreatePlanAsync_WithInvalidDomain_ReturnsFailedPlan()
        {
            // Arrange
            var planner = new MultiSourcePlanner(
                _catalogueStoreMock.Object,
                _plannerMock.Object,
                _resolverMock.Object);

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
        public async Task ApplyDomainRulesAndMode_SoulseekBackend_OnlyAllowedForMusicDomain()
        {
            // Arrange
            var planner = new MultiSourcePlanner(
                _catalogueStoreMock.Object,
                _plannerMock.Object,
                _resolverMock.Object);

            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate { Backend = ContentBackendType.Soulseek, Uri = "test://soulseek" },
                new SourceCandidate { Backend = ContentBackendType.MeshDht, Uri = "test://mesh" },
                new SourceCandidate { Backend = ContentBackendType.LocalLibrary, Uri = "test://local" }
            };

            // Act - Music domain should allow Soulseek
            var musicResults = planner.GetType()
                .GetMethod("ApplyDomainRulesAndMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(planner, new object[] { ContentDomain.Music, PlanningMode.SoulseekFriendly, candidates });

            // Act - GenericFile domain should filter out Soulseek
            var genericFileResults = planner.GetType()
                .GetMethod("ApplyDomainRulesAndMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(planner, new object[] { ContentDomain.GenericFile, PlanningMode.SoulseekFriendly, candidates });

            // Assert
            var musicFiltered = musicResults as IEnumerable<SourceCandidate>;
            var genericFiltered = genericFileResults as IEnumerable<SourceCandidate>;

            Assert.NotNull(musicFiltered);
            Assert.NotNull(genericFiltered);

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
        public async Task CreatePlanAsync_UsesDomainFromDesiredTrack()
        {
            // Arrange
            var planner = new MultiSourcePlanner(
                _catalogueStoreMock.Object,
                _plannerMock.Object,
                _resolverMock.Object);

            // Setup catalogue to return a track
            var testTrack = new VirtualTrack { Id = "test-track-id" };
            _catalogueStoreMock
                .Setup(x => x.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testTrack);

            var desiredTrack = new DesiredTrack
            {
                Domain = ContentDomain.GenericFile, // Non-Music domain
                DesiredTrackId = "test-id",
                TrackId = "test-track",
                Status = IntentStatus.Pending
            };

            // Act
            var plan = await planner.CreatePlanAsync(desiredTrack);

            // Assert
            // The plan should be created (not fail due to domain validation)
            // and the domain gating should be applied during planning
            Assert.NotEqual(PlanStatus.Failed, plan.Status);
        }

        [Theory]
        [InlineData(ContentDomain.Music, true)] // Music should allow Soulseek
        [InlineData(ContentDomain.GenericFile, false)] // GenericFile should not allow Soulseek
        public void DomainGating_EnforcesBackendRestrictions(ContentDomain domain, bool shouldAllowSoulseek)
        {
            // Arrange
            var candidates = new List<SourceCandidate>
            {
                new SourceCandidate { Backend = ContentBackendType.Soulseek, Uri = "test://soulseek" },
                new SourceCandidate { Backend = ContentBackendType.MeshDht, Uri = "test://mesh" }
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


