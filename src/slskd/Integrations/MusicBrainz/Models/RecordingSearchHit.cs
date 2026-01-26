// <copyright file="RecordingSearchHit.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz.Models
{
    /// <summary>
    ///     A single recording from a MusicBrainz recording search. T-912 Metadata facade.
    /// </summary>
    public sealed record RecordingSearchHit(
        string RecordingId,
        string Title,
        string Artist,
        string? MusicBrainzArtistId);

    // IMusicBrainzClient.SearchRecordingsAsync returns IReadOnlyList<RecordingSearchHit>
}
