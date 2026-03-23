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

    [Fact]
    public async Task PodMessageBackfill_WithBlankPeerId_ReturnsStableFailure()
    {
        var messageStorage = new Mock<IPodMessageStorage>();
        messageStorage
            .Setup(storage => storage.GetMessagesAsync("pod-1", "general", null, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PodMessage { PodId = "pod-1", ChannelId = "general", TimestampUnixMs = 10 } });

        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetChannelsAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodChannel> { new() { ChannelId = "general", Name = "general" } });
        podService
            .Setup(service => service.GetMembersAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new() { PeerId = "   " } });

        var profileService = new Mock<IProfileService>();
        profileService
            .Setup(service => service.GetMyProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerProfile { PeerId = "peer-self" });

        var service = new PodMessageBackfill(
            messageStorage.Object,
            Mock.Of<IPodMessageRouter>(),
            Mock.Of<IOverlayClient>(),
            podService.Object,
            profileService.Object,
            NullLogger<PodMessageBackfill>.Instance);

        var result = await service.SyncOnRejoinAsync(
            "pod-1",
            new Dictionary<string, long> { ["general"] = 0 },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Backfill request target peer is invalid.", result.ErrorMessage);
    }

    [Fact]
    public async Task PodMessageBackfill_WhenAllPeerRequestsFail_ReturnsFailure()
    {
        var messageStorage = new Mock<IPodMessageStorage>();
        messageStorage
            .Setup(storage => storage.GetMessagesAsync("pod-1", "general", null, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PodMessage { PodId = "pod-1", ChannelId = "general", TimestampUnixMs = 10 } });

        var podService = new Mock<IPodService>();
        podService
            .Setup(service => service.GetMembersAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new() { PeerId = "peer-remote" } });

        var profileService = new Mock<IProfileService>();
        profileService
            .Setup(service => service.GetMyProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerProfile { PeerId = "peer-self" });

        var messageRouter = new Mock<IPodMessageRouter>();
        messageRouter
            .Setup(router => router.RouteMessageToPeersAsync(It.IsAny<PodMessage>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodMessageRoutingResult(false, "msg-1", "pod-1", 1, 0, 1, TimeSpan.Zero, "Backfill request delivery failed."));

        var service = new PodMessageBackfill(
            messageStorage.Object,
            messageRouter.Object,
            Mock.Of<IOverlayClient>(),
            podService.Object,
            profileService.Object,
            NullLogger<PodMessageBackfill>.Instance);

        var result = await service.SyncOnRejoinAsync(
            "pod-1",
            new Dictionary<string, long> { ["general"] = 0 },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Backfill request delivery failed.", result.ErrorMessage);
    }

    [Fact]
    public async Task PodMessageBackfill_ProcessBackfillResponse_NormalizesAndStoresTrimmedMessages()
    {
        var messageStorage = new Mock<IPodMessageStorage>();
        messageStorage
            .Setup(storage => storage.StoreMessageAsync("pod-1", "general", It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new PodMessageBackfill(
            messageStorage.Object,
            Mock.Of<IPodMessageRouter>(),
            Mock.Of<IOverlayClient>(),
            Mock.Of<IPodService>(),
            Mock.Of<IProfileService>(),
            NullLogger<PodMessageBackfill>.Instance);

        var response = new PodBackfillResponse(
            " pod-1 ",
            "peer-remote",
            new Dictionary<string, IReadOnlyList<PodMessage>>
            {
                [" general "] = new[]
                {
                    new PodMessage
                    {
                        PodId = " pod-1 ",
                        ChannelId = " general ",
                        SenderPeerId = " peer-remote ",
                        MessageId = " msg-1 ",
                        Body = "hello",
                        TimestampUnixMs = 100,
                    }
                }
            },
            false,
            DateTimeOffset.UtcNow);

        var result = await service.ProcessBackfillResponseAsync(" pod-1 ", " peer-remote ", response, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.MessagesStored);
        messageStorage.Verify(storage => storage.StoreMessageAsync(
            "pod-1",
            "general",
            It.Is<PodMessage>(message =>
                message.PodId == "pod-1" &&
                message.ChannelId == "general" &&
                message.SenderPeerId == "peer-remote" &&
                message.MessageId == "msg-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PodDhtPublisher_WhenPublishThrows_ReturnsSanitizedErrorMessage()
    {
        var dhtClient = new Mock<IMeshDhtClient>();
        dhtClient
            .Setup(client => client.PutAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive dht detail"));

        var signer = new Mock<IControlSigner>();
        signer.Setup(service => service.Sign(It.IsAny<ControlEnvelope>())).Returns<ControlEnvelope>(envelope => envelope);
        var publisher = new PodDhtPublisher(
            NullLogger<PodDhtPublisher>.Instance,
            dhtClient.Object,
            signer.Object,
            Mock.Of<IPodService>());

        var result = await publisher.PublishAsync(new Pod { PodId = "pod-1" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed to publish pod", result.ErrorMessage);
        Assert.DoesNotContain("sensitive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
