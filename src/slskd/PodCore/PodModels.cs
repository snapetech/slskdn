// <copyright file="PodModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

/// <summary>
/// Pod capabilities (features that can be enabled).
/// </summary>
public enum PodCapability
{
    /// <summary>
    /// Private service gateway - VPN-like tunneling to private services.
    /// </summary>
    PrivateServiceGateway
}

/// <summary>
/// Pod visibility.
/// </summary>
public enum PodVisibility
{
    Listed,
    Unlisted,
    Private
}

/// <summary>
/// Pod channel kind.
/// </summary>
public enum PodChannelKind
{
    General,
    Custom,
    Bound // e.g., bound to Soulseek room
}

/// <summary>
/// Pod metadata.
/// </summary>
public class Pod
{
    public string PodId { get; set; } = string.Empty; // pod:<hash>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PodVisibility Visibility { get; set; } = PodVisibility.Unlisted;
    public bool IsPublic { get; set; } = false;
    public int MaxMembers { get; set; } = 50;
    public bool AllowGuests { get; set; } = false;
    public bool RequireApproval { get; set; } = false;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? FocusContentId { get; set; } // e.g., content:mb:recording:<mbid>
    public List<string> Tags { get; set; } = new();
    public List<PodChannel> Channels { get; set; } = new();
    public List<ExternalBinding> ExternalBindings { get; set; } = new();

    /// <summary>
    /// Enabled capabilities for this pod.
    /// </summary>
    public List<PodCapability> Capabilities { get; set; } = new();

    /// <summary>
    /// Private service gateway policy (if capability enabled).
    /// </summary>
    public PodPrivateServicePolicy? PrivateServicePolicy { get; set; }
}

public class PodChannel
{
    public string ChannelId { get; set; } = string.Empty;
    public PodChannelKind Kind { get; set; } = PodChannelKind.General;
    public string Name { get; set; } = string.Empty;
    public string? BindingInfo { get; set; } // e.g., soulseek-room:techno
}

public class ExternalBinding
{
    public string Kind { get; set; } = string.Empty; // soulseek-room
    public string Mode { get; set; } = "readonly"; // readonly | mirror
    public string Identifier { get; set; } = string.Empty; // room name/id
}

public class PodMember
{
    public string PeerId { get; set; } = string.Empty;
    public string Role { get; set; } = "member"; // owner|mod|member
    public bool IsBanned { get; set; }
    public string? PublicKey { get; set; } // Ed25519 public key (base64)
}

public class PodMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string PodId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string SenderPeerId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public long TimestampUnixMs { get; set; }
    public string Signature { get; set; } = string.Empty;
    public int SigVersion { get; set; } = 1; // Version 1: includes PodId in signature payload
}

public class PodVariantOpinion
{
    public string ContentId { get; set; } = string.Empty;
    public string VariantHash { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Note { get; set; } = string.Empty;
    public string SenderPeerId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

/// <summary>
/// Policy configuration for private service gateway capability.
/// </summary>
public class PodPrivateServicePolicy
{
    /// <summary>
    /// Whether private service gateway is enabled for this pod.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum number of members allowed when this capability is enabled.
    /// </summary>
    public int MaxMembers { get; set; } = 3;

    /// <summary>
    /// Peer ID of the gateway operator (must be a pod member).
    /// </summary>
    public string GatewayPeerId { get; set; } = string.Empty;

    /// <summary>
    /// Registry of allowed services (preferred approach - no free-form host:port).
    /// </summary>
    public List<RegisteredService> RegisteredServices { get; set; } = new();

    /// <summary>
    /// List of allowed destinations for tunnel connections (legacy - prefer RegisteredServices).
    /// </summary>
    public List<AllowedDestination> AllowedDestinations { get; set; } = new();

    /// <summary>
    /// Whether private IP ranges (RFC1918) are allowed.
    /// </summary>
    public bool AllowPrivateRanges { get; set; } = false;

    /// <summary>
    /// Whether public internet destinations are allowed (MVP: false).
    /// </summary>
    public bool AllowPublicDestinations { get; set; } = false;

    /// <summary>
    /// Maximum concurrent tunnels per peer.
    /// </summary>
    public int MaxConcurrentTunnelsPerPeer { get; set; } = 2;

    /// <summary>
    /// Maximum concurrent tunnels across the entire pod.
    /// </summary>
    public int MaxConcurrentTunnelsPod { get; set; } = 5;

    /// <summary>
    /// Maximum new tunnels per peer per minute.
    /// </summary>
    public int MaxNewTunnelsPerMinutePerPeer { get; set; } = 5;

    /// <summary>
    /// Maximum bytes per day per peer (0 = unlimited).
    /// </summary>
    public long MaxBytesPerDayPerPeer { get; set; } = 0;

    /// <summary>
    /// Idle timeout for tunnels.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Maximum lifetime for tunnels.
    /// </summary>
    public TimeSpan MaxLifetime { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Timeout for outbound connection attempts.
    /// </summary>
    public TimeSpan DialTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum buffered bytes per tunnel direction (DoS protection).
    /// </summary>
    public int MaxBufferedBytesPerTunnel { get; set; } = 65536; // 64KB

    /// <summary>
    /// Maximum frame/chunk size for data transfer.
    /// </summary>
    public int MaxFrameSize { get; set; } = 8192; // 8KB
}

/// <summary>
/// Registered service for private service gateway.
/// </summary>
public class RegisteredService
{
    /// <summary>
    /// Service name (user-friendly identifier).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Service description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Destination host (exact hostname or literal IP only - no wildcards in MVP).
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Destination port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol (currently only "tcp" supported).
    /// </summary>
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Service kind for additional validation.
    /// </summary>
    public ServiceKind Kind { get; set; } = ServiceKind.Generic;
}

/// <summary>
/// Service kinds for validation and UI.
/// </summary>
public enum ServiceKind
{
    /// <summary>
    /// Generic TCP service.
    /// </summary>
    Generic,

    /// <summary>
    /// Web interface (HTTP/HTTPS).
    /// </summary>
    WebInterface,

    /// <summary>
    /// SSH service.
    /// </summary>
    SSH,

    /// <summary>
    /// Database service.
    /// </summary>
    Database,

    /// <summary>
    /// Home automation service.
    /// </summary>
    HomeAutomation,

    /// <summary>
    /// Network storage service.
    /// </summary>
    NetworkStorage,

    /// <summary>
    /// Media server.
    /// </summary>
    MediaServer,

    /// <summary>
    /// Proxy service (requires explicit approval).
    /// </summary>
    Proxy
}

/// <summary>
/// Allowed destination for private service tunnels.
/// </summary>
public class AllowedDestination
{
    /// <summary>
    /// Host pattern (exact hostname or literal IP only - no wildcards in MVP).
    /// </summary>
    public string HostPattern { get; set; } = string.Empty;

    /// <summary>
    /// Destination port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol (currently only "tcp" supported).
    /// </summary>
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Whether public internet destinations are allowed (MVP: false).
    /// </summary>
    public bool AllowPublic { get; set; } = false;

    /// <summary>
    /// Service kind for additional validation.
    /// </summary>
    public ServiceKind Kind { get; set; } = ServiceKind.Generic;
}

/// <summary>
/// Runtime state for an active tunnel.
/// </summary>
public class TunnelSession
{
    /// <summary>
    /// Unique tunnel identifier (client-generated GUID).
    /// </summary>
    public string TunnelId { get; set; } = string.Empty;

    /// <summary>
    /// Pod ID for this tunnel.
    /// </summary>
    public string PodId { get; set; } = string.Empty;

    /// <summary>
    /// Client peer ID.
    /// </summary>
    public string ClientPeerId { get; set; } = string.Empty;

    /// <summary>
    /// Target host.
    /// </summary>
    public string DestinationHost { get; set; } = string.Empty;

    /// <summary>
    /// Target port.
    /// </summary>
    public int DestinationPort { get; set; }

    /// <summary>
    /// When the tunnel was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the tunnel was last active.
    /// </summary>
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Total bytes sent from client to destination.
    /// </summary>
    public long BytesIn { get; set; }

    /// <summary>
    /// Total bytes sent from destination to client.
    /// </summary>
    public long BytesOut { get; set; }

    /// <summary>
    /// Whether the tunnel is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
