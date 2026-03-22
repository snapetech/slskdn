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
    public void Unblock_With_Blank_Target_Returns_BadRequest()
    {
        using var blocklist = new OverlayBlocklist(NullLogger<OverlayBlocklist>.Instance);
        var controller = CreateController(blocklist);

        var result = controller.Unblock(" username ", "   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static DhtRendezvousController CreateController(OverlayBlocklist blocklist)
    {
        return new DhtRendezvousController(
            Mock.Of<IDhtRendezvousService>(),
            Mock.Of<IMeshOverlayServer>(),
            Mock.Of<IMeshOverlayConnector>(),
            new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance),
            new OverlayRateLimiter(),
            blocklist);
    }
}
