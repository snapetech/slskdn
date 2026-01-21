// <copyright file="FederationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-FED03: FederationService.
    /// </summary>
    public class FederationServiceTests
    {
        private readonly Mock<IOptionsMonitor<SocialFederationOptions>> _federationOptionsMock = new();
        private readonly Mock<IOptionsMonitor<FederationPublishingOptions>> _publishingOptionsMock = new();
        private readonly Mock<LibraryActorService> _libraryActorServiceMock = new();
        private readonly Mock<IActivityPubKeyStore> _keyStoreMock = new();
        private readonly Mock<ActivityDeliveryService> _deliveryServiceMock = new();
        private readonly Mock<ILogger<FederationService>> _loggerMock = new();

        public FederationServiceTests()
        {
            // Setup default options
            _federationOptionsMock.Setup(x => x.CurrentValue).Returns(new SocialFederationOptions
            {
                Enabled = true,
                Mode = "Public",
                BaseUrl = "https://example.com"
            });

            _publishingOptionsMock.Setup(x => x.CurrentValue).Returns(new FederationPublishingOptions
            {
                Enabled = true,
                PublishableDomains = new[] { "music", "books" }
            });
        }

        private FederationService CreateService()
        {
            return new FederationService(
                _federationOptionsMock.Object,
                _publishingOptionsMock.Object,
                _libraryActorServiceMock.Object,
                _keyStoreMock.Object,
                _deliveryServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task PublishWorkRefAsync_WithValidWorkRef_PublishesToFederation()
        {
            // Arrange
            var service = CreateService();
            var workRef = new WorkRef
            {
                Id = "work:music:123",
                Domain = "music",
                Title = "Test Song",
                Creator = "Test Artist"
            };

            var musicActor = new Mock<LibraryActor>();
            musicActor.Setup(x => x.ActorId).Returns("https://example.com/actors/music");
            musicActor.Setup(x => x.IsAvailable).Returns(true);

            _libraryActorServiceMock.Setup(x => x.GetActor("music")).Returns(musicActor.Object);

            // Act
            await service.PublishWorkRefAsync(workRef);

            // Assert
            _deliveryServiceMock.Verify(x => x.DeliverActivityAsync(
                It.Is<ActivityPubActivity>(a => a.Type == "Create" && a.Object == workRef),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishWorkRefAsync_WithUnpublishableDomain_SkipsPublishing()
        {
            // Arrange
            var service = CreateService();
            var workRef = new WorkRef
            {
                Id = "work:unknown:123",
                Domain = "unknown", // Not in publishable domains
                Title = "Test Content"
            };

            // Act
            await service.PublishWorkRefAsync(workRef);

            // Assert - Should not call delivery service
            _deliveryServiceMock.Verify(x => x.DeliverActivityAsync(
                It.IsAny<ActivityPubActivity>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PublishWorkRefAsync_InHermitMode_SkipsPublishing()
        {
            // Arrange
            _federationOptionsMock.Setup(x => x.CurrentValue).Returns(new SocialFederationOptions
            {
                Enabled = true,
                Mode = "Hermit" // Hermit mode
            });

            var service = CreateService();
            var workRef = new WorkRef
            {
                Id = "work:music:123",
                Domain = "music",
                Title = "Test Song"
            };

            // Act
            await service.PublishWorkRefAsync(workRef);

            // Assert - Should not call delivery service
            _deliveryServiceMock.Verify(x => x.DeliverActivityAsync(
                It.IsAny<ActivityPubActivity>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PublishWorkRefAsync_WithInsecureWorkRef_SkipsPublishing()
        {
            // Arrange
            var service = CreateService();
            var workRef = new WorkRef
            {
                Id = "work:music:123",
                Domain = "music",
                Title = "Song with /path/injection", // Insecure content
                Creator = "Safe Artist"
            };

            // Act
            await service.PublishWorkRefAsync(workRef);

            // Assert - Should not call delivery service due to security validation
            _deliveryServiceMock.Verify(x => x.DeliverActivityAsync(
                It.IsAny<ActivityPubActivity>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PublishListAsync_WithPrivateVisibility_SkipsPublishing()
        {
            // Arrange
            var service = CreateService();
            var workRefs = new[]
            {
                new WorkRef { Id = "work:music:1", Domain = "music", Title = "Song 1" }
            };

            // Act
            await service.PublishListAsync("list123", "My List", "private", workRefs);

            // Assert - Should not call delivery service
            _deliveryServiceMock.Verify(x => x.DeliverActivityAsync(
                It.IsAny<ActivityPubActivity>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PublishListAsync_WithPublicVisibility_PublishesToFederation()
        {
            // Arrange
            var service = CreateService();
            var workRefs = new[]
            {
                new WorkRef { Id = "work:music:1", Domain = "music", Title = "Song 1" }
            };

            // Act
            await service.PublishListAsync("list123", "My List", "public", workRefs);

            // Assert
            _deliveryServiceMock.Verify(x => x.DeliverActivityAsync(
                It.Is<ActivityPubActivity>(a => a.Type == "Announce"),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CanPublishContent_WithValidParameters_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.CanPublishContent("music", true);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanPublishContent_WithUnpublishableDomain_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.CanPublishContent("unknown", true);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanPublishContent_WithNonAdvertisableContent_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.CanPublishContent("music", false); // Not advertisable

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanPublishContent_InHermitMode_ReturnsFalse()
        {
            // Arrange
            _federationOptionsMock.Setup(x => x.CurrentValue).Returns(new SocialFederationOptions
            {
                Enabled = true,
                Mode = "Hermit"
            });

            var service = CreateService();

            // Act
            var result = service.CanPublishContent("music", true);

            // Assert
            Assert.False(result);
        }
    }
}


