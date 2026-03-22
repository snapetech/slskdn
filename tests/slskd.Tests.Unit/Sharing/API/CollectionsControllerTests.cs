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
        _sharingMock.Setup(x => x.GetShareGrantsAccessibleByUserAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShareGrant>()); // Empty list - user has no share grants for this collection

        var r = await c.Get(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Create_EmptyTitle_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.Create(new CreateCollectionRequest { Title = "" }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        var problemDetails = Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(bad.Value);
        Assert.Equal("Title is required.", problemDetails.Detail);
    }

    [Fact]
    public async Task Create_NullRequest_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.Create(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(r);
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
    public async Task Create_WithWhitespaceOnlyDescription_NormalizesToNull()
    {
        var c = CreateController();
        _sharingMock.Setup(x => x.CreateCollectionAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = Guid.NewGuid(), Title = "Test", OwnerUserId = "alice" });

        var r = await c.Create(new CreateCollectionRequest { Title = "Test", Description = "   " }, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(r);
        _sharingMock.Verify(x => x.CreateCollectionAsync(
            It.Is<Collection>(collection => collection.Description == null),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task Update_BlankTitle_ReturnsBadRequest()
    {
        var c = CreateController();
        var id = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = id, OwnerUserId = "alice", Title = "Test" });

        var r = await c.Update(id, new UpdateCollectionRequest { Title = "   " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(r);
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

    [Fact]
    public async Task ReorderItems_WithDuplicateOrEmptyIds_ReturnsBadRequest()
    {
        var c = CreateController();

        var duplicateResult = await c.ReorderItems(
            Guid.NewGuid(),
            new ReorderRequest { ItemIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.Empty } },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(duplicateResult);
    }

    [Fact]
    public async Task ReorderItems_WithDuplicateIds_ReturnsBadRequest()
    {
        var c = CreateController();
        var itemId = Guid.NewGuid();

        var result = await c.ReorderItems(
            Guid.NewGuid(),
            new ReorderRequest { ItemIds = new List<Guid> { itemId, itemId } },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateItem_TrimsFieldsBeforePersisting()
    {
        var c = CreateController();
        var collectionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new CollectionItem { Id = itemId, CollectionId = collectionId, ContentId = "old", MediaKind = "audio", ContentHash = "hash" };

        _sharingMock.Setup(x => x.GetCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = collectionId, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.GetCollectionItemsAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CollectionItem> { item });
        _sharingMock.Setup(x => x.UpdateCollectionItemAsync(It.IsAny<CollectionItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var r = await c.UpdateItem(
            collectionId,
            itemId,
            new UpdateCollectionItemRequest
            {
                ContentId = " content:mb:recording:1 ",
                MediaKind = " audio ",
                ContentHash = " hash-2 "
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var updated = Assert.IsType<CollectionItem>(ok.Value);
        Assert.Equal("content:mb:recording:1", updated.ContentId);
        Assert.Equal("audio", updated.MediaKind);
        Assert.Equal("hash-2", updated.ContentHash);
    }

    [Fact]
    public async Task AddItem_WithWhitespaceOnlyOptionalFields_NormalizesToNull()
    {
        var c = CreateController();
        var collectionId = Guid.NewGuid();
        _sharingMock.Setup(x => x.GetCollectionAsync(collectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection { Id = collectionId, OwnerUserId = "alice" });
        _sharingMock.Setup(x => x.AddCollectionItemAsync(It.IsAny<CollectionItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CollectionItem { Id = Guid.NewGuid(), CollectionId = collectionId, ContentId = "content:1" });

        var r = await c.AddItem(
            collectionId,
            new AddCollectionItemRequest
            {
                ContentId = " content:1 ",
                MediaKind = "   ",
                ContentHash = "\t"
            },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(r);
        _sharingMock.Verify(x => x.AddCollectionItemAsync(
            It.Is<CollectionItem>(item =>
                item.ContentId == "content:1" &&
                item.MediaKind == null &&
                item.ContentHash == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
