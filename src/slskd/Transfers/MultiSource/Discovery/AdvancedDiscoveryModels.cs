// <copyright file="AdvancedDiscoveryModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Discovery;

using System;

/// <summary>
///     Content discovery request.
/// </summary>
public class ContentDiscoveryRequest
{
    /// <summary>
    ///     Filename to search for.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    ///     File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     MusicBrainz recording ID (optional).
    /// </summary>
    public string? RecordingId { get; set; }

    /// <summary>
    ///     Audio fingerprint (optional).
    /// </summary>
    public string? Fingerprint { get; set; }

    /// <summary>
    ///     Content domain (music, movies, tv, books).
    /// </summary>
    public string Domain { get; set; } = "music";

    /// <summary>
    ///     Maximum number of peers to discover.
    /// </summary>
    public int MaxPeers { get; set; } = 20;

    /// <summary>
    ///     Minimum similarity score (0.0 to 1.0).
    /// </summary>
    public double MinSimilarity { get; set; } = 0.7;
}

/// <summary>
///     Discovered peer information.
/// </summary>
public class DiscoveredPeer
{
    /// <summary>
    ///     Peer identifier (username or peer ID).
    /// </summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>
    ///     Peer source (soulseek, overlay, mesh).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     Filename as reported by peer.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    ///     File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Similarity score (0.0 to 1.0).
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    ///     Match type (exact, variant, fuzzy).
    /// </summary>
    public MatchType MatchType { get; set; }

    /// <summary>
    ///     Recording ID if matched (optional).
    /// </summary>
    public string? RecordingId { get; set; }

    /// <summary>
    ///     Metadata confidence (0.0 to 1.0).
    /// </summary>
    public double MetadataConfidence { get; set; }
}

/// <summary>
///     Ranked peer with overall score.
/// </summary>
public class RankedPeer : DiscoveredPeer
{
    /// <summary>
    ///     Overall ranking score (0.0 to 1.0).
    /// </summary>
    public double RankingScore { get; set; }

    /// <summary>
    ///     Ranking position (1-based).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    ///     Performance score (based on historical metrics).
    /// </summary>
    public double PerformanceScore { get; set; }

    /// <summary>
    ///     Availability score (based on current status).
    /// </summary>
    public double AvailabilityScore { get; set; }
}

/// <summary>
///     Content variant with similarity information.
/// </summary>
public class ContentVariant
{
    /// <summary>
    ///     Variant filename.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    ///     File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Similarity score (0.0 to 1.0).
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    ///     Recording ID if available.
    /// </summary>
    public string? RecordingId { get; set; }

    /// <summary>
    ///     Number of peers with this variant.
    /// </summary>
    public int PeerCount { get; set; }
}

/// <summary>
///     Match type for content discovery.
/// </summary>
public enum MatchType
{
    /// <summary>
    ///     Exact match (filename and size).
    /// </summary>
    Exact,

    /// <summary>
    ///     Variant match (same content, different encoding/format).
    /// </summary>
    Variant,

    /// <summary>
    ///     Fuzzy match (similar filename, size within tolerance).
    /// </summary>
    Fuzzy,

    /// <summary>
    ///     Metadata match (same recording ID or fingerprint).
    /// </summary>
    Metadata
}
