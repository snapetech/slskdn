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
    public async Task UpdateMyProfile_TrimsNestedFieldsAndDropsBlankEndpoints()
    {
        var c = CreateController();
        var profile = new PeerProfile { PeerId = "p1", DisplayName = "NewName" };
        _profileMock
            .Setup(x => x.UpdateMyProfileAsync(
                "NewName",
                "https://example.com/avatar.png",
                7,
                It.Is<List<PeerEndpoint>>(endpoints =>
                    endpoints.Count == 1 &&
                    endpoints[0].Type == "Direct" &&
                    endpoints[0].Address == "https://peer.example"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var r = await c.UpdateMyProfile(new UpdateProfileRequest
        {
            DisplayName = " NewName ",
            Avatar = " https://example.com/avatar.png ",
            Capabilities = 7,
            Endpoints = new List<PeerEndpoint>
            {
                new() { Type = " Direct ", Address = " https://peer.example ", Priority = 1 },
                new() { Type = "   ", Address = "   ", Priority = 2 },
            },
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(profile, ok.Value);
    }

    [Fact]
    public async Task GetProfile_Success_ReturnsPublicProfilePayload()
    {
        var c = CreateController();
        var profile = new PeerProfile
        {
            PeerId = "p1",
            DisplayName = "Alice",
            Avatar = "https://example.com/avatar.png",
            Capabilities = 7,
            PublicKey = "leaky-public-key",
            Signature = "leaky-signature",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Endpoints = new List<PeerEndpoint>
            {
                new() { Type = "Direct", Address = "https://peer.example:5030", Priority = 1 }
            }
        };
        _profileMock.Setup(x => x.GetProfileAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var r = await c.GetProfile("p1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var publicProfile = Assert.IsType<ProfileLookupResponse>(ok.Value);
        Assert.Equal("p1", publicProfile.PeerId);
        Assert.Equal("Alice", publicProfile.DisplayName);
        Assert.Equal("https://example.com/avatar.png", publicProfile.Avatar);
        Assert.Equal(7, publicProfile.Capabilities);
        Assert.Single(publicProfile.Endpoints);
        Assert.Equal("Direct", publicProfile.Endpoints[0].Type);
        Assert.Equal("https://peer.example:5030", publicProfile.Endpoints[0].Address);
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
    public async Task GetProfile_WithBlankPeerId_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.GetProfile("   ", CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("PeerId is required.", bad.Value);
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

    [Fact]
    public async Task CreateInvite_WhenProfileLookupThrows_DoesNotLeakExceptionMessage()
    {
        var c = CreateController();
        _profileMock.Setup(x => x.GetMyProfileAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var r = await c.CreateInvite(new CreateInviteRequest(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(r);
        var problemDetails = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.DoesNotContain("sensitive detail", problemDetails.Detail ?? string.Empty);
        Assert.Equal("Cannot create invite.", problemDetails.Detail);
    }
}
