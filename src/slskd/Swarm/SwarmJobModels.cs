namespace slskd.Swarm;

/// <summary>
/// Swarm job model for orchestrated transfers.
/// </summary>
public record SwarmJob(string JobId, SwarmFile File, IReadOnlyList<SwarmSource> Sources);

public record SwarmFile(string ContentId, string Hash, long SizeBytes, string? Codec = null);

/// <summary>
/// Represents a potential source for a swarm transfer.
/// Uses mesh peer ID as the primary identifier, with Soulseek username as an optional alias.
/// </summary>
public record SwarmSource(
    string MeshPeerId,          // Primary key (mesh identity)
    string Transport,            // "overlay", "soulseek", "bittorrent"
    string? SoulseekUsername = null,  // Optional Soulseek alias (for Soulseek transport)
    string? Address = null,      // IP address (for direct connections)
    int? Port = null)            // Port (for direct connections)
{
    /// <summary>
    /// Gets the display name for this source (username if available, otherwise mesh ID).
    /// </summary>
    public string DisplayName => SoulseekUsername ?? MeshPeerId;
    
    /// <summary>
    /// Compatibility property: for legacy code that expects "PeerId" as username.
    /// </summary>
    [Obsolete("Use MeshPeerId for mesh-first logic or SoulseekUsername for Soulseek-specific operations")]
    public string PeerId => SoulseekUsername ?? MeshPeerId;
};

