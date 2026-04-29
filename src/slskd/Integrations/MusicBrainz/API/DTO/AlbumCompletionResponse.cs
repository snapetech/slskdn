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
        public string ReleaseId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string ReleaseDate { get; set; } = string.Empty;

        public string DiscogsReleaseId { get; set; } = string.Empty;

        public int TotalTracks { get; set; }

        public int CompletedTracks { get; set; }

        public AlbumCompletionTrack[] Tracks { get; set; } = Array.Empty<AlbumCompletionTrack>();
    }

    public sealed class AlbumCompletionTrack
    {
        public int Position { get; set; }

        public string Title { get; set; } = string.Empty;

        public string RecordingId { get; set; } = string.Empty;

        public int? DurationMs { get; set; }

        public bool Complete { get; set; }

        public HashMatch[] Matches { get; set; } = Array.Empty<HashMatch>();
    }

    public sealed class HashMatch
    {
        public string FlacKey { get; set; } = string.Empty;

        public long Size { get; set; }

        public int UseCount { get; set; }

        public long FirstSeenAt { get; set; }

        public long LastUpdatedAt { get; set; }
    }
}
