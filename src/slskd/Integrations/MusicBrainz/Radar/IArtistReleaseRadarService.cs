// <copyright file="IArtistReleaseRadarService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Radar;

public interface IArtistReleaseRadarService
{
    Task<ArtistRadarSubscription> SubscribeAsync(
        ArtistRadarSubscription subscription,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtistRadarSubscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task<ArtistRadarObservationResult> RecordObservationAsync(
        ArtistRadarObservation observation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtistRadarNotification>> GetNotificationsAsync(
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);
}
