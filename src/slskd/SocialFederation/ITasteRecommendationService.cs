// <copyright file="ITasteRecommendationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

public interface ITasteRecommendationService
{
    Task<TasteRecommendationResult> GetRecommendationsAsync(
        TasteRecommendationRequest request,
        CancellationToken cancellationToken = default);
}
