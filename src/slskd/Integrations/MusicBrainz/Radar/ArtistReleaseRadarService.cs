// <copyright file="ArtistReleaseRadarService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Radar;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public sealed class ArtistReleaseRadarService : IArtistReleaseRadarService
{
    private const double MinimumConfidence = 0.8;

    private readonly ConcurrentDictionary<string, ArtistRadarSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ArtistRadarNotification> _notifications = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenObservationKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly ILogger<ArtistReleaseRadarService> _logger;

    public ArtistReleaseRadarService(ILogger<ArtistReleaseRadarService> logger)
    {
        _logger = logger;
    }

    public Task<ArtistRadarSubscription> SubscribeAsync(
        ArtistRadarSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        subscription.ArtistId = subscription.ArtistId.Trim();
        subscription.ArtistName = subscription.ArtistName?.Trim() ?? string.Empty;
        subscription.Scope = string.IsNullOrWhiteSpace(subscription.Scope) ? "trusted" : subscription.Scope.Trim();
        subscription.Id = string.IsNullOrWhiteSpace(subscription.Id)
            ? $"artist-radar:{subscription.ArtistId.ToLowerInvariant()}"
            : subscription.Id.Trim();
        subscription.CreatedAt = subscription.CreatedAt == default ? DateTimeOffset.UtcNow : subscription.CreatedAt;

        _subscriptions[subscription.Id] = subscription;
        _logger.LogInformation("[ArtistRadar] Subscribed to artist {ArtistId} with scope {Scope}", subscription.ArtistId, subscription.Scope);
        return Task.FromResult(subscription);
    }

    public Task<IReadOnlyList<ArtistRadarSubscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = _subscriptions.Values
            .OrderBy(subscription => subscription.ArtistName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(subscription => subscription.ArtistId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<ArtistRadarSubscription>>(subscriptions);
    }

    public Task<ArtistRadarObservationResult> RecordObservationAsync(
        ArtistRadarObservation observation,
        CancellationToken cancellationToken = default)
    {
        var rejectionReason = GetRejectionReason(observation);
        if (rejectionReason != null)
        {
            return Task.FromResult(new ArtistRadarObservationResult
            {
                Accepted = false,
                RejectionReason = rejectionReason,
            });
        }

        var notifications = new List<ArtistRadarNotification>();
        lock (_sync)
        {
            foreach (var subscription in MatchingSubscriptions(observation))
            {
                var observationKey = BuildObservationKey(subscription, observation);
                if (!_seenObservationKeys.Add(observationKey))
                {
                    continue;
                }

                var notification = new ArtistRadarNotification
                {
                    Id = $"artist-radar-notification:{Guid.NewGuid():N}",
                    SubscriptionId = subscription.Id,
                    ArtistId = observation.ArtistId.Trim(),
                    RecordingId = observation.RecordingId.Trim(),
                    ReleaseId = string.IsNullOrWhiteSpace(observation.ReleaseId) ? null : observation.ReleaseId.Trim(),
                    ReleaseGroupId = string.IsNullOrWhiteSpace(observation.ReleaseGroupId) ? null : observation.ReleaseGroupId.Trim(),
                    SourceRealm = observation.SourceRealm?.Trim() ?? string.Empty,
                    SourceActor = observation.SourceActor?.Trim() ?? string.Empty,
                    Confidence = observation.Confidence,
                    WorkRef = observation.WorkRef,
                    FirstSeenAt = observation.ObservedAt == default ? DateTimeOffset.UtcNow : observation.ObservedAt,
                };

                _notifications[notification.Id] = notification;
                notifications.Add(notification);
            }
        }

        return Task.FromResult(new ArtistRadarObservationResult
        {
            Accepted = true,
            Notifications = notifications,
        });
    }

    public Task<IReadOnlyList<ArtistRadarNotification>> GetNotificationsAsync(
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var notifications = _notifications.Values
            .Where(notification => !unreadOnly || !notification.Read)
            .OrderByDescending(notification => notification.FirstSeenAt)
            .ThenBy(notification => notification.ArtistId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ArtistRadarNotification>>(notifications);
    }

    private static string? GetRejectionReason(ArtistRadarObservation observation)
    {
        if (!observation.SongIdConfirmed)
        {
            return "Observation is not SongID-confirmed.";
        }

        if (observation.Confidence < MinimumConfidence)
        {
            return "Observation confidence is below the release radar threshold.";
        }

        if (string.IsNullOrWhiteSpace(observation.ArtistId))
        {
            return "Artist MBID is required.";
        }

        if (string.IsNullOrWhiteSpace(observation.RecordingId))
        {
            return "Recording MBID is required.";
        }

        if (!string.Equals(observation.WorkRef.Domain, "music", StringComparison.OrdinalIgnoreCase))
        {
            return "WorkRef domain must be music.";
        }

        if (string.IsNullOrWhiteSpace(observation.WorkRef.Title))
        {
            return "WorkRef title is required.";
        }

        if (!observation.WorkRef.ValidateSecurity())
        {
            return "WorkRef is unsafe.";
        }

        return null;
    }

    private IEnumerable<ArtistRadarSubscription> MatchingSubscriptions(ArtistRadarObservation observation)
    {
        return _subscriptions.Values
            .Where(subscription => subscription.Enabled)
            .Where(subscription => string.Equals(subscription.ArtistId, observation.ArtistId, StringComparison.OrdinalIgnoreCase))
            .Where(subscription => !IsReleaseGroupMuted(subscription, observation.ReleaseGroupId))
            .Where(subscription => ScopeMatches(subscription, observation));
    }

    private static bool IsReleaseGroupMuted(ArtistRadarSubscription subscription, string? releaseGroupId)
    {
        return !string.IsNullOrWhiteSpace(releaseGroupId) &&
            subscription.MutedReleaseGroupIds.Contains(releaseGroupId, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ScopeMatches(ArtistRadarSubscription subscription, ArtistRadarObservation observation)
    {
        if (string.Equals(subscription.Scope, "trusted", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (subscription.Scope.StartsWith("realm:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(subscription.Scope[6..], observation.SourceRealm, StringComparison.OrdinalIgnoreCase);
        }

        if (subscription.Scope.StartsWith("actor:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(subscription.Scope[6..], observation.SourceActor, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string BuildObservationKey(ArtistRadarSubscription subscription, ArtistRadarObservation observation)
    {
        return string.Join(
            ':',
            subscription.Id.Trim().ToLowerInvariant(),
            observation.ArtistId.Trim().ToLowerInvariant(),
            observation.RecordingId.Trim().ToLowerInvariant(),
            observation.SourceRealm?.Trim().ToLowerInvariant() ?? string.Empty);
    }
}
