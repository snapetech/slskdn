// <copyright file="RoomsCompatibilityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Compatibility;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.API.Compatibility;
using Xunit;

public class RoomsCompatibilityControllerTests
{
    [Fact]
    public async Task JoinRoom_ReturnsSanitizedSuccessPayload()
    {
        var controller = new RoomsCompatibilityController(NullLogger<RoomsCompatibilityController>.Instance);

        var result = await controller.JoinRoom(new JoinRoomRequest(" ambient "), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("joined", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("ambient", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeaveRoom_ReturnsSanitizedSuccessPayload()
    {
        var controller = new RoomsCompatibilityController(NullLogger<RoomsCompatibilityController>.Instance);

        var result = await controller.LeaveRoom(" ambient ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("left", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("ambient", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
