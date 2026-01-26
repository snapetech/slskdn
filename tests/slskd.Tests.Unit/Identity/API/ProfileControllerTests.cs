// <copyright file="ProfileControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity.API;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Identity;
using slskd.Identity.API;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class ProfileControllerTests
{
    private readonly Mock<IProfileService> _profileMock = new();
    private IOptionsMonitor<slskd.Options> _options = new TestOptionsMonitor(new slskd.Options
    {
        Feature = new slskd.Options.FeatureOptions { IdentityFriends = true }
    });

    private ProfileController CreateController()
    {
        var c = new ProfileController(_profileMock.Object, _options);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "u") }, "Test"));
        return c;
    }

    [Fact]
    public async Task GetMyProfile_FeatureDisabled_ReturnsNotFound()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { IdentityFriends = false }
        });
        var c = CreateController();

        var r = await c.GetMyProfile(CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task GetMyProfile_Success_ReturnsProfile()
    {
        var c = CreateController();
        var profile = new PeerProfile { PeerId = "p1", DisplayName = "Test" };
        _profileMock.Setup(x => x.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var r = await c.GetMyProfile(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(profile, ok.Value);
    }

    [Fact]
    public async Task UpdateMyProfile_EmptyDisplayName_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.UpdateMyProfile(new UpdateProfileRequest { DisplayName = "" }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("DisplayName is required.", bad.Value);
    }

    [Fact]
    public async Task UpdateMyProfile_Success_ReturnsUpdated()
    {
        var c = CreateController();
        var profile = new PeerProfile { PeerId = "p1", DisplayName = "NewName" };
        _profileMock.Setup(x => x.UpdateMyProfileAsync("NewName", null, 0, It.IsAny<List<PeerEndpoint>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var r = await c.UpdateMyProfile(new UpdateProfileRequest { DisplayName = "NewName" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(profile, ok.Value);
    }

    [Fact]
    public async Task GetProfile_NotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _profileMock.Setup(x => x.GetProfileAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync((PeerProfile?)null);

        var r = await c.GetProfile("p1", CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task CreateInvite_Success_ReturnsInviteLink()
    {
        var c = CreateController();
        var profile = new PeerProfile { PeerId = "p1", DisplayName = "Test" };
        _profileMock.Setup(x => x.GetMyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _profileMock.Setup(x => x.GetFriendCode("p1")).Returns("ABCD-EFGH-IJKL-MNOP");

        var r = await c.CreateInvite(new CreateInviteRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var resp = Assert.IsType<InviteResponse>(ok.Value);
        Assert.StartsWith("slskdn://invite/", resp.InviteLink);
        Assert.Equal("ABCD-EFGH-IJKL-MNOP", resp.FriendCode);
    }
}
