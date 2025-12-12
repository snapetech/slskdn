// <copyright file="IVirtualCatalogueStore.cs" company="slskd Team">
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

namespace slskd.VirtualSoulfind.v2.Catalogue
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for the virtual catalogue store.
    /// </summary>
    /// <remarks>
    ///     T-V2-P1-03: Virtual Catalogue Store.
    ///     The catalogue tracks artists, releases, and tracks as "ideal" metadata.
    /// </remarks>
    public interface IVirtualCatalogueStore
    {
        // Artist operations
        Task UpsertArtistAsync(VirtualArtist artist, CancellationToken cancellationToken = default);
        Task<VirtualArtist?> FindArtistByIdAsync(string artistId, CancellationToken cancellationToken = default);
        Task<VirtualArtist?> FindArtistByNameAsync(string normalizedName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VirtualArtist>> ListArtistsAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default);

        // Release operations
        Task UpsertReleaseAsync(VirtualRelease release, CancellationToken cancellationToken = default);
        Task<VirtualRelease?> FindReleaseByIdAsync(string releaseId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VirtualRelease>> FindReleasesByArtistAsync(string artistId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VirtualRelease>> ListReleasesAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default);

        // Track operations
        Task UpsertTrackAsync(VirtualTrack track, CancellationToken cancellationToken = default);
        Task<VirtualTrack?> FindTrackByIdAsync(string trackId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VirtualTrack>> FindTracksByReleaseAsync(string releaseId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VirtualTrack>> FindTracksByContentItemIdAsync(string contentItemId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VirtualTrack>> ListTracksAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default);

        // Statistics
        Task<int> CountArtistsAsync(CancellationToken cancellationToken = default);
        Task<int> CountReleasesAsync(CancellationToken cancellationToken = default);
        Task<int> CountTracksAsync(CancellationToken cancellationToken = default);
    }
}
