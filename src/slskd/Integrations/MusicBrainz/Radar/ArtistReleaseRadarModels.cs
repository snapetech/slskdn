// <copyright file="ArtistReleaseRadarModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Radar;

using slskd.SocialFederation;

public sealed class ArtistRadarSubscription
{
    public string Id { get; set; } = string.Empty;

    public string ArtistId { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public string Scope { get; set; } = "trusted";

    public bool Enabled { get; set; } = true;

    public List<string> MutedReleaseGroupIds { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ArtistRadarObservation
{
    public string ArtistId { get; set; } = string.Empty;

    public string RecordingId { get; set; } = string.Empty;

    public string? ReleaseId { get; set; }

    public string? ReleaseGroupId { get; set; }

    public string SourceRealm { get; set; } = string.Empty;

    public string SourceActor { get; set; } = string.Empty;

    public bool SongIdConfirmed { get; set; }

    public double Confidence { get; set; }

    public WorkRef WorkRef { get; set; } = new();

    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Signature { get; set; }
}

public sealed class ArtistRadarNotification
{
    public string Id { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public string ArtistId { get; set; } = string.Empty;

    public string RecordingId { get; set; } = string.Empty;

    public string? ReleaseId { get; set; }

    public string? ReleaseGroupId { get; set; }

    public string SourceRealm { get; set; } = string.Empty;

    public string SourceActor { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public WorkRef WorkRef { get; set; } = new();

    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public bool Read { get; set; }
}

public sealed class ArtistRadarObservationResult
{
    public bool Accepted { get; set; }

    public List<ArtistRadarNotification> Notifications { get; set; } = new();

    public string? RejectionReason { get; set; }
}

public sealed class ArtistRadarRouteRequest
{
    public List<string> TargetPeerIds { get; set; } = new();

    public string? PodId { get; set; }

    public string? ChannelId { get; set; }

    public string? SenderPeerId { get; set; }
}

public sealed class ArtistRadarRouteAttempt
{
    public string Id { get; set; } = string.Empty;

    public string NotificationId { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public string PodId { get; set; } = string.Empty;

    public string ChannelId { get; set; } = string.Empty;

    public List<string> TargetPeerIds { get; set; } = new();

    public List<string> RoutedPeerIds { get; set; } = new();

    public List<string> FailedPeerIds { get; set; } = new();

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
