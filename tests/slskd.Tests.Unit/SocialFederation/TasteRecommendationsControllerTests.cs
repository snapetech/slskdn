// <copyright file="TasteRecommendationsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.SocialFederation;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.SocialFederation;
using slskd.SocialFederation.API;

public sealed class TasteRecommendationsControllerTests
{
    [Fact]
    public async Task PromoteToWishlist_ReturnsServiceResult()
    {
        var service = new Mock<ITasteRecommendationService>();
        service
            .Setup(s => s.PromoteToWishlistAsync(It.IsAny<TasteRecommendationWishlistPromotionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TasteRecommendationWishlistPromotionResult
            {
                Created = true,
                WishlistItemId = "wishlist-1",
                SearchText = "artist title",
            });
        var controller = new TasteRecommendationsController(service.Object);

        var result = await controller.PromoteToWishlist(new TasteRecommendationWishlistPromotionRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var promotion = Assert.IsType<TasteRecommendationWishlistPromotionResult>(ok.Value);
        Assert.True(promotion.Created);
    }

    [Fact]
    public async Task SubscribeReleaseRadar_ReturnsBadRequestWhenServiceRejects()
    {
        var service = new Mock<ITasteRecommendationService>();
        service
            .Setup(s => s.SubscribeArtistRadarAsync(It.IsAny<TasteRecommendationRadarSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TasteRecommendationRadarSubscriptionResult
            {
                Created = false,
                Message = "artist required",
            });
        var controller = new TasteRecommendationsController(service.Object);

        var result = await controller.SubscribeReleaseRadar(new TasteRecommendationRadarSubscriptionRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PreviewDiscoveryGraph_ReturnsGraphPreview()
    {
        var service = new Mock<ITasteRecommendationService>();
        service
            .Setup(s => s.PreviewDiscoveryGraphAsync(It.IsAny<TasteRecommendationGraphPreviewRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TasteRecommendationGraphPreviewResult
            {
                Available = true,
                SeedNodeId = "artist:seed",
                NodeCount = 1,
            });
        var controller = new TasteRecommendationsController(service.Object);

        var result = await controller.PreviewDiscoveryGraph(new TasteRecommendationGraphPreviewRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var preview = Assert.IsType<TasteRecommendationGraphPreviewResult>(ok.Value);
        Assert.True(preview.Available);
        Assert.Equal("artist:seed", preview.SeedNodeId);
    }
}
