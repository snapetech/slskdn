// <copyright file="ArtistReleaseRadarService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Radar;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using slskd.PodCore;

public sealed class ArtistReleaseRadarService : IArtistReleaseRadarService
{
    private const double MinimumConfidence = 0.8;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentDictionary<string, ArtistRadarSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ArtistRadarNotification> _notifications = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ArtistRadarRouteAttempt> _routeAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenObservationKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly object _storageSync = new();
    private readonly string _storagePath;
    private readonly IPodMessageRouter? _messageRouter;
    private readonly ILogger<ArtistReleaseRadarService> _logger;

    public ArtistReleaseRadarService(ILogger<ArtistReleaseRadarService> logger)
        : this(
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "artist-release-radar.json"))
    {
    }

    public ArtistReleaseRadarService(ILogger<ArtistReleaseRadarService> logger, IPodMessageRouter messageRouter)
        : this(
            logger,
            Path.Combine(
                string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                    ? global::slskd.Program.DefaultAppDirectory
                    : global::slskd.Program.AppDirectory,
                "artist-release-radar.json"),
            messageRouter)
    {
    }

    public ArtistReleaseRadarService(ILogger<ArtistReleaseRadarService> logger, string storagePath)
        : this(logger, storagePath, null)
    {
    }

    public ArtistReleaseRadarService(
        ILogger<ArtistReleaseRadarService> logger,
        string storagePath,
        IPodMessageRouter? messageRouter)
    {
        _logger = logger;
        _storagePath = storagePath;
        _messageRouter = messageRouter;
        LoadState();
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
        PersistState();
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

        if (notifications.Count > 0)
        {
            PersistState();
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

    public async Task<ArtistRadarRouteAttempt> RouteNotificationAsync(
        string notificationId,
        ArtistRadarRouteRequest routeRequest,
        CancellationToken cancellationToken = default)
    {
        notificationId = notificationId.Trim();
        routeRequest ??= new ArtistRadarRouteRequest();
        if (!_notifications.TryGetValue(notificationId, out var notification))
        {
            return StoreRouteAttempt(BuildRouteAttempt(notificationId, routeRequest, Array.Empty<string>(), success: false, "Notification not found."));
        }

        if (HasUnsafeRouteMetadata(routeRequest))
        {
            return StoreRouteAttempt(BuildRouteAttempt(notificationId, routeRequest, Array.Empty<string>(), success: false, "Route metadata must be opaque and safe."));
        }

        var targetPeerIds = NormalizeRouteTargets(routeRequest.TargetPeerIds);
        if (targetPeerIds.Count == 0)
        {
            return StoreRouteAttempt(BuildRouteAttempt(notificationId, routeRequest, targetPeerIds, success: false, "At least one target peer is required."));
        }

        if (targetPeerIds.Any(peerId => !IsSafeOpaqueReference(peerId)))
        {
            return StoreRouteAttempt(BuildRouteAttempt(notificationId, routeRequest, targetPeerIds, success: false, "Route targets must be opaque and safe."));
        }

        if (_messageRouter == null)
        {
            return StoreRouteAttempt(BuildRouteAttempt(notificationId, routeRequest, targetPeerIds, success: false, "Routing backend is not available."));
        }

        var message = BuildRouteMessage(notification, routeRequest, targetPeerIds);
        var routingResult = await _messageRouter.RouteMessageToPeersAsync(message, targetPeerIds, cancellationToken).ConfigureAwait(false);
        var failedPeerIds = routingResult.FailedPeerIds?.ToList() ?? new List<string>();
        var routedPeerIds = targetPeerIds
            .Where(peerId => !failedPeerIds.Contains(peerId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return StoreRouteAttempt(new ArtistRadarRouteAttempt
        {
            Id = $"artist-radar-route:{Guid.NewGuid():N}",
            NotificationId = notification.Id,
            MessageId = message.MessageId,
            PodId = message.PodId,
            ChannelId = message.ChannelId,
            TargetPeerIds = targetPeerIds,
            RoutedPeerIds = routedPeerIds,
            FailedPeerIds = failedPeerIds,
            Success = routingResult.Success,
            ErrorMessage = routingResult.ErrorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public Task<IReadOnlyList<ArtistRadarRouteAttempt>> GetRouteAttemptsAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        notificationId = notificationId.Trim();
        var attempts = _routeAttempts.Values
            .Where(attempt => string.Equals(attempt.NotificationId, notificationId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenBy(attempt => attempt.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<ArtistRadarRouteAttempt>>(attempts);
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

    private static List<string> NormalizeRouteTargets(IEnumerable<string> targetPeerIds)
    {
        return targetPeerIds
            .Select(target => target.Trim())
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasUnsafeRouteMetadata(ArtistRadarRouteRequest routeRequest)
    {
        return (!string.IsNullOrWhiteSpace(routeRequest.SenderPeerId) && !IsSafeOpaqueReference(routeRequest.SenderPeerId)) ||
            (!string.IsNullOrWhiteSpace(routeRequest.PodId) && !IsSafeOpaqueReference(routeRequest.PodId)) ||
            (!string.IsNullOrWhiteSpace(routeRequest.ChannelId) && !IsSafeOpaqueReference(routeRequest.ChannelId));
    }

    private static bool IsSafeOpaqueReference(string value)
    {
        return value.Length <= 256 &&
            value.All(character => char.IsLetterOrDigit(character) ||
                character == '-' ||
                character == '_' ||
                character == ':' ||
                character == '.' ||
                character == '@');
    }

    private static PodMessage BuildRouteMessage(
        ArtistRadarNotification notification,
        ArtistRadarRouteRequest routeRequest,
        IReadOnlyList<string> targetPeerIds)
    {
        var envelope = new ArtistRadarRouteEnvelope
        {
            Notification = notification,
            TargetPeerIds = targetPeerIds.ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return new PodMessage
        {
            MessageId = $"artist-radar-observation:{Guid.NewGuid():N}",
            PodId = string.IsNullOrWhiteSpace(routeRequest.PodId) ? "artist-release-radar" : routeRequest.PodId.Trim(),
            ChannelId = string.IsNullOrWhiteSpace(routeRequest.ChannelId) ? $"notification:{notification.Id}" : routeRequest.ChannelId.Trim(),
            SenderPeerId = string.IsNullOrWhiteSpace(routeRequest.SenderPeerId) ? "local-artist-release-radar" : routeRequest.SenderPeerId.Trim(),
            Body = JsonSerializer.Serialize(envelope, JsonOptions),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "local-artist-release-radar-route",
        };
    }

    private static ArtistRadarRouteAttempt BuildRouteAttempt(
        string notificationId,
        ArtistRadarRouteRequest routeRequest,
        IReadOnlyList<string> targets,
        bool success,
        string? errorMessage)
    {
        return new ArtistRadarRouteAttempt
        {
            Id = $"artist-radar-route:{Guid.NewGuid():N}",
            NotificationId = notificationId,
            MessageId = string.Empty,
            PodId = string.IsNullOrWhiteSpace(routeRequest.PodId) ? "artist-release-radar" : routeRequest.PodId.Trim(),
            ChannelId = string.IsNullOrWhiteSpace(routeRequest.ChannelId) ? $"notification:{notificationId}" : routeRequest.ChannelId.Trim(),
            TargetPeerIds = targets.ToList(),
            Success = success,
            ErrorMessage = errorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private ArtistRadarRouteAttempt StoreRouteAttempt(ArtistRadarRouteAttempt attempt)
    {
        _routeAttempts[attempt.Id] = attempt;
        PersistState();
        return attempt;
    }

    private void LoadState()
    {
        lock (_storageSync)
        {
            if (!File.Exists(_storagePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_storagePath);
                var state = JsonSerializer.Deserialize<ArtistReleaseRadarStoreState>(json, JsonOptions);
                if (state == null)
                {
                    return;
                }

                foreach (var subscription in state.Subscriptions)
                {
                    _subscriptions[subscription.Id] = subscription;
                }

                foreach (var notification in state.Notifications)
                {
                    _notifications[notification.Id] = notification;
                }

                foreach (var attempt in state.RouteAttempts)
                {
                    _routeAttempts[attempt.Id] = attempt;
                }

                foreach (var observationKey in state.SeenObservationKeys)
                {
                    _seenObservationKeys.Add(observationKey);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "[ArtistRadar] Failed to load persisted release radar state from {Path}", _storagePath);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[ArtistRadar] Failed to parse persisted release radar state from {Path}", _storagePath);
            }
        }
    }

    private void PersistState()
    {
        lock (_storageSync)
        {
            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ArtistReleaseRadarStoreState state;
            lock (_sync)
            {
                state = new ArtistReleaseRadarStoreState
                {
                    Subscriptions = _subscriptions.Values
                        .OrderBy(subscription => subscription.Id, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Notifications = _notifications.Values
                        .OrderBy(notification => notification.Id, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    RouteAttempts = _routeAttempts.Values
                        .OrderBy(attempt => attempt.NotificationId, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(attempt => attempt.CreatedAt)
                        .ToList(),
                    SeenObservationKeys = _seenObservationKeys
                        .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                };
            }

            var tempPath = $"{_storagePath}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tempPath, _storagePath, overwrite: true);
        }
    }

    private sealed class ArtistReleaseRadarStoreState
    {
        public List<ArtistRadarSubscription> Subscriptions { get; set; } = new();

        public List<ArtistRadarNotification> Notifications { get; set; } = new();

        public List<ArtistRadarRouteAttempt> RouteAttempts { get; set; } = new();

        public List<string> SeenObservationKeys { get; set; } = new();
    }

    private sealed class ArtistRadarRouteEnvelope
    {
        public string Type { get; set; } = "slskdn.artist-release-radar.observation.v1";

        public ArtistRadarNotification Notification { get; set; } = new();

        public List<string> TargetPeerIds { get; set; } = new();

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
