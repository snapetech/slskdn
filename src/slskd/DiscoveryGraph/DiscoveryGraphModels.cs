// <copyright file="DiscoveryGraphModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DiscoveryGraph;

public sealed class DiscoveryGraphRequest
{
    public string Scope { get; set; } = "songid_run";

    public Guid? SongIdRunId { get; set; }

    public string? RecordingId { get; set; }

    public string? ReleaseId { get; set; }

    public string? ArtistId { get; set; }

    public string? Title { get; set; }

    public string? Artist { get; set; }

    public string? Album { get; set; }

    public string? CompareNodeId { get; set; }

    public string? CompareLabel { get; set; }
}

public sealed class DiscoveryGraphResult
{
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string SeedNodeId { get; set; } = string.Empty;

    public DiscoveryGraphRequest? Request { get; set; }

    public List<DiscoveryGraphNode> Nodes { get; set; } = new();

    public List<DiscoveryGraphEdge> Edges { get; set; } = new();
}

public sealed class DiscoveryGraphNode
{
    public string NodeId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string Accent { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public double Weight { get; set; }

    public int Depth { get; set; }
}

public sealed class DiscoveryGraphEdge
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public string EdgeType { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public double Weight { get; set; }

    public string Provenance { get; set; } = string.Empty;

    public Dictionary<string, double> ScoreComponents { get; set; } = new();

    public List<string> Evidence { get; set; } = new();
}
