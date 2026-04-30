// <copyright file="TasteRecommendationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

using System.Text.Json;
using Microsoft.Extensions.Logging;

public sealed class TasteRecommendationService : ITasteRecommendationService
{
    private const int MaxInboxActivities = 250;
    private const int MaxRecommendationLimit = 100;

    private readonly IActivityPubInboxStore _inboxStore;
    private readonly IActivityPubRelationshipStore _relationshipStore;
    private readonly ILogger<TasteRecommendationService> _logger;

    public TasteRecommendationService(
        IActivityPubInboxStore inboxStore,
        IActivityPubRelationshipStore relationshipStore,
        ILogger<TasteRecommendationService> logger)
    {
        _inboxStore = inboxStore;
        _relationshipStore = relationshipStore;
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

        return BuildRecommendations(observations, trustedActorSet.Count, minimumTrustedSources, limit, request.IncludeSourceActors);
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
}
