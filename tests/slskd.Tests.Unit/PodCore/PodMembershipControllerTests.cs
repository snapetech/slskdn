// <copyright file="PodMembershipControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.PodCore;

using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Core.Security;
using slskd.PodCore;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodMembershipControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedAccess()
    {
        var authorize = typeof(PodMembershipController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AuthPolicy.Any, authorize.Policy);
    }

    [Fact]
    public async Task ChangeRole_TrimsPodPeerAndRoleBeforeDispatch()
    {
        var membershipService = new Mock<IPodMembershipService>();
        membershipService
            .Setup(service => service.ChangeRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipPublishResult(true, "pod-1", "peer-1", "dht:key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var controller = new PodMembershipController(
            NullLogger<PodMembershipController>.Instance,
            membershipService.Object);

        var result = await controller.ChangeRole(" pod-1 ", " peer-1 ", new ChangeRoleRequest(" moderator "), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        membershipService.Verify(
            service => service.ChangeRoleAsync("pod-1", "peer-1", "moderator", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishMembership_TrimsMemberPeerIdBeforeDispatch()
    {
        var membershipService = new Mock<IPodMembershipService>();
        membershipService
            .Setup(service => service.PublishMembershipAsync(It.IsAny<string>(), It.IsAny<PodMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipPublishResult(true, "pod-1", "peer-1", "dht:key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var controller = new PodMembershipController(
            NullLogger<PodMembershipController>.Instance,
            membershipService.Object);

        var result = await controller.PublishMembership(
            " pod-1 ",
            new PodMember { PeerId = " peer-1 ", Role = " member " },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        membershipService.Verify(
            service => service.PublishMembershipAsync(
                "pod-1",
                It.Is<PodMember>(member => member.PeerId == "peer-1" && member.Role == "member"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateMembership_TrimsMemberFieldsBeforeDispatch()
    {
        var membershipService = new Mock<IPodMembershipService>();
        membershipService
            .Setup(service => service.UpdateMembershipAsync(It.IsAny<string>(), It.IsAny<PodMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipPublishResult(true, "pod-1", "peer-1", "dht:key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var controller = new PodMembershipController(
            NullLogger<PodMembershipController>.Instance,
            membershipService.Object);

        var result = await controller.UpdateMembership(
            " pod-1 ",
            " peer-1 ",
            new PodMember { Role = " member ", PublicKey = " pub " },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        membershipService.Verify(
            service => service.UpdateMembershipAsync(
                "pod-1",
                It.Is<PodMember>(member =>
                    member.PeerId == "peer-1" &&
                    member.Role == "member" &&
                    member.PublicKey == "pub"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishMembership_WhenServiceReturnsFailure_DoesNotLeakErrorMessage()
    {
        var membershipService = new Mock<IPodMembershipService>();
        membershipService
            .Setup(service => service.PublishMembershipAsync(It.IsAny<string>(), It.IsAny<PodMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipPublishResult(false, "pod-1", "peer-1", string.Empty, DateTimeOffset.MinValue, DateTimeOffset.MinValue, "sensitive detail"));

        var controller = new PodMembershipController(
            NullLogger<PodMembershipController>.Instance,
            membershipService.Object);

        var result = await controller.PublishMembership(
            "pod-1",
            new PodMember { PeerId = "peer-1", Role = "member" },
            CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to publish membership", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetMembership_WhenServiceReturnsNotFound_DoesNotLeakErrorMessage()
    {
        var membershipService = new Mock<IPodMembershipService>();
        membershipService
            .Setup(service => service.GetMembershipAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipRetrievalResult(false, "pod-1", "peer-1", null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, false, "sensitive detail"));

        var controller = new PodMembershipController(
            NullLogger<PodMembershipController>.Instance,
            membershipService.Object);

        var result = await controller.GetMembership("pod-1", "peer-1", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", notFound.Value?.ToString() ?? string.Empty);
        Assert.Contains("Membership not found", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("pod-1", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("peer-1", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
