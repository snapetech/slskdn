// <copyright file="DiscographyCoverageModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz;

using System;
using System.Collections.Generic;
using slskd.Integrations.MusicBrainz.API.DTO;
using slskd.Integrations.MusicBrainz.Models;

public sealed class DiscographyCoverageRequest
{
    public string ArtistId { get; set; } = string.Empty;

    public DiscographyProfile Profile { get; set; } = DiscographyProfile.CoreDiscography;

    public bool ForceRefresh { get; set; }

    public bool IncludeDiscoveryGraphPriority { get; set; } = true;
}

public sealed class DiscographyCoverageResult
{
    public string ArtistId { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public DiscographyProfile Profile { get; set; } = DiscographyProfile.CoreDiscography;

    public int TotalReleases { get; set; }

    public int CompleteReleases { get; set; }

    public int TotalTracks { get; set; }

    public int CoveredTracks { get; set; }

    public double CoverageRatio => TotalTracks == 0 ? 0 : (double)CoveredTracks / TotalTracks;

    public bool PromotionSuggested => TotalTracks > 0 && CoverageRatio >= 0.70 && CoveredTracks < TotalTracks;

    public DiscographyGraphPrioritySummary? GraphPriority { get; set; }

    public List<DiscographyCoverageRelease> Releases { get; set; } = new();
}

public sealed class DiscographyCoverageRelease
{
    public string ReleaseGroupId { get; set; } = string.Empty;

    public string ReleaseId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ReleaseDate { get; set; } = string.Empty;

    public ReleaseGroupType Type { get; set; }

    public int TotalTracks { get; set; }

    public int CoveredTracks { get; set; }

    public bool Complete => TotalTracks > 0 && CoveredTracks == TotalTracks;

    public double PriorityScore { get; set; }

    public double GraphDensityScore { get; set; }

    public double EvidenceScore { get; set; }

    public double GapScore { get; set; }

    public List<string> PriorityReasons { get; set; } = new();

    public List<DiscographyCoverageTrack> Tracks { get; set; } = new();
}

public sealed class DiscographyGraphPrioritySummary
{
    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public double NeighborhoodDensityScore { get; set; }

    public double EvidenceScore { get; set; }

    public List<string> RecommendedReleaseIds { get; set; } = new();

    public List<string> Reasons { get; set; } = new();
}

public sealed class DiscographyCoverageTrack
{
    public int Position { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string RecordingId { get; set; } = string.Empty;

    public int? DurationMs { get; set; }

    public DiscographyCoverageStatus Status { get; set; } = DiscographyCoverageStatus.Absent;

    public List<string> Evidence { get; set; } = new();

    public List<HashMatch> Matches { get; set; } = new();
}

public enum DiscographyCoverageStatus
{
    Absent,
    MeshAvailable,
    WishlistSeeded,
    Ambiguous,
}

public sealed class DiscographyWishlistPromotionRequest
{
    public string ArtistId { get; set; } = string.Empty;

    public DiscographyProfile Profile { get; set; } = DiscographyProfile.CoreDiscography;

    public string Filter { get; set; } = "flac";

    public int MaxResults { get; set; } = 100;
}

public sealed class DiscographyWishlistPromotionResult
{
    public string ArtistId { get; set; } = string.Empty;

    public int CreatedCount { get; set; }

    public int AlreadySeededCount { get; set; }

    public List<Guid> CreatedItemIds { get; set; } = new();
}
