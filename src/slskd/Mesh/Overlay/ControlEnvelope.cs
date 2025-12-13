using System;
using MessagePack;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Overlay control-plane envelope (signed).
/// </summary>
[MessagePackObject]
public class ControlEnvelope
{
    [Key(0)] public string Type { get; set; } = string.Empty;
    [Key(1)] public byte[] Payload { get; set; } = Array.Empty<byte>();
    [Key(2)] public string PublicKey { get; set; } = string.Empty;
    [Key(3)] public string Signature { get; set; } = string.Empty;
    [Key(4)] public long TimestampUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    [Key(5)] public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    [Key(6)] public string? SignerKeyId { get; set; }
}
