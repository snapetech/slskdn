// <copyright file="PodCoreServiceSanitizationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Identity;
using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;
using slskd.PodCore;
using Xunit;

public class PodCoreServiceSanitizationTests
{
    [Fact]
    public async Task PodMembershipVerifier_WhenMembershipLookupThrows_ReturnsSanitizedErrorMessage()
    {
        var membershipService = new Mock<IPodMembershipService>();
        membershipService
            .Setup(service => service.VerifyMembershipAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive membership detail"));

        var verifier = new PodMembershipVerifier(
            NullLogger<PodMembershipVerifier>.Instance,
            membershipService.Object,
            Mock.Of<IMessageSigner>());

        var result = await verifier.VerifyMembershipAsync("pod-1", "peer-1", CancellationToken.None);

        Assert.False(result.IsValidMember);
        Assert.Equal("Membership verification failed", result.ErrorMessage);
        Assert.DoesNotContain("sensitive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PodDiscoveryService_WhenDiscoveryThrows_ReturnsSanitizedErrorMessage()
    {
        var dhtClient = new Mock<IMeshDhtClient>();
        dhtClient
            .Setup(client => client.GetAsync<List<string>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive discovery detail"));

        var service = new PodDiscoveryService(
            NullLogger<PodDiscoveryService>.Instance,
            dhtClient.Object,
            Mock.Of<IPodDhtPublisher>(),
            Mock.Of<IPodService>());

        var result = await service.DiscoverPodsByNameAsync("demo", CancellationToken.None);

        Assert.Equal("Failed to discover pods by name", result.ErrorMessage);
        Assert.DoesNotContain("sensitive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PodMessageBackfill_WhenSyncThrows_ReturnsSanitizedErrorMessage()
    {
        var messageStorage = new Mock<IPodMessageStorage>();
        messageStorage
            .Setup(storage => storage.GetMessagesAsync("pod-1", "general", null, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive storage detail"));

        var podService = new Mock<IPodService>();
        var profileService = new Mock<IProfileService>();
        var messageRouter = new Mock<IPodMessageRouter>();
        var overlayClient = new Mock<IOverlayClient>();

        var service = new PodMessageBackfill(
            messageStorage.Object,
            messageRouter.Object,
            overlayClient.Object,
            podService.Object,
            profileService.Object,
            NullLogger<PodMessageBackfill>.Instance);

        var result = await service.SyncOnRejoinAsync(
            "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            new Dictionary<string, long> { ["general"] = 1 },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Backfill sync failed", result.ErrorMessage);
        Assert.DoesNotContain("sensitive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
