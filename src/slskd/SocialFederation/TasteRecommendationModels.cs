// <copyright file="TasteRecommendationModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

public sealed class TasteRecommendationRequest
{
    public int Limit { get; set; } = 20;

    public int MinimumTrustedSources { get; set; } = 2;

    public bool IncludeSourceActors { get; set; }
}

public sealed class TasteRecommendation
{
    public WorkRef WorkRef { get; set; } = new();

    public double Score { get; set; }

    public int TrustedSourceCount { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<string> Reasons { get; set; } = new();

    public List<string> SourceActors { get; set; } = new();
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
