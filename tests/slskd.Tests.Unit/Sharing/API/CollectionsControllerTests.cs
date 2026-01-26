// <copyright file="CollectionsControllerTests.cs" company="slskdN Team">
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

public class CollectionsControllerTests
{
    private readonly Mock<ISharingService> _sharingMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private IOptionsMonitor<slskd.Options> _options = new TestOptionsMonitor(new slskd.Options
    {
        Feature = new slskd.Options.FeatureOptions { CollectionsSharing = true },
        Soulseek = new slskd.Options.SoulseekOptions { Username = "alice" }
    });

    public CollectionsControllerTests()
    {
        // Setup service provider to return null for GetService calls (handled gracefully in controllers)
        _serviceProviderMock.Setup(x => x.GetService(It.IsAny<Type>())).Returns((object?)null);
    }

    private CollectionsController CreateController()
    {
        var c = new CollectionsController(_sharingMock.Object, _options, _serviceProviderMock.Object);
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
        _sharingMock.Setup(x => x.GetCollectionsByOwnerAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Collection> { new() { Id = Guid.NewGuid(), Title = "Test" } });

        var r = await c.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var list = Assert.IsAssignableFrom<List<Collection>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task Get_NotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _sharingMock.Setup(x => x.GetCollectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var r = await c.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Get_WrongOwner_ReturnsNotFound()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = id, OwnerUserId = "bob" });

        var r = await c.Get(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Create_EmptyTitle_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.Create(new CreateCollectionRequest { Title = "" }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("Title is required.", bad.Value);
    }

    [Fact]
    public async Task Create_Success_ReturnsCreated()
    {
        var c = CreateController();
        var created = new Collection { Id = Guid.NewGuid(), Title = "Test", OwnerUserId = "alice" };
        _sharingMock.Setup(x => x.CreateCollectionAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var r = await c.Create(new CreateCollectionRequest { Title = "Test" }, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(r);
        Assert.Equal(created, createdResult.Value);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var c = CreateController();
        _sharingMock.Setup(x => x.GetCollectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var r = await c.Update(Guid.NewGuid(), new UpdateCollectionRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Delete_Success_ReturnsNoContent()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = id, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.DeleteCollectionAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var r = await c.Delete(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(r);
    }
}
