// <copyright file="TasteRecommendationModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

public sealed class TasteRecommendationRequest
{
    public int Limit { get; set; } = 20;

    public int MinimumTrustedSources { get; set; } = 2;

    public bool IncludeSourceActors { get; set; }

    public bool IncludeGraphEvidence { get; set; } = true;
}

public sealed class TasteRecommendation
{
    public WorkRef WorkRef { get; set; } = new();

    public double Score { get; set; }

    public int TrustedSourceCount { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<string> Reasons { get; set; } = new();

    public List<string> SourceActors { get; set; } = new();

    public TasteRecommendationGraphEvidence? GraphEvidence { get; set; }
}

public sealed class TasteRecommendationGraphEvidence
{
    public string SeedNodeId { get; set; } = string.Empty;

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public double ProximityScore { get; set; }

    public List<string> Reasons { get; set; } = new();
}

public sealed class TasteRecommendationResult
{
    public int MinimumTrustedSources { get; set; }

    public int TrustedActorCount { get; set; }

    public int CandidateCount { get; set; }

    public List<TasteRecommendation> Recommendations { get; set; } = new();
}

public sealed class FederatedWorkRefObservation
{
    public string ActorName { get; set; } = "music";

    public string RemoteActor { get; set; } = string.Empty;

    public WorkRef WorkRef { get; set; } = new();

    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TasteRecommendationWishlistPromotionRequest
{
    public WorkRef WorkRef { get; set; } = new();

    public string? Note { get; set; }
}

public sealed class TasteRecommendationWishlistPromotionResult
{
    public bool Created { get; set; }

    public string? WishlistItemId { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public string? Message { get; set; }
}

public sealed class TasteRecommendationRadarSubscriptionRequest
{
    public WorkRef WorkRef { get; set; } = new();

    public string? ArtistId { get; set; }

    public string Scope { get; set; } = "trusted";
}

public sealed class TasteRecommendationRadarSubscriptionResult
{
    public bool Created { get; set; }

    public string? SubscriptionId { get; set; }

    public string? ArtistId { get; set; }

    public string? Message { get; set; }
}

public sealed class TasteRecommendationGraphPreviewRequest
{
    public WorkRef WorkRef { get; set; } = new();
}

public sealed class TasteRecommendationGraphPreviewResult
{
    public bool Available { get; set; }

    public string? Message { get; set; }

    public string SeedNodeId { get; set; } = string.Empty;

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public List<string> Reasons { get; set; } = new();
}
