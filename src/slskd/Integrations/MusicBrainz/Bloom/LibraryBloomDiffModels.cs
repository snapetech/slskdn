// <copyright file="LibraryBloomDiffModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.Bloom;

public sealed class LibraryBloomSnapshotRequest
{
    public int ExpectedItems { get; set; } = 1024;

    public double FalsePositiveRate { get; set; } = 0.01;

    public string SaltId { get; set; } = string.Empty;

    public DateTimeOffset? RotatesAt { get; set; }
}

public sealed class LibraryBloomSnapshot
{
    public int Version { get; set; } = 1;

    public string SnapshotId { get; set; } = string.Empty;

    public string Scope { get; set; } = "manual-preview";

    public string SaltId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset RotatesAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);

    public int ExpectedItems { get; set; }

    public double FalsePositiveRate { get; set; }

    public int BitSize { get; set; }

    public int HashFunctionCount { get; set; }

    public long ItemCount { get; set; }

    public double FillRatio { get; set; }

    public string BitsBase64 { get; set; } = string.Empty;

    public Dictionary<string, int> NamespaceItemCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> PrivacyNotes { get; set; } = new();
}

public sealed class LibraryBloomDiffRequest
{
    public LibraryBloomSnapshot Snapshot { get; set; } = new();

    public int Limit { get; set; } = 50;
}

public sealed class LibraryBloomDiffResult
{
    public string SnapshotId { get; set; } = string.Empty;

    public int Version { get; set; }

    public bool IsCompatible { get; set; }

    public List<string> Warnings { get; set; } = new();

    public List<LibraryBloomDiffSuggestion> Suggestions { get; set; } = new();
}

public sealed class LibraryBloomDiffSuggestion
{
    public string Namespace { get; set; } = "musicbrainz:recording";

    public string Mbid { get; set; } = string.Empty;

    public string ReleaseId { get; set; } = string.Empty;

    public string ReleaseTitle { get; set; } = string.Empty;

    public string TrackTitle { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public double Confidence { get; set; } = 0.55;

    public string Reason { get; set; } = "Remote Bloom filter probably contains this MusicBrainz identifier; false positives are possible.";

    public bool WishlistSeeded { get; set; }
}

public sealed class LibraryBloomWishlistPromotionRequest
{
    public LibraryBloomDiffRequest DiffRequest { get; set; } = new();

    public string Filter { get; set; } = "flac";

    public int MaxResults { get; set; } = 100;
}

public sealed class LibraryBloomWishlistPromotionResult
{
    public int CreatedCount { get; set; }

    public int AlreadySeededCount { get; set; }

    public List<Guid> CreatedItemIds { get; set; } = new();
}
