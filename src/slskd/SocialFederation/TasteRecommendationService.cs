// <copyright file="TasteRecommendationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using slskd.DiscoveryGraph;
using slskd.Integrations.MusicBrainz.Radar;
using slskd.Wishlist;

public sealed class TasteRecommendationService : ITasteRecommendationService
{
    private const int MaxInboxActivities = 250;
    private const int MaxRecommendationLimit = 100;

    private readonly IActivityPubInboxStore _inboxStore;
    private readonly IActivityPubRelationshipStore _relationshipStore;
    private readonly IDiscoveryGraphService? _discoveryGraphService;
    private readonly IWishlistService? _wishlistService;
    private readonly IArtistReleaseRadarService? _artistRadarService;
    private readonly ILogger<TasteRecommendationService> _logger;

    public TasteRecommendationService(
        IActivityPubInboxStore inboxStore,
        IActivityPubRelationshipStore relationshipStore,
        ILogger<TasteRecommendationService> logger)
        : this(inboxStore, relationshipStore, null, null, null, logger)
    {
    }

    public TasteRecommendationService(
        IActivityPubInboxStore inboxStore,
        IActivityPubRelationshipStore relationshipStore,
        IDiscoveryGraphService? discoveryGraphService,
        IWishlistService? wishlistService,
        IArtistReleaseRadarService? artistRadarService,
        ILogger<TasteRecommendationService> logger)
    {
        _inboxStore = inboxStore;
        _relationshipStore = relationshipStore;
        _discoveryGraphService = discoveryGraphService;
        _wishlistService = wishlistService;
        _artistRadarService = artistRadarService;
        _logger = logger;
    }

    public async Task<TasteRecommendationResult> GetRecommendationsAsync(
        TasteRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, MaxRecommendationLimit);
        var minimumTrustedSources = Math.Max(2, request.MinimumTrustedSources);
        var trustedActors = await _relationshipStore
            .GetFollowingAsync("music", MaxInboxActivities, cancellationToken)
            .ConfigureAwait(false);
        var trustedActorSet = trustedActors.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = await _inboxStore
            .GetActivitiesAsync("music", MaxInboxActivities, cancellationToken)
            .ConfigureAwait(false);
        var observations = new List<FederatedWorkRefObservation>();

        foreach (var entry in entries)
        {
            if (!trustedActorSet.Contains(entry.RemoteActor))
            {
                continue;
            }

            observations.AddRange(ExtractObservations(entry));
        }

        var result = BuildRecommendations(observations, trustedActorSet.Count, minimumTrustedSources, limit, request.IncludeSourceActors);
        if (request.IncludeGraphEvidence && _discoveryGraphService != null)
        {
            await AddGraphEvidenceAsync(result.Recommendations, cancellationToken).ConfigureAwait(false);
            result.Recommendations = result.Recommendations
                .OrderByDescending(recommendation => recommendation.Score)
                .ThenBy(recommendation => recommendation.WorkRef.Creator ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(recommendation => recommendation.WorkRef.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return result;
    }

    public async Task<TasteRecommendationWishlistPromotionResult> PromoteToWishlistAsync(
        TasteRecommendationWishlistPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_wishlistService == null)
        {
            return new TasteRecommendationWishlistPromotionResult
            {
                Created = false,
                Message = "Wishlist service is not available.",
            };
        }

        if (!IsRecommendable(request.WorkRef))
        {
            return new TasteRecommendationWishlistPromotionResult
            {
                Created = false,
                Message = "WorkRef is not a safe music recommendation.",
            };
        }

        var searchText = BuildSearchText(request.WorkRef);
        var existing = await _wishlistService.ListAsync().ConfigureAwait(false);
        var duplicate = existing.FirstOrDefault(item =>
            string.Equals(item.SearchText, searchText, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            return new TasteRecommendationWishlistPromotionResult
            {
                Created = false,
                WishlistItemId = duplicate.Id.ToString(),
                SearchText = searchText,
                Message = "Wishlist already has this recommendation seed.",
            };
        }

        var created = await _wishlistService.CreateAsync(new WishlistItem
        {
            SearchText = searchText,
            Filter = BuildWishlistFilter(request.WorkRef, request.Note),
            AutoDownload = false,
            Enabled = false,
            MaxResults = 25,
            CreatedAt = DateTime.UtcNow,
        }).ConfigureAwait(false);

        return new TasteRecommendationWishlistPromotionResult
        {
            Created = true,
            WishlistItemId = created.Id.ToString(),
            SearchText = searchText,
            Message = "Created review-only Wishlist seed.",
        };
    }

    public async Task<TasteRecommendationRadarSubscriptionResult> SubscribeArtistRadarAsync(
        TasteRecommendationRadarSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_artistRadarService == null)
        {
            return new TasteRecommendationRadarSubscriptionResult
            {
                Created = false,
                Message = "Artist release radar service is not available.",
            };
        }

        if (!IsRecommendable(request.WorkRef))
        {
            return new TasteRecommendationRadarSubscriptionResult
            {
                Created = false,
                Message = "WorkRef is not a safe music recommendation.",
            };
        }

        var artistId = NormalizeArtistId(request.ArtistId) ?? GetExternalId(request.WorkRef, "musicbrainz_artist");
        if (string.IsNullOrWhiteSpace(artistId))
        {
            return new TasteRecommendationRadarSubscriptionResult
            {
                Created = false,
                Message = "Artist MusicBrainz ID is required for release radar subscription.",
            };
        }

        var subscription = await _artistRadarService.SubscribeAsync(new ArtistRadarSubscription
        {
            ArtistId = artistId,
            ArtistName = request.WorkRef.Creator ?? string.Empty,
            Scope = string.IsNullOrWhiteSpace(request.Scope) ? "trusted" : request.Scope.Trim(),
        }, cancellationToken).ConfigureAwait(false);

        return new TasteRecommendationRadarSubscriptionResult
        {
            Created = true,
            SubscriptionId = subscription.Id,
            ArtistId = subscription.ArtistId,
            Message = "Created artist release radar subscription.",
        };
    }

    public async Task<TasteRecommendationGraphPreviewResult> PreviewDiscoveryGraphAsync(
        TasteRecommendationGraphPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_discoveryGraphService == null)
        {
            return new TasteRecommendationGraphPreviewResult
            {
                Available = false,
                Message = "Discovery Graph service is not available.",
            };
        }

        if (!IsRecommendable(request.WorkRef))
        {
            return new TasteRecommendationGraphPreviewResult
            {
                Available = false,
                Message = "WorkRef is not a safe music recommendation.",
            };
        }

        var graphRequest = BuildGraphRequest(request.WorkRef);
        try
        {
            var graph = await _discoveryGraphService.BuildAsync(graphRequest, cancellationToken).ConfigureAwait(false);
            return new TasteRecommendationGraphPreviewResult
            {
                Available = true,
                SeedNodeId = graph.SeedNodeId,
                NodeCount = graph.Nodes.Count,
                EdgeCount = graph.Edges.Count,
                Reasons = BuildGraphReasons(graph),
            };
        }
        catch (InvalidOperationException ex)
        {
            return new TasteRecommendationGraphPreviewResult
            {
                Available = false,
                Message = ex.Message,
            };
        }
    }

    internal static TasteRecommendationResult BuildRecommendations(
        IEnumerable<FederatedWorkRefObservation> observations,
        int trustedActorCount,
        int minimumTrustedSources,
        int limit,
        bool includeSourceActors)
    {
        var grouped = observations
            .Where(o => string.Equals(o.ActorName, "music", StringComparison.OrdinalIgnoreCase))
            .Where(o => !string.IsNullOrWhiteSpace(o.RemoteActor))
            .Where(o => IsRecommendable(o.WorkRef))
            .GroupBy(o => BuildWorkKey(o.WorkRef), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sourceActors = group
                    .Select(o => o.RemoteActor)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var lastSeen = group.Max(o => o.ObservedAt);
                var workRef = group
                    .OrderByDescending(o => o.ObservedAt)
                    .Select(o => o.WorkRef)
                    .First();
                var sourceCount = sourceActors.Count;

                return new TasteRecommendation
                {
                    WorkRef = workRef,
                    TrustedSourceCount = sourceCount,
                    LastSeenAt = lastSeen,
                    Score = Math.Round(sourceCount * 10 + RecencyScore(lastSeen), 3),
                    Reasons = BuildReasons(workRef, sourceCount),
                    SourceActors = includeSourceActors ? sourceActors : new List<string>(),
                };
            })
            .ToList();

        var recommendations = grouped
            .Where(r => r.TrustedSourceCount >= minimumTrustedSources)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.WorkRef.Creator ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.WorkRef.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return new TasteRecommendationResult
        {
            MinimumTrustedSources = minimumTrustedSources,
            TrustedActorCount = trustedActorCount,
            CandidateCount = grouped.Count,
            Recommendations = recommendations,
        };
    }

    private IEnumerable<FederatedWorkRefObservation> ExtractObservations(ActivityPubInboxEntry entry)
    {
        using var document = JsonDocument.Parse(entry.RawJson);
        foreach (var workRefElement in EnumerateWorkRefElements(document.RootElement))
        {
            WorkRef? workRef;
            try
            {
                workRef = workRefElement.Deserialize<WorkRef>();
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "[TasteRecommendations] Ignored malformed inbound WorkRef from {RemoteActor}", entry.RemoteActor);
                continue;
            }

            if (workRef?.ValidateSecurity() == true)
            {
                yield return new FederatedWorkRefObservation
                {
                    ActorName = entry.ActorName,
                    RemoteActor = entry.RemoteActor,
                    WorkRef = workRef,
                    ObservedAt = entry.ReceivedAt,
                };
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateWorkRefElements(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (IsWorkRefElement(element))
        {
            yield return element;
        }

        if (element.TryGetProperty("object", out var objectElement))
        {
            foreach (var workRef in EnumerateWorkRefElements(objectElement))
            {
                yield return workRef;
            }
        }

        if (element.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                foreach (var workRef in EnumerateWorkRefElements(item))
                {
                    yield return workRef;
                }
            }
        }

        if (element.TryGetProperty("orderedItems", out var orderedItemsElement) && orderedItemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in orderedItemsElement.EnumerateArray())
            {
                foreach (var workRef in EnumerateWorkRefElements(item))
                {
                    yield return workRef;
                }
            }
        }
    }

    private static bool IsWorkRefElement(JsonElement element)
    {
        return element.TryGetProperty("type", out var typeElement) &&
            typeElement.ValueKind == JsonValueKind.String &&
            string.Equals(typeElement.GetString(), "WorkRef", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecommendable(WorkRef workRef)
    {
        return string.Equals(workRef.Domain, "music", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(workRef.Title) &&
            workRef.ValidateSecurity();
    }

    private static string BuildWorkKey(WorkRef workRef)
    {
        if (workRef.ExternalIds.TryGetValue("musicbrainz", out var musicBrainzId) &&
            !string.IsNullOrWhiteSpace(musicBrainzId))
        {
            return $"mb:{musicBrainzId.Trim()}";
        }

        var creator = NormalizeKeyPart(workRef.Creator);
        var title = NormalizeKeyPart(workRef.Title);
        var year = workRef.Year?.ToString() ?? string.Empty;
        return $"text:{creator}:{title}:{year}";
    }

    private static string NormalizeKeyPart(string? value)
    {
        return string.Join(' ', (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static double RecencyScore(DateTimeOffset lastSeen)
    {
        var ageDays = Math.Max(0, (DateTimeOffset.UtcNow - lastSeen).TotalDays);
        return Math.Max(0, 5 - ageDays);
    }

    private static List<string> BuildReasons(WorkRef workRef, int sourceCount)
    {
        var reasons = new List<string>
        {
            $"appeared in {sourceCount} trusted federated music libraries",
        };

        if (!string.IsNullOrWhiteSpace(workRef.Creator))
        {
            reasons.Add($"near your federated music graph for {workRef.Creator}");
        }

        if (workRef.ExternalIds.ContainsKey("musicbrainz"))
        {
            reasons.Add("has MusicBrainz identity evidence");
        }

        return reasons;
    }

    private async Task AddGraphEvidenceAsync(
        IReadOnlyList<TasteRecommendation> recommendations,
        CancellationToken cancellationToken)
    {
        foreach (var recommendation in recommendations)
        {
            try
            {
                var graph = await _discoveryGraphService!.BuildAsync(BuildGraphRequest(recommendation.WorkRef), cancellationToken).ConfigureAwait(false);
                var proximityScore = CalculateGraphProximityScore(graph);
                recommendation.GraphEvidence = new TasteRecommendationGraphEvidence
                {
                    SeedNodeId = graph.SeedNodeId,
                    NodeCount = graph.Nodes.Count,
                    EdgeCount = graph.Edges.Count,
                    ProximityScore = proximityScore,
                    Reasons = BuildGraphReasons(graph),
                };
                recommendation.Score = Math.Round(recommendation.Score + proximityScore, 3);
                recommendation.Reasons.AddRange(recommendation.GraphEvidence.Reasons);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(ex, "[TasteRecommendations] Discovery Graph evidence unavailable for {Title}", recommendation.WorkRef.Title);
            }
        }
    }

    private static DiscoveryGraphRequest BuildGraphRequest(WorkRef workRef)
    {
        return new DiscoveryGraphRequest
        {
            Scope = !string.IsNullOrWhiteSpace(GetExternalId(workRef, "musicbrainz_artist")) ||
                !string.IsNullOrWhiteSpace(workRef.Creator)
                    ? "artist"
                    : "track",
            ArtistId = GetExternalId(workRef, "musicbrainz_artist"),
            RecordingId = GetExternalId(workRef, "musicbrainz"),
            Title = workRef.Title,
            Artist = workRef.Creator,
        };
    }

    private static double CalculateGraphProximityScore(DiscoveryGraphResult graph)
    {
        var nodeScore = Math.Min(8, graph.Nodes.Count * 0.75);
        var edgeScore = Math.Min(7, graph.Edges.Count);
        return Math.Round(nodeScore + edgeScore, 3);
    }

    private static List<string> BuildGraphReasons(DiscoveryGraphResult graph)
    {
        var reasons = new List<string>();
        if (graph.Nodes.Count > 0)
        {
            reasons.Add($"near a Discovery Graph neighborhood with {graph.Nodes.Count} nodes");
        }

        if (graph.Edges.Count > 0)
        {
            reasons.Add($"connected by {graph.Edges.Count} graph evidence edges");
        }

        return reasons;
    }

    private static string BuildSearchText(WorkRef workRef)
    {
        return string.Join(' ', new[] { workRef.Creator, workRef.Title }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
    }

    private static string BuildWishlistFilter(WorkRef workRef, string? note)
    {
        var metadata = new List<string> { "source:taste-recommendation", "review-only" };
        var musicBrainzRecordingId = GetExternalId(workRef, "musicbrainz");
        if (!string.IsNullOrWhiteSpace(musicBrainzRecordingId))
        {
            metadata.Add($"mbid:{musicBrainzRecordingId}");
        }

        var artistId = GetExternalId(workRef, "musicbrainz_artist");
        if (!string.IsNullOrWhiteSpace(artistId))
        {
            metadata.Add($"artist-mbid:{artistId}");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            metadata.Add($"note:{note.Trim()}");
        }

        return string.Join("; ", metadata);
    }

    private static string? GetExternalId(WorkRef workRef, string key)
    {
        return workRef.ExternalIds.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static string? NormalizeArtistId(string? artistId)
    {
        return string.IsNullOrWhiteSpace(artistId) ? null : artistId.Trim();
    }
}
