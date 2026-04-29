// <copyright file="ContentItemDto.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Api
{
    /// <summary>
    ///     DTO for API responses (catalogue browsing, library dashboards).
    /// </summary>
    public sealed class TrackDto
    {
        public string TrackId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public int TrackNumber { get; init; }
        public int? DurationSeconds { get; init; }
        public string? MusicBrainzRecordingId { get; init; }
        public bool IsAvailableLocally { get; init; }
        public int? LocalQuality { get; init; }
    }

    public sealed class ReleaseDto
    {
        public string ReleaseId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public int? Year { get; init; }
        public string? Country { get; init; }
        public int TrackCount { get; init; }
        public int TracksAvailableLocally { get; init; }
    }

    public sealed class ArtistDto
    {
        public string ArtistId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? SortName { get; init; }
        public int ReleaseCount { get; init; }
    }
}
