// <copyright file="ITasteRecommendationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

public interface ITasteRecommendationService
{
    Task<TasteRecommendationResult> GetRecommendationsAsync(
        TasteRecommendationRequest request,
        CancellationToken cancellationToken = default);

    Task<TasteRecommendationWishlistPromotionResult> PromoteToWishlistAsync(
        TasteRecommendationWishlistPromotionRequest request,
        CancellationToken cancellationToken = default);

    Task<TasteRecommendationRadarSubscriptionResult> SubscribeArtistRadarAsync(
        TasteRecommendationRadarSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<TasteRecommendationGraphPreviewResult> PreviewDiscoveryGraphAsync(
        TasteRecommendationGraphPreviewRequest request,
        CancellationToken cancellationToken = default);
}
