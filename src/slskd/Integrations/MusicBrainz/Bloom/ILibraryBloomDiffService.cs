// <copyright file="ILibraryBloomDiffService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Bloom;

public interface ILibraryBloomDiffService
{
    Task<LibraryBloomSnapshot> CreateSnapshotAsync(
        LibraryBloomSnapshotRequest request,
        CancellationToken cancellationToken = default);

    Task<LibraryBloomDiffResult> CompareAsync(
        LibraryBloomDiffRequest request,
        CancellationToken cancellationToken = default);

    Task<LibraryBloomWishlistPromotionResult> PromoteSuggestionsToWishlistAsync(
        LibraryBloomWishlistPromotionRequest request,
        CancellationToken cancellationToken = default);
}
