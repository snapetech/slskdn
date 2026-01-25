// <copyright file="ControlEnvelope.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using MessagePack;
using slskd.Mesh.Transport;

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

    /// <summary>
    /// Unique message identifier for replay protection.
    /// </summary>
    [Key(5)] public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Initializes a new ControlEnvelope with a unique MessageId.
    /// </summary>
    public ControlEnvelope()
    {
        MessageId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Gets the data that should be signed for envelope verification (canonical format).
    /// </summary>
    public byte[] GetSignableData()
    {
        return CanonicalSerialization.SerializeEnvelopeForSigning(this);
    }

    /// <summary>
    /// Gets the legacy signable payload (Type|Timestamp|Base64(Payload)) for backwards compatibility during verify.
    /// PR-10: matches old KeyedSigner.BuildSignablePayload so envelopes signed with it still validate.
    /// </summary>
    public byte[] GetLegacySignableData()
    {
        var s = $"{Type}|{TimestampUnixMs}|{Convert.ToBase64String(Payload)}";
        return System.Text.Encoding.UTF8.GetBytes(s);
    }

    /// <summary>
    /// Validates the envelope timestamp against current time.
    /// </summary>
    /// <param name="maxSkewSeconds">Maximum allowed time skew in seconds.</param>
    /// <returns>True if timestamp is within acceptable range.</returns>
    public bool IsTimestampValid(int maxSkewSeconds = 120)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var diff = Math.Abs(now - TimestampUnixMs);
        return diff <= (maxSkewSeconds * 1000);
    }
}
