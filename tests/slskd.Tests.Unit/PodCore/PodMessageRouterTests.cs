// <copyright file="PodMessageRouterTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Overlay;
using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

/// <summary>
/// Unit tests for PodMessageRouter. PR-13: envelope signing, PeerResolution, no hardcoded loopback.
/// </summary>
public class PodMessageRouterTests
{
    [Fact]
    public async Task RouteMessageToPeersAsync_when_peer_resolution_returns_null_fails_for_that_peer()
    {
        var logger = new Mock<ILogger<PodMessageRouter>>();
        var podService = new Mock<IPodService>();
        var overlayClient = new Mock<IOverlayClient>();
        var controlSigner = new Mock<IControlSigner>();
        var peerResolution = new Mock<IPeerResolutionService>();

        controlSigner.Setup(c => c.Sign(It.IsAny<ControlEnvelope>())).Returns<ControlEnvelope>(e => e);
        peerResolution.Setup(r => r.ResolvePeerIdToEndpointAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPEndPoint?)null);

        var router = new PodMessageRouter(
            logger.Object,
            podService.Object,
            overlayClient.Object,
            controlSigner.Object,
            peerResolution.Object,
            privacyLayer: null);

        var message = new PodMessage
        {
            MessageId = "msg-1",
            ChannelId = "pod1:general",
            SenderPeerId = "peer-sender",
            Body = "hi",
            TimestampUnixMs = 1
        };

        podService.Setup(s => s.GetChannelAsync("pod1", "general", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodChannel { ChannelId = "general", Name = "General" });
        podService.Setup(s => s.GetMembersAsync("pod1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-sender", Role = "member" }, new PodMember { PeerId = "peer-other", Role = "member" } });

        var result = await router.RouteMessageToPeersAsync(message, new[] { "peer-other" });

        Assert.Equal(1, result.FailedRoutingCount);
        Assert.Equal(0, result.SuccessfullyRoutedCount);
        overlayClient.Verify(o => o.SendAsync(It.IsAny<ControlEnvelope>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RouteMessageToPeersAsync_when_peer_resolution_returns_endpoint_calls_SendAsync_with_it()
    {
        var logger = new Mock<ILogger<PodMessageRouter>>();
        var podService = new Mock<IPodService>();
        var overlayClient = new Mock<IOverlayClient>();
        var controlSigner = new Mock<IControlSigner>();
        var peerResolution = new Mock<IPeerResolutionService>();

        var resolvedEp = new IPEndPoint(IPAddress.Loopback, 9000);
        controlSigner.Setup(c => c.Sign(It.IsAny<ControlEnvelope>())).Returns<ControlEnvelope>(e => e);
        peerResolution.Setup(r => r.ResolvePeerIdToEndpointAsync("peer-other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedEp);
        overlayClient.Setup(o => o.SendAsync(It.IsAny<ControlEnvelope>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var router = new PodMessageRouter(
            logger.Object,
            podService.Object,
            overlayClient.Object,
            controlSigner.Object,
            peerResolution.Object,
            privacyLayer: null);

        var message = new PodMessage
        {
            MessageId = "msg-2",
            ChannelId = "pod1:general",
            SenderPeerId = "peer-sender",
            Body = "hi",
            TimestampUnixMs = 1
        };

        var result = await router.RouteMessageToPeersAsync(message, new[] { "peer-other" });

        Assert.Equal(1, result.SuccessfullyRoutedCount);
        Assert.Equal(0, result.FailedRoutingCount);
        overlayClient.Verify(o => o.SendAsync(It.IsAny<ControlEnvelope>(), resolvedEp, It.IsAny<CancellationToken>()), Times.Once);
        controlSigner.Verify(c => c.Sign(It.Is<ControlEnvelope>(e => e.Type == "pod_message")), Times.Once);
    }
}
