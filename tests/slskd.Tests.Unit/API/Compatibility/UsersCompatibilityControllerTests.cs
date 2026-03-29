// <copyright file="UsersCompatibilityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Compatibility;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.Compatibility;
using Soulseek;
using Xunit;

public class UsersCompatibilityControllerTests
{
    [Fact]
    public async Task BrowseUser_WhenBrowseThrows_DoesNotLeakExceptionMessage()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.BrowseAsync(It.IsAny<string>(), It.IsAny<BrowseOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new UsersCompatibilityController(
            NullLogger<UsersCompatibilityController>.Instance,
            soulseekClient.Object);

        var result = await controller.BrowseUser(" alice ", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("alice", error.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed to browse user", error.Value?.ToString() ?? string.Empty);
    }
}
