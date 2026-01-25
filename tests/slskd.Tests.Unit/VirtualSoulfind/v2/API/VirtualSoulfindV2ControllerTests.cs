// <copyright file="VirtualSoulfindV2ControllerTests.cs" company="slskd Team">
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

namespace slskd.Tests.Unit.VirtualSoulfind.v2.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Moq;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.API;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Execution;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Processing;
    using slskd.VirtualSoulfind.v2.Resolution;
    using Xunit;

    /// <summary>
    ///     Tests for <see cref="VirtualSoulfindV2Controller"/>.
    /// </summary>
    public class VirtualSoulfindV2ControllerTests
    {
        private readonly Mock<IIntentQueue> _mockIntentQueue;
        private readonly Mock<ICatalogueStore> _mockCatalogueStore;
        private readonly Mock<IPlanner> _mockPlanner;
        private readonly Mock<IResolver> _mockResolver;
        private readonly Mock<IIntentQueueProcessor> _mockProcessor;
        private readonly VirtualSoulfindV2Controller _controller;

        public VirtualSoulfindV2ControllerTests()
        {
            _mockIntentQueue = new Mock<IIntentQueue>();
            _mockCatalogueStore = new Mock<ICatalogueStore>();
            _mockPlanner = new Mock<IPlanner>();
            _mockResolver = new Mock<IResolver>();
            _mockProcessor = new Mock<IIntentQueueProcessor>();

            _controller = new VirtualSoulfindV2Controller(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockProcessor.Object);
        }

        #region Intent Endpoints

        [Fact]
        public async Task EnqueueTrack_ValidRequest_ReturnsCreated()
        {
            var trackId = ContentItemId.NewId().ToString();
            var request = new EnqueueTrackRequest
            {
                Domain = ContentDomain.Music,
                TrackId = trackId,
                Priority = IntentPriority.High,
            };

            var expectedIntent = new DesiredTrack
            {
                DesiredTrackId = Guid.NewGuid().ToString(),
                TrackId = trackId,
                Priority = IntentPriority.High,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.EnqueueTrackAsync(request.Domain, request.TrackId, request.Priority, request.ParentDesiredReleaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedIntent);

            var result = await _controller.EnqueueTrack(request, CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(_controller.GetTrackIntent), createdResult.ActionName);
            var returnedIntent = Assert.IsType<DesiredTrack>(createdResult.Value);
            Assert.Equal(expectedIntent.DesiredTrackId, returnedIntent.DesiredTrackId);
            Assert.Equal(expectedIntent.TrackId, returnedIntent.TrackId);
        }

        [Fact]
        public async Task EnqueueRelease_ValidRequest_ReturnsCreated()
        {
            // Arrange
            var releaseId = "release:test";
            var request = new EnqueueReleaseRequest
            {
                ReleaseId = releaseId,
                Priority = IntentPriority.Normal,
                Mode = IntentMode.Wanted,
            };

            var expectedIntent = new DesiredRelease
            {
                DesiredReleaseId = Guid.NewGuid().ToString(),
                ReleaseId = releaseId,
                Priority = IntentPriority.Normal,
                Mode = IntentMode.Wanted,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.EnqueueReleaseAsync(releaseId, IntentPriority.Normal, IntentMode.Wanted, request.Notes, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedIntent);

            // Act
            var result = await _controller.EnqueueRelease(request, CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(_controller.GetReleaseIntent), createdResult.ActionName);
            var returnedIntent = Assert.IsType<DesiredRelease>(createdResult.Value);
            Assert.Equal(expectedIntent.DesiredReleaseId, returnedIntent.DesiredReleaseId);
        }

        [Fact]
        public async Task GetTrackIntent_ExistingIntent_ReturnsOk()
        {
            // Arrange
            var intentId = Guid.NewGuid().ToString();
            var trackId = ContentItemId.NewId().ToString();
            var intent = new DesiredTrack
            {
                DesiredTrackId = intentId,
                TrackId = trackId,
                Priority = IntentPriority.High,
                Status = IntentStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            // Act
            var result = await _controller.GetTrackIntent(intentId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedIntent = Assert.IsType<DesiredTrack>(okResult.Value);
            Assert.Equal(intentId, returnedIntent.DesiredTrackId);
        }

        [Fact]
        public async Task GetTrackIntent_NonExistingIntent_ReturnsNotFound()
        {
            // Arrange
            var intentId = Guid.NewGuid().ToString();

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DesiredTrack)null);

            // Act
            var result = await _controller.GetTrackIntent(intentId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetPendingTracks_ReturnsIntents()
        {
            // Arrange
            var intents = new List<DesiredTrack>
            {
                new DesiredTrack
                {
                    DesiredTrackId = Guid.NewGuid().ToString(),
                    TrackId = ContentItemId.NewId().ToString(),
                    Status = IntentStatus.Pending,
                    Priority = IntentPriority.High,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
                new DesiredTrack
                {
                    DesiredTrackId = Guid.NewGuid().ToString(),
                    TrackId = ContentItemId.NewId().ToString(),
                    Status = IntentStatus.Pending,
                    Priority = IntentPriority.Normal,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
            };

            _mockIntentQueue
                .Setup(q => q.GetPendingTracksAsync(50, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intents);

            // Act
            var result = await _controller.GetPendingTracks(50, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedIntents = Assert.IsAssignableFrom<IEnumerable<DesiredTrack>>(okResult.Value);
            Assert.Equal(2, returnedIntents.Count());
        }

        [Fact]
        public async Task UpdateTrackIntent_ValidUpdate_ReturnsNoContent()
        {
            // Arrange
            var intentId = Guid.NewGuid().ToString();
            var request = new UpdateIntentRequest
            {
                Status = IntentStatus.Completed,
            };

            var existingIntent = new DesiredTrack
            {
                DesiredTrackId = intentId,
                TrackId = ContentItemId.NewId().ToString(),
                Priority = IntentPriority.High,
                Status = IntentStatus.InProgress,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingIntent);

            _mockIntentQueue
                .Setup(q => q.UpdateTrackStatusAsync(intentId, IntentStatus.Completed, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateTrackIntent(intentId, request, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync(intentId, IntentStatus.Completed, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateTrackIntent_NonExistingIntent_ReturnsNotFound()
        {
            // Arrange
            var intentId = Guid.NewGuid().ToString();
            var request = new UpdateIntentRequest
            {
                Status = IntentStatus.Completed,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DesiredTrack)null);

            // Act
            var result = await _controller.UpdateTrackIntent(intentId, request, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        #endregion

        #region Catalogue Endpoints

        [Fact]
        public async Task GetArtist_ExistingArtist_ReturnsOk()
        {
            // Arrange
            var artistId = "artist:test";
            var artist = new Artist
            {
                ArtistId = artistId,
                Name = "Test Artist",
                SortName = "Artist, Test",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockCatalogueStore
                .Setup(s => s.FindArtistByIdAsync(artistId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(artist);

            // Act
            var result = await _controller.GetArtist(artistId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedArtist = Assert.IsType<Artist>(okResult.Value);
            Assert.Equal(artistId, returnedArtist.ArtistId);
            Assert.Equal("Test Artist", returnedArtist.Name);
        }

        [Fact]
        public async Task GetArtist_NonExistingArtist_ReturnsNotFound()
        {
            // Arrange
            var artistId = "artist:nonexistent";

            _mockCatalogueStore
                .Setup(s => s.FindArtistByIdAsync(artistId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Artist)null);

            // Act
            var result = await _controller.GetArtist(artistId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task SearchArtists_WithQuery_ReturnsResults()
        {
            // Arrange
            var query = "Pink";
            var artists = new List<Artist>
            {
                new Artist { ArtistId = "artist:pink-floyd", Name = "Pink Floyd", SortName = "Pink Floyd", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new Artist { ArtistId = "artist:pink", Name = "Pink", SortName = "Pink", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            };

            _mockCatalogueStore
                .Setup(s => s.SearchArtistsAsync(query, 50, It.IsAny<CancellationToken>()))
                .ReturnsAsync(artists);

            // Act
            var result = await _controller.SearchArtists(query, 50, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedArtists = Assert.IsAssignableFrom<IEnumerable<Artist>>(okResult.Value);
            Assert.Equal(2, returnedArtists.Count());
        }

        [Fact]
        public async Task GetArtistReleases_ReturnsReleaseGroups()
        {
            // Arrange
            var artistId = "artist:test";
            var releaseGroups = new List<ReleaseGroup>
            {
                new ReleaseGroup
                {
                    ReleaseGroupId = "rg:1",
                    ArtistId = artistId,
                    Title = "Album 1",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
            };

            _mockCatalogueStore
                .Setup(s => s.ListReleaseGroupsForArtistAsync(artistId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(releaseGroups);

            // Act
            var result = await _controller.GetArtistReleases(artistId, 100, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedReleaseGroups = Assert.IsAssignableFrom<IEnumerable<ReleaseGroup>>(okResult.Value);
            Assert.Single(returnedReleaseGroups);
        }

        [Fact]
        public async Task GetReleaseTracks_ReturnsTracks()
        {
            // Arrange
            var releaseId = "release:test";
            var tracks = new List<Track>
            {
                new Track
                {
                    TrackId = ContentItemId.NewId().ToString(),
                    ReleaseId = releaseId,
                    TrackNumber = 1,
                    Title = "Track 1",
                    DurationSeconds = 240,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
            };

            _mockCatalogueStore
                .Setup(s => s.ListTracksForReleaseAsync(releaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tracks);

            // Act
            var result = await _controller.GetReleaseTracks(releaseId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedTracks = Assert.IsAssignableFrom<IEnumerable<Track>>(okResult.Value);
            Assert.Single(returnedTracks);
        }

        #endregion

        #region Planning & Execution Endpoints

        [Fact]
        public async Task CreatePlan_ValidRequest_ReturnsOk()
        {
            // Arrange
            var trackId = ContentItemId.NewId().ToString();
            var request = new CreatePlanRequest
            {
                TrackId = trackId,
                Mode = PlanningMode.SoulseekFriendly,
            };

            var plan = new TrackAcquisitionPlan
            {
                TrackId = trackId,
                Mode = PlanningMode.SoulseekFriendly,
                Steps = new List<PlanStep>(),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _mockPlanner
                .Setup(p => p.CreatePlanAsync(It.IsAny<DesiredTrack>(), PlanningMode.SoulseekFriendly, It.IsAny<CancellationToken>()))
                .ReturnsAsync(plan);

            // Act
            var result = await _controller.CreatePlan(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedPlan = Assert.IsType<TrackAcquisitionPlan>(okResult.Value);
            Assert.Equal(trackId, returnedPlan.TrackId);
        }

        [Fact]
        public async Task GetExecutionStatus_ExistingExecution_ReturnsOk()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString();
            var state = new PlanExecutionState
            {
                ExecutionId = executionId,
                TrackId = ContentItemId.NewId().ToString(),
                Status = PlanExecutionStatus.Succeeded,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            };

            _mockResolver
                .Setup(r => r.GetExecutionStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(state);

            // Act
            var result = await _controller.GetExecutionStatus(executionId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedState = Assert.IsType<PlanExecutionState>(okResult.Value);
            Assert.Equal(executionId, returnedState.ExecutionId);
        }

        [Fact]
        public async Task GetExecutionStatus_NonExistingExecution_ReturnsNotFound()
        {
            // Arrange
            var executionId = Guid.NewGuid().ToString();

            _mockResolver
                .Setup(r => r.GetExecutionStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlanExecutionState)null);

            // Act
            var result = await _controller.GetExecutionStatus(executionId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ProcessIntent_ExistingIntent_ReturnsAccepted()
        {
            // Arrange
            var intentId = Guid.NewGuid().ToString();
            var intent = new DesiredTrack
            {
                DesiredTrackId = intentId,
                TrackId = ContentItemId.NewId().ToString(),
                Priority = IntentPriority.High,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            _mockProcessor
                .Setup(p => p.ProcessIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            // Act
            var result = await _controller.ProcessIntent(intentId, CancellationToken.None);

            // Assert
            var acceptedResult = Assert.IsType<AcceptedResult>(result);
            Assert.NotNull(acceptedResult.Value);
        }

        [Fact]
        public async Task ProcessIntent_NonExistingIntent_ReturnsNotFound()
        {
            // Arrange
            var intentId = Guid.NewGuid().ToString();

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(intentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DesiredTrack)null);

            // Act
            var result = await _controller.ProcessIntent(intentId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetStats_ReturnsProcessorStats()
        {
            // Arrange
            var stats = new IntentProcessorStats
            {
                TotalProcessed = 100,
                SuccessCount = 85,
                FailureCount = 10,
                InProgressCount = 3,
                PendingCount = 12,
            };

            _mockProcessor
                .Setup(p => p.GetStatsAsync())
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedStats = Assert.IsType<IntentProcessorStats>(okResult.Value);
            Assert.Equal(100, returnedStats.TotalProcessed);
            Assert.Equal(85, returnedStats.SuccessCount);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_NullIntentQueue_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new VirtualSoulfindV2Controller(
                    null,
                    _mockCatalogueStore.Object,
                    _mockPlanner.Object,
                    _mockResolver.Object,
                    _mockProcessor.Object));
        }

        [Fact]
        public void Constructor_NullCatalogueStore_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new VirtualSoulfindV2Controller(
                    _mockIntentQueue.Object,
                    null,
                    _mockPlanner.Object,
                    _mockResolver.Object,
                    _mockProcessor.Object));
        }

        [Fact]
        public void Constructor_NullPlanner_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new VirtualSoulfindV2Controller(
                    _mockIntentQueue.Object,
                    _mockCatalogueStore.Object,
                    null,
                    _mockResolver.Object,
                    _mockProcessor.Object));
        }

        [Fact]
        public void Constructor_NullResolver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new VirtualSoulfindV2Controller(
                    _mockIntentQueue.Object,
                    _mockCatalogueStore.Object,
                    _mockPlanner.Object,
                    null,
                    _mockProcessor.Object));
        }

        [Fact]
        public void Constructor_NullProcessor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new VirtualSoulfindV2Controller(
                    _mockIntentQueue.Object,
                    _mockCatalogueStore.Object,
                    _mockPlanner.Object,
                    _mockResolver.Object,
                    null));
        }

        #endregion
    }
}
