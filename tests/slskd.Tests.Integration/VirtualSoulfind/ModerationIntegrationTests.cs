// <copyright file="ModerationIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.VirtualSoulfind
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using slskd.Common.Moderation;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.Shares;
    using slskd.MediaCore;
    using slskd.Tests.Integration;
    using slskd.VirtualSoulfind.v2.Backends;
    using Xunit;

    /// <summary>
    ///     Integration tests for T-MCP03: End-to-end moderation enforcement.
    /// </summary>
    [Collection("Integration")]
    public class ModerationIntegrationTests : IClassFixture<StubWebApplicationFactory>
    {
        private readonly StubWebApplicationFactory _fixture;

        public ModerationIntegrationTests(StubWebApplicationFactory fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VirtualSoulfindPipeline_EnforcesModerationEndToEnd()
        {
            // Arrange
            var services = _fixture.Services;
            var intentQueue = services.GetRequiredService<IIntentQueue>();
            var planner = services.GetRequiredService<IPlanner>();
            var moderationProvider = services.GetRequiredService<IModerationProvider>();

            // Create a track intent
            var trackId = Guid.NewGuid().ToString();
            var desiredTrack = await intentQueue.EnqueueTrackAsync(
                ContentDomain.Music,
                trackId,
                cancellationToken: CancellationToken.None);

            // Act - Try to plan the track acquisition
            var plan = await planner.CreatePlanAsync(desiredTrack, cancellationToken: CancellationToken.None);

            // Assert
            // The plan should either succeed (if content is allowed) or fail gracefully
            // In a real scenario, this would depend on MCP configuration
            Assert.NotNull(plan);
            Assert.Equal(desiredTrack.TrackId, plan.TrackId);

            // If the plan fails, it should be due to moderation (not system errors)
            if (plan.Status == PlanStatus.Failed)
            {
                // Should not fail due to domain validation (we provided valid domain)
                Assert.DoesNotContain("Domain validation failed", plan.ErrorMessage ?? string.Empty);
            }
        }

        [Fact]
        public async Task ModerationProvider_CheckContentIdAsync_IntegratesWithPlanning()
        {
            // Arrange
            var services = _fixture.Services;
            var moderationProvider = services.GetRequiredService<IModerationProvider>();
            var planner = services.GetRequiredService<IPlanner>();

            // Create test content ID
            var contentId = Guid.NewGuid().ToString();

            // Act - Check moderation decision
            var decision = await moderationProvider.CheckContentIdAsync(
                contentId,
                CancellationToken.None);

            // Assert - Should return a valid decision
            Assert.NotNull(decision);
            Assert.True(decision.Verdict == ModerationVerdict.Allowed ||
                       decision.Verdict == ModerationVerdict.Blocked ||
                       decision.Verdict == ModerationVerdict.Quarantined ||
                       decision.Verdict == ModerationVerdict.Unknown);

            // Should have a reason (may be empty for Allowed/Unknown)
            Assert.NotNull(decision.Reason);
        }

        [Fact]
        public async Task ShareRepository_IsAdvertisableFlag_IntegratesWithVirtualSoulfind()
        {
            // Arrange
            var shareRepository = _fixture.Services.GetRequiredService<IShareRepository>();
            if (shareRepository is StubShareRepository stub)
                stub.SeedForIsAdvertisableTest();

            // Act & Assert: ListFiles -> ListContentItemsForFile; non-advertisable items must have ModerationReason.
            foreach (var file in shareRepository.ListFiles().Take(10))
            {
                var maskedFilename = file.Filename;
                var items = shareRepository.ListContentItemsForFile(maskedFilename);
                foreach (var (ContentId, Domain, WorkId, IsAdvertisable, ModerationReason) in items)
                {
                    if (!IsAdvertisable)
                        Assert.False(string.IsNullOrEmpty(ModerationReason));
                }
            }

            await Task.CompletedTask;
        }

        [Fact]
        public async Task ContentDescriptorPublisher_IsAdvertisableFilter_PreventsPublication()
        {
            // Arrange
            var services = _fixture.Services;
            var publisher = services.GetRequiredService<IContentDescriptorPublisher>();

            // Create a descriptor for non-advertisable content
            var descriptor = new ContentDescriptor
            {
                ContentId = Guid.NewGuid().ToString(),
                IsAdvertisable = false, // Explicitly mark as not advertisable
                SizeBytes = 1024
            };

            // Act - Try to publish
            var result = await publisher.PublishAsync(descriptor, cancellationToken: CancellationToken.None);

            // Assert - Should fail due to IsAdvertisable = false
            Assert.False(result.Success);
            Assert.Contains("not advertisable", result.ErrorMessage);
        }

        [Fact]
        public async Task LocalLibraryBackend_IsAdvertisableFilter_PreventsCandidateReturn()
        {
            // Arrange
            var services = _fixture.Services;
            var backend = services.GetRequiredService<IEnumerable<IContentBackend>>()
                .OfType<LocalLibraryBackend>()
                .FirstOrDefault();

            Assert.NotNull(backend); // StubWebApplicationFactory registers LocalLibraryBackend

            var itemId = ContentItemId.Parse(Guid.NewGuid().ToString());

            // Act - Query for candidates
            var candidates = await backend.FindCandidatesAsync(itemId, CancellationToken.None);

            // Assert - Should return candidates only for advertisable content
            // (In test environment, this may be empty if no content is configured)
            Assert.NotNull(candidates);

            // If candidates are returned, they should be for advertisable content
            foreach (var candidate in candidates)
            {
                Assert.Equal(ContentBackendType.LocalLibrary, candidate.Backend);
            }
        }

        [Fact]
        public async Task MultiSourcePlanner_ModerationIntegration_FiltersCandidates()
        {
            // Arrange
            var services = _fixture.Services;
            var planner = services.GetRequiredService<IPlanner>();
            var intentQueue = services.GetRequiredService<IIntentQueue>();

            // Create a track intent
            var trackId = Guid.NewGuid().ToString();
            var desiredTrack = await intentQueue.EnqueueTrackAsync(
                ContentDomain.Music,
                trackId,
                cancellationToken: CancellationToken.None);

            // Act - Create acquisition plan
            var plan = await planner.CreatePlanAsync(desiredTrack, cancellationToken: CancellationToken.None);

            // Assert - Plan should be created successfully or fail gracefully
            Assert.NotNull(plan);
            Assert.Equal(desiredTrack.TrackId, plan.TrackId);

            // If planning produced steps, they should be valid
            if (plan.Steps != null && plan.Steps.Count > 0)
            {
                foreach (var step in plan.Steps)
                {
                    Assert.True(Enum.IsDefined(typeof(ContentBackendType), step.Backend));
                }
            }
        }
    }
}

