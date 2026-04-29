// <copyright file="DhtRendezvousControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.API;
using slskd.DhtRendezvous.Security;
using Xunit;

public class DhtRendezvousControllerTests
{
    [Fact]
    public void Controller_Allows_ApiKey_Or_Jwt_Authentication()
    {
        var authorize = typeof(DhtRendezvousController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicy.Any, authorize!.Policy);
    }

    [Fact]
    public async Task ConnectOverlayPeer_WithInvalidPort_ReturnsBadRequest()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = await controller.ConnectOverlayPeer(new ConnectOverlayPeerRequest
        {
            Address = "127.0.0.1",
            Port = 70000,
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ConnectOverlayPeer_WhenConnectorFails_ReturnsBadGateway()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector.Setup(x => x.ConnectToEndpointAsync(It.IsAny<System.Net.IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeshOverlayConnection?)null);

        var controller = CreateController(blocklist, overlayConnector: connector.Object);

        var result = await controller.ConnectOverlayPeer(new ConnectOverlayPeerRequest
        {
            Address = "127.0.0.1",
            Port = 50305,
        }, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(502, response.StatusCode);
        var body = Assert.IsType<OverlayConnectResultResponse>(response.Value);
        Assert.False(body.Connected);
        Assert.Equal("127.0.0.1", body.Address);
        Assert.Equal(50305, body.Port);
    }

    [Fact]
    public void GetDhtStatus_UsesConfiguredEnabledFlag_InsteadOfReadiness()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var dhtService = new Mock<IDhtRendezvousService>();
        dhtService.Setup(x => x.GetStats()).Returns(new DhtRendezvousStats
        {
            IsEnabled = true,
            IsBeaconCapable = true,
            IsDhtRunning = false,
            DhtNodeCount = 9,
        });

        var controller = CreateController(blocklist, dhtService: dhtService.Object);

        var result = controller.GetDhtStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DhtStatusResponse>(ok.Value);
        Assert.True(response.IsEnabled);
        Assert.False(response.IsDhtRunning);
        Assert.Equal(9, response.DhtNodeCount);
    }

    [Fact]
    public void GetDhtStatus_ExposesCandidateRollupCounters()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var dhtService = new Mock<IDhtRendezvousService>();
        dhtService.Setup(x => x.GetStats()).Returns(new DhtRendezvousStats
        {
            IsEnabled = true,
            TotalCandidateEndpointsSeen = 11,
            TotalCandidatesAccepted = 8,
            TotalCandidatesSkippedDhtPort = 1,
            TotalCandidatesSkippedDiscoveredCapacity = 2,
            TotalCandidatesDeferredConnectorCapacity = 3,
            TotalCandidatesSkippedReconnectBackoff = 4,
        });

        var controller = CreateController(blocklist, dhtService: dhtService.Object);

        var result = controller.GetDhtStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DhtStatusResponse>(ok.Value);
        Assert.Equal(11, response.TotalCandidateEndpointsSeen);
        Assert.Equal(8, response.TotalCandidatesAccepted);
        Assert.Equal(1, response.TotalCandidatesSkippedDhtPort);
        Assert.Equal(2, response.TotalCandidatesSkippedDiscoveredCapacity);
        Assert.Equal(3, response.TotalCandidatesDeferredConnectorCapacity);
        Assert.Equal(4, response.TotalCandidatesSkippedReconnectBackoff);
    }

    [Fact]
    public void GetOverlayStats_ExposesConnectorFailureReasons()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var connector = new Mock<IMeshOverlayConnector>();
        connector.Setup(x => x.GetStats()).Returns(new MeshOverlayConnectorStats
        {
            PendingConnections = 1,
            SuccessfulConnections = 2,
            FailedConnections = 3,
            EndpointCooldownSkips = 7,
            FailureReasons = new OverlayConnectionFailureStats
            {
                ConnectTimeouts = 4,
                TlsEofFailures = 5,
                ProtocolHandshakeFailures = 6,
            },
            TopProblemEndpoints =
            [
                new OverlayEndpointHealthStats
                {
                    Endpoint = "203.0.113.10:50305",
                    ConsecutiveFailureCount = 3,
                    TotalFailures = 5,
                    LastFailureReason = "TlsEof",
                    LastFailureAt = DateTimeOffset.UtcNow,
                    SuppressedUntil = DateTimeOffset.UtcNow.AddMinutes(4),
                    LastUsername = "alice",
                },
            ],
        });

        var dhtService = new Mock<IDhtRendezvousService>();
        dhtService.Setup(x => x.GetMeshPeers()).Returns(System.Array.Empty<MeshPeerInfo>());

        var controller = CreateController(blocklist, dhtService: dhtService.Object, overlayConnector: connector.Object);

        var result = controller.GetOverlayStats();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<OverlayStatsResponse>(ok.Value);
        Assert.Equal(1, response.Connector.PendingConnections);
        Assert.Equal(4, response.Connector.FailureReasons.ConnectTimeouts);
        Assert.Equal(5, response.Connector.FailureReasons.TlsEofFailures);
        Assert.Equal(6, response.Connector.FailureReasons.ProtocolHandshakeFailures);
        Assert.Equal(7, response.Connector.EndpointCooldownSkips);
        Assert.Single(response.Connector.TopProblemEndpoints);
        Assert.Equal("203.0.113.10:50305", response.Connector.TopProblemEndpoints[0].Endpoint);
    }

    [Fact]
    public void BlockUsername_Trims_Request_Before_Blocking()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.BlockUsername(new BlockUsernameRequest
        {
            Username = " user-1 ",
            Reason = " noisy ",
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.True(blocklist.IsBlocked("user-1"));
    }

    [Fact]
    public void BlockIp_ReturnsSanitizedSuccessMessage()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.BlockIp(new BlockIpRequest
        {
            Ip = " 127.0.0.1 ",
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("IP address blocked", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("127.0.0.1", ok.Value?.ToString() ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlockUsername_ReturnsSanitizedSuccessMessage()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.BlockUsername(new BlockUsernameRequest
        {
            Username = " user-1 ",
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Username blocked", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("user-1", ok.Value?.ToString() ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unblock_With_Blank_Target_Returns_BadRequest()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.Unblock(" username ", "   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Unblock_With_Unsupported_Type_Returns_Sanitized_BadRequest()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.Unblock(" peer ", "alice");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid blocklist entry type", badRequest.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public void Unblock_With_Missing_Entry_Returns_Sanitized_NotFound()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.Unblock(" username ", " alice ");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("Blocklist entry not found", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("alice", notFound.Value?.ToString() ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unblock_WhenEntryExists_ReturnsSanitizedSuccessMessage()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        blocklist.BlockUsername("alice", "test");
        var controller = CreateController(blocklist);

        var result = controller.Unblock(" username ", " alice ");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Blocklist entry removed", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("alice", ok.Value?.ToString() ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    private static DhtRendezvousController CreateController(
        OverlayBlocklist blocklist,
        IDhtRendezvousService? dhtService = null,
        IMeshOverlayConnector? overlayConnector = null)
    {
        var overlayServer = new Mock<IMeshOverlayServer>();
        overlayServer.Setup(x => x.GetStats()).Returns(new MeshOverlayServerStats());

        return new DhtRendezvousController(
            dhtService ?? Mock.Of<IDhtRendezvousService>(),
            overlayServer.Object,
            overlayConnector ?? Mock.Of<IMeshOverlayConnector>(),
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            new OverlayRateLimiter(),
            blocklist);
    }
}
