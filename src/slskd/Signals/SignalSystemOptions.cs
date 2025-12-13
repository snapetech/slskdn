namespace slskd.Signals;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

/// <summary>
/// Configuration options for the signal system.
/// </summary>
public class SignalSystemOptions
{
    /// <summary>
    /// Enable the signal system.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum size of the deduplication cache (LRU).
    /// </summary>
    public int DeduplicationCacheSize { get; set; } = 10000;

    /// <summary>
    /// Default TTL for signals.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Mesh channel configuration.
    /// </summary>
    public SignalChannelOptions MeshChannel { get; set; } = new()
    {
        Enabled = true,
        Priority = 1
    };

    /// <summary>
    /// BT extension channel configuration.
    /// </summary>
    public SignalChannelOptions BtExtensionChannel { get; set; } = new()
    {
        Enabled = true,
        Priority = 2,
        RequireActiveSession = true
    };
}

/// <summary>
/// Configuration for a specific signal channel.
/// </summary>
public class SignalChannelOptions
{
    /// <summary>
    /// Enable this channel.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Priority/order for this channel (lower = higher priority).
    /// </summary>
    [Range(1, 10)]
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Require an active session before using this channel (for BT extension).
    /// </summary>
    public bool RequireActiveSession { get; set; } = false;
}
















