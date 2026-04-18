// <copyright file="DhtRendezvousControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

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

        var controller = CreateController(blocklist, dhtService.Object);

        var result = controller.GetDhtStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DhtStatusResponse>(ok.Value);
        Assert.True(response.IsEnabled);
        Assert.False(response.IsDhtRunning);
        Assert.Equal(9, response.DhtNodeCount);
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
        Assert.DoesNotContain("127.0.0.1", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("user-1", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("alice", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("alice", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static DhtRendezvousController CreateController(OverlayBlocklist blocklist, IDhtRendezvousService? dhtService = null)
    {
        return new DhtRendezvousController(
            dhtService ?? Mock.Of<IDhtRendezvousService>(),
            Mock.Of<IMeshOverlayServer>(),
            Mock.Of<IMeshOverlayConnector>(),
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            new OverlayRateLimiter(),
            blocklist);
    }
}
