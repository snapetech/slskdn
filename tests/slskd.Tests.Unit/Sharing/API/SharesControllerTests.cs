// <copyright file="SharesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Sharing.API;

using System;
using System.Collections.Generic;
using System.Linq;
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

public class SharesControllerTests
{
    private readonly Mock<ISharingService> _sharingMock = new();
    private readonly Mock<IShareTokenService> _tokensMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private IOptionsMonitor<slskd.Options> _options = new TestOptionsMonitor(new slskd.Options
    {
        Feature = new slskd.Options.FeatureOptions { CollectionsSharing = true, Streaming = true },
        Soulseek = new slskd.Options.SoulseekOptions { Username = "alice" }
    });

    public SharesControllerTests()
    {
        // Setup service provider to return null for GetService calls (handled gracefully in controllers)
        _serviceProviderMock.Setup(x => x.GetService(It.IsAny<Type>())).Returns((object?)null);
    }

    private SharesController CreateController()
    {
        var c = new SharesController(_sharingMock.Object, _tokensMock.Object, _options, _serviceProviderMock.Object);
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
        _sharingMock.Setup(x => x.GetShareGrantsAccessibleByUserAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShareGrant> { new() { Id = Guid.NewGuid() } });

        var r = await c.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var list = Assert.IsAssignableFrom<List<ShareGrant>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task Create_EmptyCollectionId_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.Create(new CreateShareGrantRequest { CollectionId = default }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("CollectionId is required.", bad.Value);
    }

    [Fact]
    public async Task Create_CollectionNotFound_ReturnsNotFound()
    {
        var c = CreateController();
        var collectionId = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var r = await c.Create(new CreateShareGrantRequest
        {
            CollectionId = collectionId,
            AudienceType = "User",
            AudienceId = "bob"
        }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Create_Success_ReturnsCreated()
    {
        var c = CreateController();
        var collectionId = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = collectionId, OwnerUserId = "alice" });
        var created = new ShareGrant { Id = Guid.NewGuid(), CollectionId = collectionId };
        _sharingMock.Setup(x => x.CreateShareGrantAsync(It.IsAny<ShareGrant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var r = await c.Create(new CreateShareGrantRequest
        {
            CollectionId = collectionId,
            AudienceType = "User",
            AudienceId = "bob"
        }, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(r);
        Assert.Equal(created, createdResult.Value);
    }

    [Fact]
    public async Task CreateToken_GrantNotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _sharingMock.Setup(x => x.GetShareGrantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareGrant?)null);

        var r = await c.CreateToken(Guid.NewGuid(), new CreateTokenRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task CreateToken_Success_ReturnsToken()
    {
        var c = CreateController();
        var grantId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var grant = new ShareGrant { Id = grantId, CollectionId = collectionId };
        _sharingMock.Setup(x => x.GetShareGrantAsync(grantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grant);
        _sharingMock.Setup(x => x.GetCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = collectionId, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.CreateTokenAsync(grantId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token123");

        var r = await c.CreateToken(grantId, new CreateTokenRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var resp = Assert.IsType<TokenResponse>(ok.Value);
        Assert.Equal("token123", resp.Token);
    }

    [Fact]
    public async Task GetManifest_TokenAuth_InvalidToken_ReturnsUnauthorized()
    {
        var c = CreateController();
        c.HttpContext.Request.QueryString = new QueryString("?token=bad");
        _tokensMock.Setup(x => x.ValidateAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareTokenClaims?)null);

        var r = await c.GetManifest(Guid.NewGuid(), "bad", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(r);
    }

    [Fact]
    public async Task GetManifest_TokenAuth_Success()
    {
        var c = CreateController();
        var grantId = Guid.NewGuid();
        var claims = new ShareTokenClaims(grantId.ToString(), Guid.NewGuid().ToString(), null, true, true, 1, DateTimeOffset.UtcNow.AddHours(1));
        _tokensMock.Setup(x => x.ValidateAsync("token123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(claims);
        var manifest = new ShareManifestDto { CollectionId = claims.CollectionId, Title = "Test" };
        _sharingMock.Setup(x => x.GetManifestAsync(grantId, "token123", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);

        var r = await c.GetManifest(grantId, "token123", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(manifest, ok.Value);
    }

    [Fact]
    public async Task GetManifest_NormalAuth_NotAuthenticated_ReturnsUnauthorized()
    {
        var c = CreateController();
        c.HttpContext.User = new ClaimsPrincipal();

        var r = await c.GetManifest(Guid.NewGuid(), null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(r);
    }

    [Fact]
    public async Task Create_WithAudiencePeerId_Success()
    {
        var c = CreateController();
        var collectionId = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = collectionId, OwnerUserId = "alice" });
        var created = new ShareGrant { Id = Guid.NewGuid(), CollectionId = collectionId, AudiencePeerId = "peer123" };
        _sharingMock.Setup(x => x.CreateShareGrantAsync(It.Is<ShareGrant>(g => g.AudiencePeerId == "peer123"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var r = await c.Create(new CreateShareGrantRequest
        {
            CollectionId = collectionId,
            AudienceType = "User",
            AudienceId = "bob",
            AudiencePeerId = "peer123"
        }, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(r);
        var grant = Assert.IsType<ShareGrant>(createdResult.Value);
        Assert.Equal("peer123", grant.AudiencePeerId);
    }
}
