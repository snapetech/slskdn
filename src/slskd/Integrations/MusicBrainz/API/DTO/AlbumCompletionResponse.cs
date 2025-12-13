// <copyright file="AlbumCompletionResponse.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz.API.DTO
{
    using System;

    public sealed class AlbumCompletionResponse
    {
        public AlbumCompletionSummary[] Albums { get; set; } = Array.Empty<AlbumCompletionSummary>();
    }

    public sealed class AlbumCompletionSummary
    {
        public string ReleaseId { get; set; }

        public string Title { get; set; }

        public string Artist { get; set; }

        public string ReleaseDate { get; set; }

        public string DiscogsReleaseId { get; set; }

        public int TotalTracks { get; set; }

        public int CompletedTracks { get; set; }

        public AlbumCompletionTrack[] Tracks { get; set; } = Array.Empty<AlbumCompletionTrack>();
    }

    public sealed class AlbumCompletionTrack
    {
        public int Position { get; set; }

        public string Title { get; set; }

        public string RecordingId { get; set; }

        public int? DurationMs { get; set; }

        public bool Complete { get; set; }

        public HashMatch[] Matches { get; set; } = Array.Empty<HashMatch>();
    }

    public sealed class HashMatch
    {
        public string FlacKey { get; set; }

        public long Size { get; set; }

        public int UseCount { get; set; }

        public long FirstSeenAt { get; set; }

        public long LastUpdatedAt { get; set; }
    }
}


















