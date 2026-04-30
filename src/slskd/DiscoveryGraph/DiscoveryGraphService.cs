// <copyright file="DiscoveryGraphService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DiscoveryGraph;

using slskd.Integrations.MusicBrainz;
using slskd.SongID;

public interface IDiscoveryGraphService
{
    Task<DiscoveryGraphResult> BuildAsync(DiscoveryGraphRequest request, CancellationToken cancellationToken = default);
}

public sealed class DiscoveryGraphService : IDiscoveryGraphService
{
    private const double MinimumTrackIdentityForWeakRun = 0.70;
    private const double MinimumTrackIdentityForCatalogExpansion = 0.85;
    private const double MinimumSegmentConfidenceForGraph = 0.65;
    private const double MinimumSegmentCandidateIdentityForGraph = 0.63;
    private const double MinimumMixIdentityForGraph = 0.60;

    private readonly ISongIdService _songIdService;
    private readonly IArtistReleaseGraphService _releaseGraphService;

    public DiscoveryGraphService(
        ISongIdService songIdService,
        IArtistReleaseGraphService releaseGraphService)
    {
        _songIdService = songIdService;
        _releaseGraphService = releaseGraphService;
    }

    public async Task<DiscoveryGraphResult> BuildAsync(DiscoveryGraphRequest request, CancellationToken cancellationToken = default)
    {
        var run = request.SongIdRunId.HasValue ? _songIdService.Get(request.SongIdRunId.Value) : null;
        var graph = new DiscoveryGraphResult
        {
            Request = request,
        };

        switch ((request.Scope ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "artist":
                await BuildArtistGraphAsync(graph, request, run, cancellationToken).ConfigureAwait(false);
                break;
            case "album":
                BuildAlbumGraph(graph, request, run);
                break;
            case "track":
                BuildTrackGraph(graph, request, run);
                break;
            default:
                BuildRunGraph(graph, request, run);
                break;
        }

        AddComparisonOverlay(graph, request, run);

        if (graph.Nodes.Count == 0)
        {
            throw new InvalidOperationException("No Discovery Graph neighborhood could be generated for the supplied seed.");
        }

        return graph;
    }

    private async Task BuildArtistGraphAsync(DiscoveryGraphResult graph, DiscoveryGraphRequest request, SongIdRun? run, CancellationToken cancellationToken)
    {
        var canExpandCatalog = CanExpandCatalogContext(run);
        var artistId = request.ArtistId
            ?? run?.Artists.FirstOrDefault()?.ArtistId
            ?? run?.Tracks.FirstOrDefault()?.MusicBrainzArtistId;
        var artistName = request.Artist
            ?? run?.Artists.FirstOrDefault(candidate => string.Equals(candidate.ArtistId, artistId, StringComparison.OrdinalIgnoreCase))?.Name
            ?? run?.Metadata.Artist
            ?? "Unknown artist";
        var centerNodeId = $"artist:{artistId ?? NormalizeId(artistName)}";
        AddNode(graph, centerNodeId, artistName, "artist", 1, 0, "center", "Seed artist for discovery topology.");

        if (run != null)
        {
            foreach (var track in GetGraphTrackCandidates(run).Where(track => string.Equals(track.MusicBrainzArtistId, artistId, StringComparison.OrdinalIgnoreCase)).Take(4))
            {
                var nodeId = $"track:{track.RecordingId}";
                AddNode(graph, nodeId, track.Title, "track", track.ActionScore, 1, "recording", "Track candidate attached to the selected artist.");
                AddEdge(
                    graph,
                    centerNodeId,
                    nodeId,
                    "performed_by",
                    track.IdentityScore,
                    "Track candidate resolves to this artist.",
                    "songid",
                    new Dictionary<string, double>
                    {
                        ["identity"] = track.IdentityScore,
                        ["action"] = track.ActionScore,
                    },
                    $"SongID track candidate {track.CandidateId}");
            }

            foreach (var otherArtist in GetGraphArtistCandidates(run).Where(candidate => !string.Equals(candidate.ArtistId, artistId, StringComparison.OrdinalIgnoreCase)).Take(4))
            {
                var nodeId = $"artist:{otherArtist.ArtistId}";
                AddNode(graph, nodeId, otherArtist.Name, "artist", otherArtist.ActionScore, 1, "neighbor", "Nearby artist candidate surfaced in the same SongID context.");
                AddEdge(
                    graph,
                    centerNodeId,
                    nodeId,
                    "candidate_neighbor",
                    otherArtist.ByzantineScore,
                    "Artist candidate co-occurred in the same identification neighborhood.",
                    "songid",
                    new Dictionary<string, double>
                    {
                        ["identity"] = otherArtist.IdentityScore,
                        ["byzantine"] = otherArtist.ByzantineScore,
                        ["action"] = otherArtist.ActionScore,
                    },
                    $"SongID artist candidate {otherArtist.CandidateId}");
            }
        }

        if (!string.IsNullOrWhiteSpace(artistId) && canExpandCatalog)
        {
            var releaseGraph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, false, cancellationToken).ConfigureAwait(false);
            foreach (var releaseGroup in releaseGraph?.ReleaseGroups?.Take(6) ?? Enumerable.Empty<Integrations.MusicBrainz.Models.ReleaseGroup>())
            {
                var nodeId = $"release-group:{releaseGroup.ReleaseGroupId}";
                AddNode(graph, nodeId, releaseGroup.Title, "album", 0.58, 1, "release", "Release group from MusicBrainz artist graph.");
                AddEdge(
                    graph,
                    centerNodeId,
                    nodeId,
                    "release_group",
                    0.62,
                    "MusicBrainz release-graph expansion.",
                    "musicbrainz_release_graph",
                    new Dictionary<string, double>
                    {
                        ["metadata"] = 0.62,
                    },
                    "Release-group expansion from MusicBrainz");
            }
        }

        FinalizeGraph(graph, artistName, "Artist neighborhood with SongID candidates and release-graph context.");
    }

    private static void BuildAlbumGraph(DiscoveryGraphResult graph, DiscoveryGraphRequest request, SongIdRun? run)
    {
        var album = run?.Albums.FirstOrDefault(candidate =>
            string.Equals(candidate.ReleaseId, request.ReleaseId, StringComparison.OrdinalIgnoreCase)) ?? run?.Albums.FirstOrDefault();
        var title = request.Album ?? album?.Title ?? "Unknown album";
        var centerNodeId = $"album:{request.ReleaseId ?? album?.ReleaseId ?? NormalizeId(title)}";
        AddNode(graph, centerNodeId, title, "album", 1, 0, "center", "Seed release for discovery topology.");

        if (album != null)
        {
            AddArtistNeighbor(graph, centerNodeId, album.MusicBrainzArtistId, album.Artist, album.IdentityScore, "Album candidate resolves to this artist.");
        }

        if (run != null)
        {
            foreach (var track in GetGraphTrackCandidates(run).Where(track => string.Equals(track.Artist, album?.Artist ?? request.Artist, StringComparison.OrdinalIgnoreCase)).Take(5))
            {
                var nodeId = $"track:{track.RecordingId}";
                AddNode(graph, nodeId, track.Title, "track", track.ActionScore, 1, "recording", "Track candidate sits near this album in the same SongID context.");
                AddEdge(
                    graph,
                    centerNodeId,
                    nodeId,
                    "album_context",
                    track.IdentityScore,
                    "Track candidate shares artist context with the selected album.",
                    "songid",
                    new Dictionary<string, double>
                    {
                        ["identity"] = track.IdentityScore,
                        ["action"] = track.ActionScore,
                    },
                    $"Track candidate {track.CandidateId} shares artist context");
            }
        }

        FinalizeGraph(graph, title, "Album neighborhood with artist and nearby track context.");
    }

    private static void BuildTrackGraph(DiscoveryGraphResult graph, DiscoveryGraphRequest request, SongIdRun? run)
    {
        var track = run?.Tracks.FirstOrDefault(candidate =>
            string.Equals(candidate.RecordingId, request.RecordingId, StringComparison.OrdinalIgnoreCase)) ?? run?.Tracks.FirstOrDefault();
        var title = request.Title ?? track?.Title ?? "Unknown track";
        var centerNodeId = $"track:{request.RecordingId ?? track?.RecordingId ?? NormalizeId(title)}";
        AddNode(graph, centerNodeId, title, "track", 1, 0, "center", "Seed recording for discovery topology.");

        if (track != null)
        {
            AddArtistNeighbor(graph, centerNodeId, track.MusicBrainzArtistId, track.Artist, track.IdentityScore, "Track candidate resolves to this artist.");
        }

        if (run != null)
        {
            foreach (var sibling in GetGraphTrackCandidates(run).Where(candidate => !string.Equals(candidate.RecordingId, track?.RecordingId, StringComparison.OrdinalIgnoreCase)).Take(5))
            {
                var nodeId = $"track:{sibling.RecordingId}";
                AddNode(graph, nodeId, $"{sibling.Artist} - {sibling.Title}", "track", sibling.ActionScore, 1, "candidate", "Alternative or adjacent track candidate from the same SongID run.");
                AddEdge(
                    graph,
                    centerNodeId,
                    nodeId,
                    "candidate_neighbor",
                    sibling.ByzantineScore,
                    "Track candidate co-occurred in the same identification neighborhood.",
                    "songid",
                    new Dictionary<string, double>
                    {
                        ["identity"] = sibling.IdentityScore,
                        ["byzantine"] = sibling.ByzantineScore,
                        ["action"] = sibling.ActionScore,
                    },
                    $"Track candidate {sibling.CandidateId} from the same SongID run");
            }

            foreach (var segment in GetGraphSegments(run).Take(4))
            {
                var nodeId = $"segment:{segment.SegmentId}";
                AddNode(graph, nodeId, segment.Label, "segment", segment.Confidence, 1, "segment", segment.DecompositionLabel);
                AddEdge(
                    graph,
                    centerNodeId,
                    nodeId,
                    "segment_context",
                    segment.Confidence,
                    "Timestamp/chapter decomposition linked this section to the seed neighborhood.",
                    "songid_segment",
                    new Dictionary<string, double>
                    {
                        ["confidence"] = segment.Confidence,
                    },
                    segment.DecompositionLabel);
            }
        }

        FinalizeGraph(graph, title, "Track neighborhood with artist, alternatives, and segment ambiguity context.");
    }

    private static void BuildRunGraph(DiscoveryGraphResult graph, DiscoveryGraphRequest request, SongIdRun? run)
    {
        if (run == null)
        {
            var fallbackSeedLabel = request.Title ?? request.Artist ?? request.Album ?? "SongID seed";
            var fallbackSeedNodeId = $"seed:{NormalizeId(fallbackSeedLabel)}";
            AddNode(graph, fallbackSeedNodeId, fallbackSeedLabel, "seed", 1, 0, "center", "Fallback Discovery Graph seed.");
            AddFallbackContext(graph, fallbackSeedNodeId, request);
            FinalizeGraph(graph, fallbackSeedLabel, "Fallback discovery seed without a SongID run context.");
            return;
        }

        var seedLabel = run.Query is { Length: > 0 } ? run.Query : run.Metadata.Title;
        var seedNodeId = $"songid:{run.Id:D}";
        AddNode(graph, seedNodeId, seedLabel, "songid_run", 1, 0, "center", "SongID run seed. Near nodes come from identity, ambiguity, and evidence context.");

        foreach (var track in GetGraphTrackCandidates(run).Take(4))
        {
            var nodeId = $"track:{track.RecordingId}";
            AddNode(graph, nodeId, $"{track.Artist} - {track.Title}", "track", track.ActionScore, 1, "candidate", "Ranked SongID track candidate.");
            AddEdge(
                graph,
                seedNodeId,
                nodeId,
                "identity_candidate",
                track.IdentityScore,
                track.IsExact ? "Exact track identity." : "Plausible track identity.",
                "songid",
                new Dictionary<string, double>
                {
                    ["identity"] = track.IdentityScore,
                    ["byzantine"] = track.ByzantineScore,
                    ["action"] = track.ActionScore,
                },
                $"Track candidate {track.CandidateId}");
        }

        foreach (var album in GetGraphAlbumCandidates(run).Take(3))
        {
            var nodeId = $"album:{album.ReleaseId}";
            AddNode(graph, nodeId, $"{album.Artist} - {album.Title}", "album", album.ActionScore, 1, "release", "Album candidate near the current SongID result.");
            AddEdge(
                graph,
                seedNodeId,
                nodeId,
                "album_context",
                album.IdentityScore,
                "Album candidate derived from SongID identity resolution.",
                "songid",
                new Dictionary<string, double>
                {
                    ["identity"] = album.IdentityScore,
                    ["byzantine"] = album.ByzantineScore,
                    ["action"] = album.ActionScore,
                },
                $"Album candidate {album.CandidateId}");
        }

        foreach (var artist in GetGraphArtistCandidates(run).Take(3))
        {
            var nodeId = $"artist:{artist.ArtistId}";
            AddNode(graph, nodeId, artist.Name, "artist", artist.ActionScore, 1, "neighbor", "Artist candidate near the current SongID result.");
            AddEdge(
                graph,
                seedNodeId,
                nodeId,
                "artist_context",
                artist.IdentityScore,
                "Artist context derived from SongID resolution.",
                "songid",
                new Dictionary<string, double>
                {
                    ["identity"] = artist.IdentityScore,
                    ["byzantine"] = artist.ByzantineScore,
                    ["action"] = artist.ActionScore,
                },
                $"Artist candidate {artist.CandidateId}");
        }

        foreach (var segment in GetGraphSegments(run).Take(4))
        {
            var nodeId = $"segment:{segment.SegmentId}";
            AddNode(graph, nodeId, segment.Label, "segment", segment.Confidence, 1, "segment", segment.DecompositionLabel);
            AddEdge(
                graph,
                seedNodeId,
                nodeId,
                "segment_context",
                segment.Confidence,
                "Mix or timestamp decomposition branch.",
                "songid_segment",
                new Dictionary<string, double>
                {
                    ["confidence"] = segment.Confidence,
                },
                segment.DecompositionLabel);

            foreach (var candidate in GetGraphSegmentCandidates(segment).Take(2))
            {
                var candidateNodeId = $"track:{candidate.RecordingId}";
                AddNode(graph, candidateNodeId, $"{candidate.Artist} - {candidate.Title}", "track", candidate.ActionScore, 2, "candidate", "Candidate attached to a decomposed SongID segment.");
                AddEdge(
                    graph,
                    nodeId,
                    candidateNodeId,
                    "segment_candidate",
                    candidate.IdentityScore,
                    "Segment-level candidate search path.",
                    "songid_segment",
                    new Dictionary<string, double>
                    {
                        ["identity"] = candidate.IdentityScore,
                        ["byzantine"] = candidate.ByzantineScore,
                        ["action"] = candidate.ActionScore,
                    },
                    $"Segment candidate {candidate.CandidateId}");
            }
        }

        foreach (var mix in GetGraphMixGroups(run).Take(3))
        {
            var mixNodeId = $"mix:{mix.MixId}";
            AddNode(graph, mixNodeId, mix.Label, "mix", mix.ActionScore, 1, "segment", "Mix cluster aggregated from contiguous segments.");
            AddEdge(
                graph,
                seedNodeId,
                mixNodeId,
                "mix_cluster",
                mix.Confidence,
                "Detected mix cluster from segment decomposition.",
                "songid_mix",
                new Dictionary<string, double>
                {
                    ["confidence"] = mix.Confidence,
                    ["identity"] = mix.IdentityScore,
                    ["action"] = mix.ActionScore,
                },
                $"Mix cluster covering {mix.SegmentCount} segments");

            foreach (var segmentId in mix.SegmentIds)
            {
                var segmentNodeId = $"segment:{segmentId}";
                AddEdge(
                    graph,
                    mixNodeId,
                    segmentNodeId,
                    "mix_segment",
                    mix.Confidence,
                    "Segment belongs to this mix cluster.",
                    "songid_mix",
                    new Dictionary<string, double>
                    {
                        ["confidence"] = mix.Confidence,
                    },
                    $"Segment {segmentId} is part of mix cluster");
            }
        }

        FinalizeGraph(graph, seedLabel, "SongID neighborhood with candidates, ambiguity branches, and segment decomposition.");
    }

    private static void AddArtistNeighbor(DiscoveryGraphResult graph, string sourceNodeId, string? artistId, string? artistName, double weight, string reason)
    {
        var label = !string.IsNullOrWhiteSpace(artistName) ? artistName : "Unknown artist";
        var nodeId = $"artist:{artistId ?? NormalizeId(label)}";
        AddNode(graph, nodeId, label, "artist", weight, 1, "neighbor", reason);
        AddEdge(
            graph,
            sourceNodeId,
            nodeId,
            "performed_by",
            weight,
            reason,
            "songid",
            new Dictionary<string, double>
            {
                ["identity"] = weight,
            },
            reason);
    }

    private static void AddNode(DiscoveryGraphResult graph, string nodeId, string label, string nodeType, double weight, int depth, string accent, string reason)
    {
        if (graph.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        graph.Nodes.Add(new DiscoveryGraphNode
        {
            NodeId = nodeId,
            Label = label,
            Subtitle = nodeType.Replace('_', ' '),
            NodeType = nodeType,
            Accent = accent,
            Reason = reason,
            Weight = Math.Max(0.2, Math.Min(1, weight)),
            Depth = depth,
        });
    }

    private static void AddEdge(
        DiscoveryGraphResult graph,
        string sourceNodeId,
        string targetNodeId,
        string edgeType,
        double weight,
        string reason,
        string provenance = "",
        Dictionary<string, double>? scoreComponents = null,
        params string[] evidence)
    {
        if (graph.Edges.Any(edge =>
                string.Equals(edge.SourceNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(edge.TargetNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(edge.EdgeType, edgeType, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        graph.Edges.Add(new DiscoveryGraphEdge
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            EdgeType = edgeType,
            Weight = Math.Max(0.2, Math.Min(1, weight)),
            Reason = reason,
            Provenance = provenance,
            ScoreComponents = scoreComponents ?? new Dictionary<string, double>(),
            Evidence = evidence?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? new List<string>(),
        });
    }

    private static void AddComparisonOverlay(DiscoveryGraphResult graph, DiscoveryGraphRequest request, SongIdRun? run)
    {
        if (string.IsNullOrWhiteSpace(request.CompareNodeId))
        {
            return;
        }

        var compareParts = request.CompareNodeId.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (compareParts.Length != 2)
        {
            return;
        }

        var nodeType = compareParts[0];
        var rawId = compareParts[1];
        var label = !string.IsNullOrWhiteSpace(request.CompareLabel) ? request.CompareLabel : request.CompareNodeId;

        if (graph.Nodes.Any(node => string.Equals(node.NodeId, request.CompareNodeId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AddNode(graph, request.CompareNodeId, label, nodeType, 0.84, 1, "compare", "Pinned comparison node.");
        AddEdge(
            graph,
            graph.SeedNodeId,
            request.CompareNodeId,
            "comparison",
            0.72,
            "User-pinned comparison between the current seed and another graph node.",
            "ui_compare",
            BuildComparisonScores(graph, request.CompareNodeId, run),
            "Pinned from graph UI");

        if (run == null)
        {
            return;
        }

        if (string.Equals(nodeType, "artist", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var track in GetGraphTrackCandidates(run).Where(track => string.Equals(track.MusicBrainzArtistId, rawId, StringComparison.OrdinalIgnoreCase)).Take(3))
            {
                var nodeId = $"track:{track.RecordingId}";
                AddNode(graph, nodeId, $"{track.Artist} - {track.Title}", "track", track.ActionScore, 2, "candidate", "Track attached to the pinned comparison artist.");
                AddEdge(
                    graph,
                    request.CompareNodeId,
                    nodeId,
                    "comparison_context",
                    track.IdentityScore,
                    "Track branch attached to the pinned comparison artist.",
                    "songid",
                    new Dictionary<string, double>
                    {
                        ["identity"] = track.IdentityScore,
                        ["action"] = track.ActionScore,
                    },
                    $"Track candidate {track.CandidateId}");
            }
        }
    }

    private static IEnumerable<SongIdTrackCandidate> GetGraphTrackCandidates(SongIdRun run)
    {
        var canExpand = CanExpandCatalogContext(run);

        return run.Tracks
            .Where(track =>
                !string.IsNullOrWhiteSpace(track.RecordingId) &&
                !string.IsNullOrWhiteSpace(track.Title) &&
                !string.IsNullOrWhiteSpace(track.Artist))
            .Where(track => canExpand || track.IsExact || track.IdentityScore >= MinimumTrackIdentityForWeakRun)
            .OrderByDescending(track => track.ActionScore)
            .ThenByDescending(track => track.IdentityScore);
    }

    private static IEnumerable<SongIdAlbumCandidate> GetGraphAlbumCandidates(SongIdRun run)
    {
        if (!CanExpandCatalogContext(run))
        {
            return Enumerable.Empty<SongIdAlbumCandidate>();
        }

        return run.Albums
            .Where(album =>
                !string.IsNullOrWhiteSpace(album.ReleaseId) &&
                !string.IsNullOrWhiteSpace(album.Title))
            .OrderByDescending(album => album.ActionScore)
            .ThenByDescending(album => album.IdentityScore);
    }

    private static IEnumerable<SongIdArtistCandidate> GetGraphArtistCandidates(SongIdRun run)
    {
        if (!CanExpandCatalogContext(run))
        {
            return Enumerable.Empty<SongIdArtistCandidate>();
        }

        return run.Artists
            .Where(artist =>
                !string.IsNullOrWhiteSpace(artist.ArtistId) &&
                !string.IsNullOrWhiteSpace(artist.Name))
            .OrderByDescending(artist => artist.ActionScore)
            .ThenByDescending(artist => artist.IdentityScore);
    }

    private static IEnumerable<SongIdSegmentResult> GetGraphSegments(SongIdRun run)
    {
        if (!CanExpandCatalogContext(run))
        {
            return Enumerable.Empty<SongIdSegmentResult>();
        }

        return run.Segments
            .Where(segment =>
                segment.Confidence >= MinimumSegmentConfidenceForGraph &&
                GetGraphSegmentCandidates(segment).Any())
            .OrderByDescending(segment => segment.Confidence);
    }

    private static IEnumerable<SongIdTrackCandidate> GetGraphSegmentCandidates(SongIdSegmentResult segment)
        => segment.Candidates
            .Where(candidate =>
                !string.IsNullOrWhiteSpace(candidate.RecordingId) &&
                !string.IsNullOrWhiteSpace(candidate.Title) &&
                !string.IsNullOrWhiteSpace(candidate.Artist) &&
                candidate.IdentityScore >= MinimumSegmentCandidateIdentityForGraph)
            .OrderByDescending(candidate => candidate.ActionScore)
            .ThenByDescending(candidate => candidate.IdentityScore);

    private static IEnumerable<SongIdMixGroup> GetGraphMixGroups(SongIdRun run)
    {
        if (!CanExpandCatalogContext(run))
        {
            return Enumerable.Empty<SongIdMixGroup>();
        }

        var graphSegmentIds = GetGraphSegments(run)
            .Select(segment => segment.SegmentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return run.MixGroups
            .Where(mix =>
                mix.IdentityScore >= MinimumMixIdentityForGraph &&
                mix.SegmentIds.Any(graphSegmentIds.Contains))
            .OrderByDescending(mix => mix.ActionScore)
            .ThenByDescending(mix => mix.IdentityScore);
    }

    private static bool CanExpandCatalogContext(SongIdRun? run)
    {
        if (run == null)
        {
            return true;
        }

        if (run.Tracks.Any(track => track.IsExact || track.IdentityScore >= MinimumTrackIdentityForCatalogExpansion))
        {
            return true;
        }

        var verdict = run.IdentityAssessment?.Verdict ?? run.Assessment?.Verdict ?? string.Empty;
        var confidence = run.IdentityAssessment?.Confidence ?? run.Assessment?.Confidence ?? 0;

        return (string.Equals(verdict, "recognized_cataloged_track", StringComparison.OrdinalIgnoreCase) && confidence >= 0.65)
            || (string.Equals(verdict, "candidate_match_found", StringComparison.OrdinalIgnoreCase) && confidence >= 0.75);
    }

    private static Dictionary<string, double> BuildComparisonScores(DiscoveryGraphResult graph, string compareNodeId, SongIdRun? run)
    {
        var scores = new Dictionary<string, double>();
        var seed = graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, graph.SeedNodeId, StringComparison.OrdinalIgnoreCase));
        var compare = graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, compareNodeId, StringComparison.OrdinalIgnoreCase));
        if (seed != null && compare != null)
        {
            scores["weight_delta"] = Math.Abs(seed.Weight - compare.Weight);
        }

        if (run != null)
        {
            scores["shared_songid_context"] = 1;
            scores["segment_count"] = run.Segments.Count;
        }

        return scores;
    }

    private static void AddFallbackContext(DiscoveryGraphResult graph, string seedNodeId, DiscoveryGraphRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Artist))
        {
            var artistNodeId = $"artist:{request.ArtistId ?? NormalizeId(request.Artist)}";
            AddNode(graph, artistNodeId, request.Artist, "artist", 0.74, 1, "neighbor", "Artist supplied directly as fallback graph context.");
            AddEdge(
                graph,
                seedNodeId,
                artistNodeId,
                "metadata_context",
                0.68,
                "Artist text supplied with the graph seed.",
                "fallback_request",
                new Dictionary<string, double>
                {
                    ["metadata"] = 0.68,
                },
                request.Artist);
        }

        if (!string.IsNullOrWhiteSpace(request.Album))
        {
            var albumNodeId = $"album:{request.ReleaseId ?? NormalizeId(request.Album)}";
            AddNode(graph, albumNodeId, request.Album, "album", 0.7, 1, "release", "Album supplied directly as fallback graph context.");
            AddEdge(
                graph,
                seedNodeId,
                albumNodeId,
                "metadata_context",
                0.64,
                "Album text supplied with the graph seed.",
                "fallback_request",
                new Dictionary<string, double>
                {
                    ["metadata"] = 0.64,
                },
                request.Album);
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var trackNodeId = $"track:{request.RecordingId ?? NormalizeId(request.Title)}";
            AddNode(graph, trackNodeId, request.Title, "track", 0.76, 1, "candidate", "Track title supplied directly as fallback graph context.");
            AddEdge(
                graph,
                seedNodeId,
                trackNodeId,
                "metadata_context",
                0.72,
                "Track title text supplied with the graph seed.",
                "fallback_request",
                new Dictionary<string, double>
                {
                    ["metadata"] = 0.72,
                },
                request.Title);
        }
    }

    private static void FinalizeGraph(DiscoveryGraphResult graph, string title, string summary)
    {
        graph.Title = title;
        graph.Summary = summary;
        graph.SeedNodeId = graph.Nodes.First(node => node.Depth == 0).NodeId;
    }

    private static string NormalizeId(string value)
    {
        return string.Join("-", (value ?? string.Empty)
            .ToLowerInvariant()
            .Split(new[] { ' ', '/', '\\', ':', '.', ',', '|', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
