// <copyright file="ShareGroupsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Sharing.API;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Sharing;
using slskd.Sharing.API;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class ShareGroupsControllerTests
{
    private readonly Mock<ISharingService> _sharingMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private IOptionsMonitor<slskd.Options> _options = new TestOptionsMonitor(new slskd.Options
    {
        Feature = new slskd.Options.FeatureOptions { CollectionsSharing = true },
        Soulseek = new slskd.Options.SoulseekOptions { Username = "alice" }
    });

    public ShareGroupsControllerTests()
    {
        // Setup service provider to return null for GetService calls (handled gracefully in controllers)
        _serviceProviderMock.Setup(x => x.GetService(It.IsAny<Type>())).Returns((object?)null);
    }

    private ShareGroupsController CreateController()
    {
        var c = new ShareGroupsController(_sharingMock.Object, _options, _serviceProviderMock.Object);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "u") }, "Test"));
        return c;
    }

    [Fact]
    public async Task GetAll_FeatureDisabled_ReturnsNotFound()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { CollectionsSharing = false },
            Soulseek = new slskd.Options.SoulseekOptions { Username = "alice" }
        });
        var c = CreateController();

        var r = await c.GetAll(CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task GetAll_Success_ReturnsList()
    {
        var c = CreateController();
        _sharingMock.Setup(x => x.GetShareGroupsByOwnerAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShareGroup> { new() { Id = Guid.NewGuid(), Name = "Group1" } });

        var r = await c.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var list = Assert.IsAssignableFrom<List<ShareGroup>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task Get_NotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _sharingMock.Setup(x => x.GetShareGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareGroup?)null);

        var r = await c.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.Create(new CreateShareGroupRequest { Name = "" }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("Name is required.", bad.Value);
    }

    [Fact]
    public async Task Create_Success_ReturnsCreated()
    {
        var c = CreateController();
        var created = new ShareGroup { Id = Guid.NewGuid(), Name = "Group1", OwnerUserId = "alice" };
        _sharingMock.Setup(x => x.CreateShareGroupAsync(It.IsAny<ShareGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var r = await c.Create(new CreateShareGroupRequest { Name = "Group1" }, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(r);
        Assert.Equal(created, createdResult.Value);
    }

    [Fact]
    public async Task AddMember_NoUserIdOrPeerId_ReturnsBadRequest()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetShareGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareGroup { Id = id, OwnerUserId = "alice" });

        var r = await c.AddMember(id, new AddMemberRequest(), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("UserId or PeerId is required.", bad.Value);
    }

    [Fact]
    public async Task GetMembers_Success_ReturnsList()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetShareGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareGroup { Id = id, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.GetShareGroupMembersAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "bob", "charlie" });

        var r = await c.GetMembers(id, false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var list = Assert.IsAssignableFrom<List<string>>(ok.Value);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetMembers_Detailed_ReturnsMemberInfos()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetShareGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareGroup { Id = id, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.GetShareGroupMemberInfosAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShareGroupMemberInfo>
            {
                new() { UserId = "bob", PeerId = "peer1", ContactNickname = "Bob" },
                new() { UserId = "charlie", PeerId = null }
            });

        var r = await c.GetMembers(id, true, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var list = Assert.IsAssignableFrom<List<ShareGroupMemberInfo>>(ok.Value);
        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsContactBased); // Computed from PeerId != null
        Assert.Equal("Bob", list[0].ContactNickname);
        Assert.False(list[1].IsContactBased); // Computed from PeerId == null
    }

    [Fact]
    public async Task AddMember_WithPeerId_Success()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetShareGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareGroup { Id = id, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.AddShareGroupMemberByPeerIdAsync(id, "peer123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var r = await c.AddMember(id, new AddMemberRequest { PeerId = "peer123" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(r);
        _sharingMock.Verify(x => x.AddShareGroupMemberByPeerIdAsync(id, "peer123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMember_WithUserId_Success()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetShareGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareGroup { Id = id, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.AddShareGroupMemberAsync(id, "bob", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var r = await c.AddMember(id, new AddMemberRequest { UserId = "bob" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(r);
        _sharingMock.Verify(x => x.AddShareGroupMemberAsync(id, "bob", It.IsAny<CancellationToken>()), Times.Once);
    }
}
