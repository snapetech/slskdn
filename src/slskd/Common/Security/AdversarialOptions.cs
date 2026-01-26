// <copyright file="AdversarialOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

// Type aliases for backward compatibility  
using HttpTunnelOptions = slskd.Common.Security.HttpTunnelTransportOptions;
using MeekOptions = slskd.Common.Security.MeekTransportOptions;
using Obfs4Options = slskd.Common.Security.Obfs4TransportOptions;

/// <summary>
/// Configuration options for adversarial resilience and privacy hardening features.
/// ALL FEATURES DISABLED BY DEFAULT - only enable if you understand the security implications.
/// Can be bound to appsettings.json "Security:Adversarial" section.
/// </summary>
public sealed class AdversarialOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string Section = "Security:Adversarial";

    /// <summary>
    /// Gets or sets whether adversarial features are enabled.
    /// WARNING: These features are designed for users in adversarial environments.
    /// Only enable if you are in a repressive regime or face active surveillance.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the adversarial profile preset.
    /// </summary>
    public AdversarialProfile Profile { get; set; } = AdversarialProfile.Disabled;

    /// <summary>
    /// Gets or sets privacy layer options (traffic analysis protection).
    /// </summary>
    public PrivacyLayerOptions Privacy { get; set; } = new();

    /// <summary>
    /// Gets or sets anonymity layer options (IP protection).
    /// </summary>
    public AnonymityLayerOptions Anonymity { get; set; } = new();

    /// <summary>
    /// Gets or sets obfuscated transport options (anti-DPI).
    /// </summary>
    public ObfuscatedTransportOptions Transport { get; set; } = new();

    /// <summary>
    /// Gets or sets onion routing options (mesh-level anonymity).
    /// </summary>
    public OnionRoutingOptions OnionRouting { get; set; } = new();

    /// <summary>
    /// Gets or sets censorship resistance options.
    /// </summary>
    public CensorshipResistanceOptions CensorshipResistance { get; set; } = new();

    /// <summary>
    /// Gets or sets plausible deniability options.
    /// </summary>
    public PlausibleDeniabilityOptions PlausibleDeniability { get; set; } = new();

    /// <summary>
    /// Gets the anonymity layer options (convenience property for accessing Anonymity).
    /// </summary>
    public AnonymityLayerOptions AnonymityLayer => Anonymity;

    /// <summary>
    /// Gets the obfuscated transport options (convenience property for accessing Transport).
    /// </summary>
    public ObfuscatedTransportOptions ObfuscatedTransports => Transport;

    /// <summary>
    /// Gets or sets the mesh transport mode.
    /// </summary>
    public slskd.Mesh.MeshTransportOptions MeshTransportOptions { get; set; } = new();
}

/// <summary>
/// Adversarial profile presets.
/// </summary>
public enum AdversarialProfile
{
    /// <summary>
    /// All adversarial features disabled (default).
    /// </summary>
    Disabled,

    /// <summary>
    /// Standard protection - privacy layer enabled.
    /// </summary>
    Standard,

    /// <summary>
    /// Enhanced protection - privacy + anonymity layers.
    /// </summary>
    Enhanced,

    /// <summary>
    /// Maximum protection - all layers enabled.
    /// </summary>
    Maximum,

    /// <summary>
    /// Custom configuration - use individual settings.
    /// </summary>
    Custom,
}

/// <summary>
/// Privacy layer options (traffic analysis protection).
/// </summary>
public sealed class PrivacyLayerOptions
{
    /// <summary>
    /// Gets or sets whether privacy layer is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets message padding options.
    /// </summary>
    public MessagePaddingOptions Padding { get; set; } = new();

    /// <summary>
    /// Gets or sets timing obfuscation options.
    /// </summary>
    public TimingObfuscationOptions Timing { get; set; } = new();

    /// <summary>
    /// Gets or sets message batching options.
    /// </summary>
    public MessageBatchingOptions Batching { get; set; } = new();

    /// <summary>
    /// Gets or sets cover traffic options.
    /// </summary>
    public CoverTrafficOptions CoverTraffic { get; set; } = new();
}

/// <summary>
/// Message padding options.
/// </summary>
public sealed class MessagePaddingOptions
{
    /// <summary>
    /// Gets or sets whether message padding is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets bucket sizes in bytes for padding.
    /// Messages are padded to the next bucket size boundary.
    /// </summary>
    public List<int> BucketSizes { get; set; } = new() { 512, 1024, 2048, 4096, 8192, 16384 };

    /// <summary>
    /// Gets or sets whether to use random fill bytes (recommended) or zeros.
    /// </summary>
    public bool UseRandomFill { get; set; } = true;

    /// <summary>
    /// Gets or sets maximum padding overhead percentage (0-100).
    /// Prevents excessive padding for very small messages.
    /// </summary>
    [Range(0, 100)]
    public int MaxOverheadPercent { get; set; } = 50;

    /// <summary>
    /// Maximum unpadded payload size in bytes (Unpad rejects larger originalLength).
    /// 0 = use default (1MB). PR-11 DoS limit.
    /// </summary>
    public int MaxUnpaddedBytes { get; set; } = 0;

    /// <summary>
    /// Maximum padded message size in bytes (Unpad rejects larger packets).
    /// 0 = use default (2MB). PR-11 DoS limit.
    /// </summary>
    public int MaxPaddedBytes { get; set; } = 0;
}

/// <summary>
/// Timing obfuscation options.
/// </summary>
public sealed class TimingObfuscationOptions
{
    /// <summary>
    /// Gets or sets whether timing obfuscation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets random jitter range in milliseconds (0-500).
    /// Each message is delayed by random(0, JitterMs).
    /// </summary>
    [Range(0, 500)]
    public int JitterMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to add jitter to all outbound messages.
    /// </summary>
    public bool JitterAllMessages { get; set; } = true;
}

/// <summary>
/// Message batching options.
/// </summary>
public sealed class MessageBatchingOptions
{
    /// <summary>
    /// Gets or sets whether message batching is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets batch window in milliseconds.
    /// Messages are held for this duration before sending.
    /// </summary>
    [Range(100, 5000)]
    public int BatchWindowMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets maximum batch size.
    /// Send immediately if batch reaches this size.
    /// </summary>
    [Range(1, 100)]
    public int MaxBatchSize { get; set; } = 10;
}

/// <summary>
/// Cover traffic options.
/// </summary>
public sealed class CoverTrafficOptions
{
    /// <summary>
    /// Gets or sets whether cover traffic is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets cover traffic interval in seconds.
    /// Send dummy messages when idle for this duration.
    /// </summary>
    [Range(10, 3600)]
    public int IntervalSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Gets or sets whether to send cover traffic only when no real traffic exists.
    /// </summary>
    public bool OnlyWhenIdle { get; set; } = true;
}

/// <summary>
/// Anonymity layer options (IP protection).
/// </summary>
public sealed class AnonymityLayerOptions
{
    /// <summary>
    /// Gets or sets whether anonymity layer is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the anonymity mode.
    /// </summary>
    public AnonymityMode Mode { get; set; } = AnonymityMode.Direct;

    /// <summary>
    /// Gets or sets Tor SOCKS proxy options.
    /// </summary>
    public TorOptions Tor { get; set; } = new();

    /// <summary>
    /// Gets or sets I2P options.
    /// </summary>
    public I2POptions I2P { get; set; } = new();

    /// <summary>
    /// Gets or sets relay-only options.
    /// </summary>
    public RelayOnlyOptions RelayOnly { get; set; } = new();
}

/// <summary>
/// Anonymity mode enumeration.
/// </summary>
public enum AnonymityMode
{
    /// <summary>
    /// Direct connections (no anonymity).
    /// </summary>
    Direct,

    /// <summary>
    /// Route through Tor.
    /// </summary>
    Tor,

    /// <summary>
    /// Route through I2P.
    /// </summary>
    I2P,

    /// <summary>
    /// Only use relay nodes (never direct).
    /// </summary>
    RelayOnly,
}

/// <summary>
/// Tor SOCKS proxy options.
/// </summary>
public sealed class TorOptions
{
    /// <summary>
    /// Gets or sets Tor SOCKS proxy address.
    /// </summary>
    public string SocksAddress { get; set; } = "127.0.0.1:9050";

    /// <summary>
    /// Gets or sets whether to isolate streams per peer.
    /// Uses different Tor circuits for each peer to prevent correlation.
    /// </summary>
    public bool IsolateStreams { get; set; } = true;

    /// <summary>
    /// Gets or sets Tor control port for circuit management (optional).
    /// </summary>
    public string? ControlPort { get; set; }

    /// <summary>
    /// Gets or sets whether to verify Tor connectivity on startup.
    /// </summary>
    public bool VerifyConnectivity { get; set; } = true;
}

/// <summary>
/// I2P options.
/// </summary>
public sealed class I2POptions
{
    /// <summary>
    /// Gets or sets I2P SAM bridge address.
    /// </summary>
    public string SamAddress { get; set; } = "127.0.0.1:7656";

    /// <summary>
    /// Gets or sets whether to verify I2P connectivity on startup.
    /// </summary>
    public bool VerifyConnectivity { get; set; } = true;
}

/// <summary>
/// Relay-only options.
/// </summary>
public sealed class RelayOnlyOptions
{
    /// <summary>
    /// Gets or sets trusted relay node peer IDs.
    /// Only these peers can be used as relays.
    /// </summary>
    public List<string> TrustedRelayPeers { get; set; } = new();

    /// <summary>
    /// Gets or sets data-overlay endpoints for relay peers ("host:port" for each relay's QUIC data overlay).
    /// When non-empty, used instead of resolving TrustedRelayPeers. Required for RelayOnly until peer-id resolution is integrated.
    /// </summary>
    public List<string> RelayPeerDataEndpoints { get; set; } = new();

    /// <summary>
    /// Gets or sets maximum relay chain length.
    /// </summary>
    [Range(1, 5)]
    public int MaxChainLength { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to require encrypted relay connections.
    /// </summary>
    public bool RequireEncryption { get; set; } = true;
}

/// <summary>
/// Obfuscated transport options (anti-DPI).
/// </summary>
public sealed class ObfuscatedTransportOptions
{
    /// <summary>
    /// Gets or sets whether obfuscated transports are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the obfuscated transport mode.
    /// </summary>
    public ObfuscatedTransportMode Mode { get; set; } = ObfuscatedTransportMode.Direct;

    /// <summary>
    /// Gets or sets the primary transport to use.
    /// </summary>
    public ObfuscatedTransport PrimaryTransport { get; set; } = ObfuscatedTransport.Direct;

    /// <summary>
    /// Gets or sets fallback transports in priority order.
    /// </summary>
    public List<ObfuscatedTransport> FallbackTransports { get; set; } = new();

    /// <summary>
    /// Gets or sets WebSocket tunnel options.
    /// </summary>
    public WebSocketTransportOptions WebSocket { get; set; } = new();

    /// <summary>
    /// Gets or sets HTTP tunnel options.
    /// </summary>
    public HttpTunnelTransportOptions HttpTunnel { get; set; } = new();

    /// <summary>
    /// Gets or sets Obfs4 options.
    /// </summary>
    public Obfs4TransportOptions Obfs4 { get; set; } = new();

    /// <summary>
    /// Gets or sets Meek options.
    /// </summary>
    public MeekTransportOptions Meek { get; set; } = new();
}

/// <summary>
/// Obfuscated transport types.
/// </summary>
public enum ObfuscatedTransport
{
    /// <summary>
    /// Direct connection (no obfuscation).
    /// </summary>
    Direct,

    /// <summary>
    /// WebSocket tunnel.
    /// </summary>
    WebSocket,

    /// <summary>
    /// HTTP tunnel.
    /// </summary>
    HttpTunnel,

    /// <summary>
    /// Obfs4 obfuscation.
    /// </summary>
    Obfs4,

    /// <summary>
    /// Meek domain fronting.
    /// </summary>
    Meek,
}

/// <summary>
/// WebSocket transport options.
/// </summary>
public sealed class WebSocketTransportOptions
{
    /// <summary>
    /// Gets or sets whether WebSocket transport is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets WebSocket server URL.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to use WSS (secure WebSocket).
    /// </summary>
    public bool UseWss { get; set; } = true;

    /// <summary>
    /// Gets or sets WebSocket sub-protocol.
    /// </summary>
    public string? SubProtocol { get; set; }

    /// <summary>
    /// Gets or sets custom headers to send with WebSocket handshake.
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of pooled connections.
    /// </summary>
    public int MaxPooledConnections { get; set; } = 10;

    /// <summary>
    /// Gets or sets custom headers to send with WebSocket handshake (legacy property name).
    /// </summary>
    public Dictionary<string, string> Headers
    {
        get => CustomHeaders ?? new();
        set => CustomHeaders = value;
    }
}

/// <summary>
/// HTTP tunnel transport options.
/// </summary>
public sealed class HttpTunnelTransportOptions
{
    /// <summary>
    /// Gets or sets whether HTTP tunnel transport is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets HTTP tunnel server URL.
    /// </summary>
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets HTTP proxy URL.
    /// </summary>
    public string ProxyUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to use HTTPS.
    /// </summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets HTTP method to use (POST/GET/PUT).
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    /// Gets or sets custom HTTP headers.
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Gets or sets the User-Agent header.
    /// </summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Obfs4 transport options.
/// </summary>
public sealed class Obfs4TransportOptions
{
    /// <summary>
    /// Gets or sets whether Obfs4 transport is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets Obfs4 bridge lines.
    /// Each line should be in Tor bridge format.
    /// </summary>
    public List<string> BridgeLines { get; set; } = new();

    /// <summary>
    /// Gets or sets path to obfs4proxy binary.
    /// </summary>
    public string Obfs4ProxyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to verify bridge connectivity.
    /// </summary>
    public bool VerifyBridges { get; set; } = true;
}

/// <summary>
/// Meek transport options.
/// </summary>
public sealed class MeekTransportOptions
{
    /// <summary>
    /// Gets or sets whether Meek transport is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets Meek bridge URL.
    /// </summary>
    public string BridgeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets front domain for domain fronting.
    /// </summary>
    public string FrontDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to verify front domain.
    /// </summary>
    public bool VerifyFrontDomain { get; set; } = true;

    /// <summary>
    /// Gets or sets custom HTTP headers.
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Gets or sets the User-Agent header for Meek requests.
    /// </summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Onion routing options (mesh-level anonymity).
/// </summary>
public sealed class OnionRoutingOptions
{
    /// <summary>
    /// Gets or sets whether onion routing is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets circuit rotation interval in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int CircuitRotationMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets maximum circuit length.
    /// </summary>
    [Range(2, 10)]
    public int MaxCircuitLength { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to use diverse relay selection.
    /// </summary>
    public bool UseDiverseRelays { get; set; } = true;

    /// <summary>
    /// Gets or sets relay bandwidth accounting options.
    /// </summary>
    public RelayBandwidthOptions Bandwidth { get; set; } = new();
}

/// <summary>
/// Relay bandwidth accounting options.
/// </summary>
public sealed class RelayBandwidthOptions
{
    /// <summary>
    /// Gets or sets whether to enable bandwidth accounting.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets maximum bandwidth to contribute as relay (MB/s).
    /// </summary>
    [Range(0.1, 100)]
    public double MaxContributedBandwidthMbps { get; set; } = 10;

    /// <summary>
    /// Gets or sets bandwidth accounting window in hours.
    /// </summary>
    [Range(1, 168)]
    public int AccountingWindowHours { get; set; } = 24;
}

/// <summary>
/// Censorship resistance options.
/// </summary>
public sealed class CensorshipResistanceOptions
{
    /// <summary>
    /// Gets or sets whether censorship resistance is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets bridge discovery options.
    /// </summary>
    public BridgeDiscoveryOptions BridgeDiscovery { get; set; } = new();

    /// <summary>
    /// Gets or sets domain fronting options.
    /// </summary>
    public DomainFrontingOptions DomainFronting { get; set; } = new();

    /// <summary>
    /// Gets or sets steganography options.
    /// </summary>
    public SteganographyOptions Steganography { get; set; } = new();
}

/// <summary>
/// Bridge discovery options.
/// </summary>
public sealed class BridgeDiscoveryOptions
{
    /// <summary>
    /// Gets or sets whether bridge discovery is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets email address for requesting bridges.
    /// </summary>
    public string RequestEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets custom bridge lines (manual configuration).
    /// </summary>
    public List<string> CustomBridges { get; set; } = new();

    /// <summary>
    /// Gets or sets bridge health check interval in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int HealthCheckIntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Domain fronting options.
/// </summary>
public sealed class DomainFrontingOptions
{
    /// <summary>
    /// Gets or sets whether domain fronting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets front domain for domain fronting.
    /// </summary>
    public string FrontDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets real domain (Host header).
    /// </summary>
    public string RealDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to verify front domain availability.
    /// </summary>
    public bool VerifyFrontDomain { get; set; } = true;
}

/// <summary>
/// Steganography options.
/// </summary>
public sealed class SteganographyOptions
{
    /// <summary>
    /// Gets or sets whether steganography is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets maximum image size for steganography (MB).
    /// </summary>
    [Range(0.1, 10)]
    public double MaxImageSizeMb { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to verify extracted bridge configurations.
    /// </summary>
    public bool VerifyExtractedBridges { get; set; } = true;
}

/// <summary>
/// Plausible deniability options.
/// </summary>
public sealed class PlausibleDeniabilityOptions
{
    /// <summary>
    /// Gets or sets whether plausible deniability is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets hidden volume options.
    /// </summary>
    public HiddenVolumeOptions HiddenVolumes { get; set; } = new();

    /// <summary>
    /// Gets or sets decoy pod options.
    /// </summary>
    public DecoyPodOptions DecoyPods { get; set; } = new();
}

/// <summary>
/// Hidden volume options.
/// </summary>
public sealed class HiddenVolumeOptions
{
    /// <summary>
    /// Gets or sets whether hidden volumes are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets maximum hidden volume size (GB).
    /// </summary>
    [Range(1, 1000)]
    public int MaxVolumeSizeGb { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to use Argon2 for key derivation.
    /// </summary>
    public bool UseArgon2 { get; set; } = true;

    /// <summary>
    /// Gets or sets Argon2 iterations.
    /// </summary>
    [Range(1, 100)]
    public int Argon2Iterations { get; set; } = 3;
}

/// <summary>
/// Decoy pod options.
/// </summary>
public sealed class DecoyPodOptions
{
    /// <summary>
    /// Gets or sets whether decoy pods are enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets number of decoy pods to maintain.
    /// </summary>
    [Range(1, 50)]
    public int DecoyPodCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets decoy pod activity interval in minutes.
    /// </summary>
    [Range(5, 1440)]
    public int ActivityIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to auto-join decoy pods.
    /// </summary>
    public bool AutoJoinDecoyPods { get; set; } = true;
}
