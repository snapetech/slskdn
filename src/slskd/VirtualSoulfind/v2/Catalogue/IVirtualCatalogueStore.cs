// <copyright file="IVirtualCatalogueStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
