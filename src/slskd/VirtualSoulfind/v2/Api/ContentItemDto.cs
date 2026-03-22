// <copyright file="ContentItemDto.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
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
