// <copyright file="LibraryBloomDiffService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Bloom;

using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.PodCore;
using slskd.Wishlist;

public sealed class LibraryBloomDiffService : ILibraryBloomDiffService
{
    private const int CurrentVersion = 1;
    private const string RecordingNamespace = "musicbrainz:recording";
    private const string ReleaseNamespace = "musicbrainz:release";

    private readonly IHashDbService _hashDb;
    private readonly IWishlistService _wishlistService;

    public LibraryBloomDiffService(
        IHashDbService hashDb,
        IWishlistService wishlistService)
    {
        _hashDb = hashDb;
        _wishlistService = wishlistService;
    }

    public async Task<LibraryBloomSnapshot> CreateSnapshotAsync(
        LibraryBloomSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var expectedItems = Math.Max(16, request.ExpectedItems);
        var falsePositiveRate = request.FalsePositiveRate <= 0 || request.FalsePositiveRate >= 1
            ? 0.01
            : request.FalsePositiveRate;
        var saltId = string.IsNullOrWhiteSpace(request.SaltId)
            ? $"salt:{Guid.NewGuid():N}"
            : request.SaltId.Trim();

        var items = await GetLocalHeldItemsAsync(cancellationToken).ConfigureAwait(false);
        var filter = new BloomFilter(Math.Max(expectedItems, items.Count), falsePositiveRate);
        foreach (var item in items)
        {
            filter.Add(BuildSaltedItem(saltId, item.Namespace, item.Mbid));
        }

        return new LibraryBloomSnapshot
        {
            Version = CurrentVersion,
            SnapshotId = $"library-bloom:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}:{Guid.NewGuid():N}",
            SaltId = saltId,
            CreatedAt = DateTimeOffset.UtcNow,
            RotatesAt = request.RotatesAt ?? DateTimeOffset.UtcNow.AddDays(7),
            ExpectedItems = filter.ExpectedItems,
            FalsePositiveRate = filter.FalsePositiveRate,
            BitSize = filter.Size,
            HashFunctionCount = filter.HashFunctionCount,
            ItemCount = filter.ItemCount,
            FillRatio = filter.FillRatio,
            BitsBase64 = filter.ToBase64(),
            NamespaceItemCounts = items
                .GroupBy(item => item.Namespace, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            PrivacyNotes =
            {
                "Snapshot contains salted Bloom-filter membership only; it does not include filenames, paths, file hashes, or exact item identifiers.",
                "Bloom matches are probabilistic and must be treated as likely suggestions, not proof of remote holdings.",
                "Rotate SaltId before long-lived publication to reduce cross-snapshot correlation.",
            },
        };
    }

    public async Task<LibraryBloomDiffResult> CompareAsync(
        LibraryBloomDiffRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = ValidateSnapshot(request.Snapshot);
        if (!result.IsCompatible)
        {
            return result;
        }

        var filter = BloomFilter.FromBase64(
            request.Snapshot.ExpectedItems,
            request.Snapshot.FalsePositiveRate,
            request.Snapshot.BitsBase64,
            request.Snapshot.ItemCount);
        var wishlistKeys = await GetWishlistKeysAsync().ConfigureAwait(false);
        var heldRecordings = (await _hashDb.GetRecordingIdsWithVariantsAsync(cancellationToken).ConfigureAwait(false))
            .Select(NormalizeMbid)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 250);

        foreach (var candidate in await GetCandidateTracksAsync(cancellationToken).ConfigureAwait(false))
        {
            if (result.Suggestions.Count >= limit)
            {
                break;
            }

            if (heldRecordings.Contains(candidate.RecordingId))
            {
                continue;
            }

            var recordingMatches = filter.Contains(BuildSaltedItem(request.Snapshot.SaltId, RecordingNamespace, candidate.RecordingId));
            var releaseMatches = filter.Contains(BuildSaltedItem(request.Snapshot.SaltId, ReleaseNamespace, candidate.ReleaseId));
            if (!recordingMatches && !releaseMatches)
            {
                continue;
            }

            var searchText = BuildSearchText(candidate.Artist, candidate.TrackTitle);
            result.Suggestions.Add(new LibraryBloomDiffSuggestion
            {
                Mbid = candidate.RecordingId,
                ReleaseId = candidate.ReleaseId,
                ReleaseTitle = candidate.ReleaseTitle,
                TrackTitle = candidate.TrackTitle,
                Artist = candidate.Artist,
                SearchText = searchText,
                Confidence = recordingMatches ? 0.70 : 0.55,
                Reason = recordingMatches
                    ? "Remote Bloom filter probably contains this recording MBID; false positives are possible."
                    : "Remote Bloom filter probably contains this release MBID; inspect before promoting.",
                WishlistSeeded = wishlistKeys.Contains(NormalizeWishlistKey(searchText, "flac")) ||
                    wishlistKeys.Any(key => key.StartsWith(NormalizeSearchText(searchText) + "\u001f", StringComparison.OrdinalIgnoreCase)),
            });
        }

        return result;
    }

    public async Task<LibraryBloomWishlistPromotionResult> PromoteSuggestionsToWishlistAsync(
        LibraryBloomWishlistPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var diff = await CompareAsync(request.DiffRequest, cancellationToken).ConfigureAwait(false);
        var result = new LibraryBloomWishlistPromotionResult();
        var filter = string.IsNullOrWhiteSpace(request.Filter) ? "flac" : request.Filter.Trim();
        var existingKeys = await GetWishlistKeysAsync().ConfigureAwait(false);
        var createdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var suggestion in diff.Suggestions)
        {
            var key = NormalizeWishlistKey(suggestion.SearchText, filter);
            if (suggestion.WishlistSeeded || existingKeys.Contains(key) || !createdKeys.Add(key))
            {
                result.AlreadySeededCount++;
                continue;
            }

            var created = await _wishlistService.CreateAsync(new WishlistItem
            {
                SearchText = suggestion.SearchText,
                Filter = filter,
                Enabled = true,
                AutoDownload = false,
                MaxResults = request.MaxResults <= 0 ? 100 : request.MaxResults,
            }).ConfigureAwait(false);

            result.CreatedCount++;
            result.CreatedItemIds.Add(created.Id);
            existingKeys.Add(key);
        }

        return result;
    }

    private async Task<List<LibraryBloomItem>> GetLocalHeldItemsAsync(CancellationToken cancellationToken)
    {
        var items = new List<LibraryBloomItem>();
        var recordingIds = await _hashDb.GetRecordingIdsWithVariantsAsync(cancellationToken).ConfigureAwait(false);
        items.AddRange(recordingIds
            .Select(NormalizeMbid)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new LibraryBloomItem(RecordingNamespace, id)));

        foreach (var release in await _hashDb.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false))
        {
            var tracks = (await _hashDb.GetAlbumTracksAsync(release.ReleaseId, cancellationToken).ConfigureAwait(false)).ToList();
            if (tracks.Count == 0)
            {
                continue;
            }

            var hasHeldTrack = tracks.Any(track => items.Any(item =>
                string.Equals(item.Namespace, RecordingNamespace, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Mbid, NormalizeMbid(track.RecordingId), StringComparison.OrdinalIgnoreCase)));
            if (hasHeldTrack)
            {
                items.Add(new LibraryBloomItem(ReleaseNamespace, NormalizeMbid(release.ReleaseId)));
            }
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Mbid))
            .DistinctBy(item => $"{item.Namespace}\u001f{item.Mbid}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<LibraryBloomCandidate>> GetCandidateTracksAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<LibraryBloomCandidate>();
        foreach (var release in await _hashDb.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var track in await _hashDb.GetAlbumTracksAsync(release.ReleaseId, cancellationToken).ConfigureAwait(false))
            {
                var recordingId = NormalizeMbid(track.RecordingId);
                if (string.IsNullOrWhiteSpace(recordingId))
                {
                    continue;
                }

                candidates.Add(new LibraryBloomCandidate(
                    ReleaseId: NormalizeMbid(release.ReleaseId),
                    ReleaseTitle: release.Title.Trim(),
                    RecordingId: recordingId,
                    TrackTitle: track.Title.Trim(),
                    Artist: string.IsNullOrWhiteSpace(track.Artist) ? release.Artist.Trim() : track.Artist.Trim()));
            }
        }

        return candidates
            .DistinctBy(candidate => candidate.RecordingId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ReleaseTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.TrackTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<HashSet<string>> GetWishlistKeysAsync()
    {
        var items = await _wishlistService.ListAsync().ConfigureAwait(false);
        return items
            .Select(item => NormalizeWishlistKey(item.SearchText, item.Filter))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static LibraryBloomDiffResult ValidateSnapshot(LibraryBloomSnapshot snapshot)
    {
        var result = new LibraryBloomDiffResult
        {
            SnapshotId = snapshot.SnapshotId,
            Version = snapshot.Version,
            IsCompatible = snapshot.Version == CurrentVersion,
        };

        if (snapshot.Version != CurrentVersion)
        {
            result.Warnings.Add("Unsupported library Bloom snapshot version.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.SaltId))
        {
            result.IsCompatible = false;
            result.Warnings.Add("Snapshot salt id is required.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.BitsBase64))
        {
            result.IsCompatible = false;
            result.Warnings.Add("Snapshot Bloom bits are required.");
        }

        return result;
    }

    private static string BuildSaltedItem(string saltId, string ns, string mbid)
    {
        return $"{saltId.Trim()}\u001f{ns.Trim().ToLowerInvariant()}\u001f{NormalizeMbid(mbid)}";
    }

    private static string NormalizeMbid(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string BuildSearchText(string artist, string title)
    {
        var normalizedArtist = artist.Trim();
        var normalizedTitle = title.Trim();
        return string.IsNullOrWhiteSpace(normalizedArtist) ? normalizedTitle : $"{normalizedArtist} {normalizedTitle}";
    }

    private static string NormalizeWishlistKey(string searchText, string filter) =>
        NormalizeSearchText(searchText) + "\u001f" + (filter ?? string.Empty).Trim();

    private static string NormalizeSearchText(string searchText) => (searchText ?? string.Empty).Trim();

    private sealed record LibraryBloomItem(string Namespace, string Mbid);

    private sealed record LibraryBloomCandidate(
        string ReleaseId,
        string ReleaseTitle,
        string RecordingId,
        string TrackTitle,
        string Artist);
}
