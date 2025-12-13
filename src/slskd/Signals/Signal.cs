namespace slskd.Signals;

using System.Collections.Generic;

/// <summary>
/// Represents a control signal sent between slskdn peers over multiple channels.
/// </summary>
public sealed class Signal
{
    /// <summary>
    /// Unique identifier for this signal (ULID/UUID) used for deduplication.
    /// </summary>
    public string SignalId { get; }

    /// <summary>
    /// Peer ID of the sender (slskdn Mesh PeerId).
    /// </summary>
    public string FromPeerId { get; }

    /// <summary>
    /// Peer ID of the target recipient.
    /// </summary>
    public string ToPeerId { get; }

    /// <summary>
    /// Timestamp when the signal was created.
    /// </summary>
    public DateTimeOffset SentAt { get; }

    /// <summary>
    /// Signal type identifier (e.g., "Swarm.RequestBtFallback").
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Signal body containing type-specific data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Body { get; }

    /// <summary>
    /// Time-to-live for this signal.
    /// </summary>
    public TimeSpan Ttl { get; }

    /// <summary>
    /// Preferred channels for delivery, in order of preference.
    /// </summary>
    public IReadOnlyList<SignalChannel> PreferredChannels { get; }

    public Signal(
        string signalId,
        string fromPeerId,
        string toPeerId,
        DateTimeOffset sentAt,
        string type,
        IReadOnlyDictionary<string, object> body,
        TimeSpan ttl,
        IReadOnlyList<SignalChannel> preferredChannels)
    {
        SignalId = signalId ?? throw new ArgumentNullException(nameof(signalId));
        FromPeerId = fromPeerId ?? throw new ArgumentNullException(nameof(fromPeerId));
        ToPeerId = toPeerId ?? throw new ArgumentNullException(nameof(toPeerId));
        SentAt = sentAt;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Ttl = ttl;
        PreferredChannels = preferredChannels ?? throw new ArgumentNullException(nameof(preferredChannels));
    }

    /// <summary>
    /// Check if this signal has expired based on TTL.
    /// </summary>
    public bool IsExpired(DateTimeOffset now)
    {
        return now > SentAt + Ttl;
    }
}

/// <summary>
/// Available channels for signal delivery.
/// </summary>
public enum SignalChannel
{
    /// <summary>
    /// Mesh overlay network (primary control plane).
    /// </summary>
    Mesh,

    /// <summary>
    /// BitTorrent extension protocol (secondary, requires active BT session).
    /// </summary>
    BtExtension,

    /// <summary>
    /// Direct peer-to-peer connection (future).
    /// </summary>
    Direct
}















