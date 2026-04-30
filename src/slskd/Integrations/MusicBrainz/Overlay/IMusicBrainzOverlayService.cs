// <copyright file="IMusicBrainzOverlayService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Overlay;

using slskd.Integrations.MusicBrainz.Models;

public interface IMusicBrainzOverlayService
{
    Task<MusicBrainzOverlayValidationResult> StoreAsync(
        MusicBrainzOverlayEdit edit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MusicBrainzOverlayEdit>> GetEditsForTargetAsync(
        MusicBrainzOverlayTargetType targetType,
        string targetId,
        CancellationToken cancellationToken = default);

    Task<MusicBrainzOverlayApplication<ArtistReleaseGraph>> ApplyToArtistReleaseGraphAsync(
        ArtistReleaseGraph graph,
        CancellationToken cancellationToken = default);
}
