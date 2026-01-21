// Intent Queue Processor Tests
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Processing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Processing;
    using slskd.VirtualSoulfind.v2.Resolution;
    using slskd.VirtualSoulfind.v2.Execution;
    using slskd.VirtualSoulfind.v2.Sources;
    using Xunit;

    public class IntentQueueProcessorTests
    {
        private readonly Mock<IIntentQueue> _mockIntentQueue;
        private readonly Mock<ICatalogueStore> _mockCatalogueStore;
        private readonly Mock<IPlanner> _mockPlanner;
        private readonly Mock<IResolver> _mockResolver;
        private readonly Mock<ILogger<IntentQueueProcessor>> _mockLogger;

        public IntentQueueProcessorTests()
        {
            _mockIntentQueue = new Mock<IIntentQueue>();
            _mockCatalogueStore = new Mock<ICatalogueStore>();
            _mockPlanner = new Mock<IPlanner>();
            _mockResolver = new Mock<IResolver>();
            _mockLogger = new Mock<ILogger<IntentQueueProcessor>>();
        }

        [Fact]
        public async Task ProcessBatch_WithNoPendingIntents_ReturnsZero()
        {
            // Arrange
            _mockIntentQueue
                .Setup(q => q.GetPendingTracksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DesiredTrack>());

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessBatchAsync(10);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task ProcessBatch_WithPendingIntents_ProcessesAll()
        {
            // Arrange
            var trackId = ContentItemId.NewId();
            var intent1 = new DesiredTrack
            {
                DesiredTrackId = "intent1",
                TrackId = trackId.ToString(),
                Status = IntentStatus.Pending,
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var intent2 = new DesiredTrack
            {
                DesiredTrackId = "intent2",
                TrackId = trackId.ToString(),
                Status = IntentStatus.Pending,
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetPendingTracksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DesiredTrack> { intent1, intent2 });

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => 
                    id == "intent1" ? intent1 : intent2);

            var track = new Track
            {
                TrackId = trackId.ToString(),
                Title = "Test Track",
            };

            _mockCatalogueStore
                .Setup(c => c.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            var plan = new TrackAcquisitionPlan
            {
                TrackId = trackId.ToString(),
                Steps = new List<PlanStep>
                {
                    new PlanStep { Backend = slskd.VirtualSoulfind.v2.Backends.ContentBackendType.LocalLibrary, Candidates = new List<SourceCandidate>() }
                },
            };

            _mockPlanner
                .Setup(p => p.CreatePlanAsync(It.IsAny<DesiredTrack>(), It.IsAny<PlanningMode?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(plan);

            _mockResolver
                .Setup(r => r.ExecutePlanAsync(It.IsAny<TrackAcquisitionPlan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlanExecutionState
                {
                    ExecutionId = "exec1",
                    TrackId = trackId.ToString(),
                    Status = PlanExecutionStatus.Succeeded,
                    CurrentStepIndex = 0,
                    TotalSteps = 1,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                });

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessBatchAsync(10);

            // Assert
            Assert.Equal(2, result);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync(It.IsAny<string>(), IntentStatus.Completed, It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ProcessIntent_WithSuccessfulExecution_MarksCompleted()
        {
            // Arrange
            var trackId = ContentItemId.NewId();
            var intent = new DesiredTrack
            {
                DesiredTrackId = "intent1",
                TrackId = trackId.ToString(),
                Status = IntentStatus.Pending,
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync("intent1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            var track = new Track
            {
                TrackId = trackId.ToString(),
                Title = "Test Track",
            };

            _mockCatalogueStore
                .Setup(c => c.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            var plan = new TrackAcquisitionPlan
            {
                TrackId = trackId.ToString(),
                Steps = new List<PlanStep> { new PlanStep { Backend = slskd.VirtualSoulfind.v2.Backends.ContentBackendType.LocalLibrary } },
            };

            _mockPlanner
                .Setup(p => p.CreatePlanAsync(It.IsAny<DesiredTrack>(), It.IsAny<PlanningMode?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(plan);

            _mockResolver
                .Setup(r => r.ExecutePlanAsync(plan, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlanExecutionState
                {
                    ExecutionId = "exec1",
                    TrackId = trackId.ToString(),
                    Status = PlanExecutionStatus.Succeeded,
                    CurrentStepIndex = 0,
                    TotalSteps = 1,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                });

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessIntentAsync("intent1");

            // Assert
            Assert.True(result);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync("intent1", IntentStatus.InProgress, It.IsAny<CancellationToken>()),
                Times.Once);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync("intent1", IntentStatus.Completed, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessIntent_WithFailedExecution_MarksFailed()
        {
            // Arrange
            var trackId = ContentItemId.NewId();
            var intent = new DesiredTrack
            {
                DesiredTrackId = "intent1",
                TrackId = trackId.ToString(),
                Status = IntentStatus.Pending,
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync("intent1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            var track = new Track { TrackId = trackId.ToString(), Title = "Test" };

            _mockCatalogueStore
                .Setup(c => c.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            var plan = new TrackAcquisitionPlan
            {
                TrackId = trackId.ToString(),
                Steps = new List<PlanStep> { new PlanStep { Backend = slskd.VirtualSoulfind.v2.Backends.ContentBackendType.LocalLibrary } },
            };

            _mockPlanner
                .Setup(p => p.CreatePlanAsync(It.IsAny<DesiredTrack>(), It.IsAny<PlanningMode?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(plan);

            _mockResolver
                .Setup(r => r.ExecutePlanAsync(plan, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlanExecutionState
                {
                    ExecutionId = "exec1",
                    TrackId = trackId.ToString(),
                    Status = PlanExecutionStatus.Failed,
                    CurrentStepIndex = 0,
                    TotalSteps = 1,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = "All candidates failed",
                });

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessIntentAsync("intent1");

            // Assert
            Assert.False(result);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync("intent1", IntentStatus.Failed, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessIntent_WithNonPendingStatus_SkipsProcessing()
        {
            // Arrange
            var intent = new DesiredTrack
            {
                DesiredTrackId = "intent1",
                TrackId = "track1",
                Status = IntentStatus.Completed, // Already completed
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync("intent1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessIntentAsync("intent1");

            // Assert
            Assert.False(result);
            _mockPlanner.Verify(
                p => p.CreatePlanAsync(It.IsAny<DesiredTrack>(), It.IsAny<PlanningMode?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessIntent_WithMissingTrack_MarksFailed()
        {
            // Arrange
            var trackId = ContentItemId.NewId();
            var intent = new DesiredTrack
            {
                DesiredTrackId = "intent1",
                TrackId = trackId.ToString(),
                Status = IntentStatus.Pending,
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync("intent1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            _mockCatalogueStore
                .Setup(c => c.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Track)null); // Track not found

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessIntentAsync("intent1");

            // Assert
            Assert.False(result);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync("intent1", IntentStatus.Failed, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetStats_ReturnsCorrectCounts()
        {
            // Arrange
            _mockIntentQueue
                .Setup(q => q.CountTracksByStatusAsync(IntentStatus.Pending, It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            _mockIntentQueue
                .Setup(q => q.CountTracksByStatusAsync(IntentStatus.InProgress, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var stats = await processor.GetStatsAsync();

            // Assert
            Assert.Equal(5, stats.PendingCount);
            Assert.Equal(2, stats.InProgressCount);
            Assert.Equal(0, stats.TotalProcessed);
            Assert.Equal(0, stats.SuccessCount);
            Assert.Equal(0, stats.FailureCount);
        }

        [Fact]
        public async Task ProcessIntent_WithNoPlan_MarksFailed()
        {
            // Arrange
            var trackId = ContentItemId.NewId();
            var intent = new DesiredTrack
            {
                DesiredTrackId = "intent1",
                TrackId = trackId.ToString(),
                Status = IntentStatus.Pending,
                Priority = IntentPriority.Normal,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _mockIntentQueue
                .Setup(q => q.GetTrackIntentAsync("intent1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(intent);

            var track = new Track { TrackId = trackId.ToString(), Title = "Test" };

            _mockCatalogueStore
                .Setup(c => c.FindTrackByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(track);

            _mockPlanner
                .Setup(p => p.CreatePlanAsync(It.IsAny<DesiredTrack>(), It.IsAny<PlanningMode?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackAcquisitionPlan)null); // No plan available

            var processor = new IntentQueueProcessor(
                _mockIntentQueue.Object,
                _mockCatalogueStore.Object,
                _mockPlanner.Object,
                _mockResolver.Object,
                _mockLogger.Object);

            // Act
            var result = await processor.ProcessIntentAsync("intent1");

            // Assert
            Assert.False(result);
            _mockIntentQueue.Verify(
                q => q.UpdateTrackStatusAsync("intent1", IntentStatus.Failed, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
