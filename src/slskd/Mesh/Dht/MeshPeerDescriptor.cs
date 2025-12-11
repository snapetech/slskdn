using System;
using System.Collections.Generic;
using MessagePack;

namespace slskd.Mesh.Dht;

/// <summary>
/// Mesh peer descriptor published to DHT.
/// </summary>
[MessagePackObject]
public class MeshPeerDescriptor
{
    [Key(0)]
    public string PeerId { get; set; } = string.Empty;

    [Key(1)]
    public List<string> Endpoints { get; set; } = new(); // e.g., udp://host:port, quic://host:port

    [Key(2)]
    public string? NatType { get; set; } // unknown|direct|symmetric|restricted

    [Key(3)]
    public bool RelayRequired { get; set; }

    [Key(4)]
    public long TimestampUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
