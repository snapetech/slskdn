// <copyright file="LibraryActorServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation
{
    using System;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-FED02: LibraryActorService.
    /// </summary>
    public class LibraryActorServiceTests
    {
        private readonly Mock<IOptionsMonitor<SocialFederationOptions>> _federationOptionsMock = new();
        private readonly Mock<IActivityPubKeyStore> _keyStoreMock = new();
        private readonly Mock<ILogger<LibraryActorService>> _loggerMock = new();
        private readonly Mock<MusicLibraryActor> _musicActorMock = new();

        public LibraryActorServiceTests()
        {
            // Setup default options
            _federationOptionsMock.Setup(x => x.CurrentValue).Returns(new SocialFederationOptions
            {
                Enabled = true,
                Mode = "Public"
            });

            // Setup music actor
            _musicActorMock.Setup(x => x.ActorName).Returns("music");
            _musicActorMock.Setup(x => x.ActorId).Returns("https://example.com/actors/music");
            _musicActorMock.Setup(x => x.IsAvailable).Returns(true);
        }

        private LibraryActorService CreateService()
        {
            return new LibraryActorService(
                _federationOptionsMock.Object,
                _keyStoreMock.Object,
                _musicActorMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public void GetActor_ReturnsMusicActor_WhenAvailable()
        {
            // Arrange
            var service = CreateService();

            // Act
            var actor = service.GetActor("music");

            // Assert
            Assert.NotNull(actor);
            Assert.Equal("music", actor.ActorName);
        }

        [Fact]
        public void GetActor_ReturnsGenericActor_ForOtherDomains()
        {
            // Arrange
            var service = CreateService();

            // Act
            var booksActor = service.GetActor("books");
            var moviesActor = service.GetActor("movies");

            // Assert
            Assert.NotNull(booksActor);
            Assert.Equal("books", booksActor.ActorName);
            Assert.NotNull(moviesActor);
            Assert.Equal("movies", moviesActor.ActorName);
        }

        [Fact]
        public void GetActor_ReturnsNull_ForUnknownActor()
        {
            // Arrange
            var service = CreateService();

            // Act
            var actor = service.GetActor("unknown");

            // Assert
            Assert.Null(actor);
        }

        [Fact]
        public void GetAvailableDomains_IncludesMusicAndGenericDomains()
        {
            // Arrange
            var service = CreateService();

            // Act
            var domains = service.GetAvailableDomains();

            // Assert
            Assert.Contains("music", domains);
            Assert.Contains("books", domains);
            Assert.Contains("movies", domains);
            Assert.Contains("tv", domains);
            Assert.Contains("software", domains);
            Assert.Contains("games", domains);
        }

        [Fact]
        public void AvailableActors_FiltersByAvailability()
        {
            // Arrange
            _federationOptionsMock.Setup(x => x.CurrentValue).Returns(new SocialFederationOptions
            {
                Enabled = true,
                Mode = "Hermit" // Hermit mode makes actors unavailable
            });

            var service = CreateService();

            // Act
            var availableActors = service.AvailableActors;

            // Assert - Should be empty in hermit mode
            Assert.Empty(availableActors);
        }

        [Fact]
        public void IsLibraryActor_ReturnsTrue_ForValidActors()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.True(service.IsLibraryActor("music"));
            Assert.True(service.IsLibraryActor("books"));
            Assert.True(service.IsLibraryActor("movies"));
            Assert.False(service.IsLibraryActor("unknown"));
        }

        [Fact]
        public void Constructor_HandlesNullMusicActor()
        {
            // Arrange & Act
            var service = new LibraryActorService(
                _federationOptionsMock.Object,
                _keyStoreMock.Object,
                musicActor: null, // No music actor
                _loggerMock.Object);

            // Assert
            Assert.NotNull(service);
            var availableActors = service.AvailableActors;
            Assert.DoesNotContain("music", availableActors.Keys);
            Assert.Contains("books", availableActors.Keys); // Generic actors still available
        }
    }
}

