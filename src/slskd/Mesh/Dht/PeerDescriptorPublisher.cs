// <copyright file="PeerDescriptorPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;
using slskd.Mesh.Overlay;
using slskd.Mesh.Transport;
using static slskd.Mesh.TransportType;

namespace slskd.Mesh.Dht;

/// <summary>
/// Publishes our peer descriptor to the mesh DHT and refreshes it.
/// </summary>
public interface IPeerDescriptorPublisher
{
    Task PublishSelfAsync(CancellationToken ct = default);
    Task MarkPeerRequiresRelayAsync(string peerId, CancellationToken ct = default);
}

public class PeerDescriptorPublisher : IPeerDescriptorPublisher
{
    private readonly ILogger<PeerDescriptorPublisher> logger;
    private readonly IMeshDhtClient dht;
    private readonly MeshOptions options;
    private readonly INatDetector natDetector;
    private readonly MeshTransportOptions transportOptions;
    private readonly OverlayOptions overlayOptions;
    private readonly DescriptorSigningService signingService;
    private readonly Mesh.Overlay.IKeyStore? keyStore;

    public PeerDescriptorPublisher(
        ILogger<PeerDescriptorPublisher> logger,
        IMeshDhtClient dht,
        IOptions<MeshOptions> options,
        INatDetector natDetector,
        IOptions<MeshTransportOptions> transportOptions,
        IOptions<OverlayOptions> overlayOptions,
        DescriptorSigningService signingService,
        Mesh.Overlay.IKeyStore? keyStore = null)
    {
        this.logger = logger;
        this.dht = dht;
        this.options = options.Value;
        this.natDetector = natDetector;
        this.transportOptions = transportOptions.Value;
        this.overlayOptions = overlayOptions.Value;
        this.signingService = signingService;
        this.keyStore = keyStore;
    }

    /// <summary>
    /// Mark a peer as requiring relay due to hole punching failures.
    /// </summary>
    public async Task MarkPeerRequiresRelayAsync(string peerId, CancellationToken ct = default)
    {
        try
        {
            // Get the current peer descriptor
            var key = $"mesh:peer:{peerId}";
            var descriptor = await dht.GetAsync<MeshPeerDescriptor>(key, ct);

            if (descriptor != null)
            {
                // Mark as requiring relay
                var updatedDescriptor = new MeshPeerDescriptor
                {
                    PeerId = descriptor.PeerId,
                    Endpoints = descriptor.Endpoints,
                    NatType = descriptor.NatType,
                    RelayRequired = true, // Mark as requiring relay
                    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                await dht.PutAsync(key, updatedDescriptor, ttlSeconds: 3600, ct: ct);

                logger.LogInformation(
                    "[MeshDHT] Marked peer {PeerId} as requiring relay due to hole punching failures",
                    peerId);
            }
            else
            {
                logger.LogWarning(
                    "[MeshDHT] Cannot mark peer {PeerId} as requiring relay - descriptor not found",
                    peerId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MeshDHT] Failed to mark peer {PeerId} as requiring relay", peerId);
        }
    }

    public async Task PublishSelfAsync(CancellationToken ct = default)
    {
        var nat = await natDetector.DetectAsync(ct);

        // Build legacy endpoints for backward compatibility
        var legacyEndpoints = await BuildLegacyEndpointsAsync();

        // Build transport endpoints based on configuration and privacy settings
        var transportEndpoints = BuildTransportEndpoints();

        // Determine sequence number for anti-rollback protection
        var sequenceNumber = signingService.GetLastAcceptedSequence(options.SelfPeerId) + 1;

        var descriptor = new MeshPeerDescriptor
        {
            PeerId = options.SelfPeerId,
            Endpoints = legacyEndpoints,
            NatType = nat.ToString().ToLowerInvariant(),
            RelayRequired = nat == NatType.Symmetric,
            SequenceNumber = sequenceNumber,
            TransportEndpoints = transportEndpoints,

            // TODO: Add certificate pins and signing keys when identity system is complete
            CertificatePins = new List<string>(),
            ControlSigningKeys = new List<string>()
        };

        // Sign the descriptor - REQUIRE real keys for security
        if (keyStore == null)
        {
            throw new InvalidOperationException("KeyStore is required for mesh peer publishing. Configure mesh identity keys.");
        }

        byte[] privateKey;
        try
        {
            var keyPair = keyStore.Current;
            privateKey = keyPair.PrivateKey;

            // Validate the key is not placeholder (all zeros)
            if (privateKey.All(b => b == 0))
            {
                throw new InvalidOperationException("Private key is placeholder (all zeros). Configure real mesh identity keys.");
            }

            logger.LogDebug("[MeshDHT] Using real private key from KeyStore for signing");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MeshDHT] Failed to get valid private key from KeyStore - cannot publish peer descriptor securely");
            throw new InvalidOperationException("Mesh identity keys are required for secure peer publishing", ex);
        }

        try
        {
            var signature = signingService.SignDescriptor(descriptor, privateKey);
            descriptor.Signature = signature;
            logger.LogDebug("[MeshDHT] Successfully signed peer descriptor");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MeshDHT] Failed to sign descriptor with real key");
            throw new InvalidOperationException("Failed to sign peer descriptor with configured key", ex);
        }

        var key = $"mesh:peer:{descriptor.PeerId}";
        var ttlSeconds = options.PeerDescriptorRefresh.DescriptorTtlSeconds;
        await dht.PutAsync(key, descriptor, ttlSeconds: ttlSeconds, ct: ct);

        logger.LogInformation("[MeshDHT] Published self descriptor {PeerId} seq={Sequence} endpoints={LegacyCount} transports={TransportCount} privacy={PrivacyMode}",
            descriptor.PeerId, descriptor.SequenceNumber, descriptor.Endpoints.Count, descriptor.TransportEndpoints.Count,
            transportOptions.Tor.PrivacyModeNoClearnetAdvertise ? "enabled" : "disabled");
    }

    private Task<List<string>> BuildLegacyEndpointsAsync()
    {
        var endpoints = new List<string>(options.SelfEndpoints);

        if (!endpoints.Any())
        {
            endpoints.AddRange(DetectLegacyNetworkEndpoints());
            if (endpoints.Count == 0)
            {
                logger.LogWarning("[MeshDHT] No configured endpoints and no public-routable network interfaces detected; descriptor will rely on DHT announce and relay endpoints only");
            }
            else
            {
                logger.LogInformation("[MeshDHT] No configured endpoints, detected {Count} public-routable network interfaces", endpoints.Count);
            }
        }
        else
        {
            logger.LogDebug("[MeshDHT] Using {Count} configured self endpoint(s); automatic endpoint detection skipped", endpoints.Count);
        }

        // Add relay endpoints
        if (options.RelayEndpoints != null)
        {
            endpoints.AddRange(options.RelayEndpoints);
        }

        return Task.FromResult(endpoints.Distinct().ToList());
    }

    private List<TransportEndpoint> BuildTransportEndpoints()
    {
        var endpoints = new List<TransportEndpoint>();

        // Add direct QUIC endpoint only when the running host can actually accept QUIC.
        if (ShouldAdvertiseDirectTransport(transportOptions.EnableDirect, transportOptions.Tor.PrivacyModeNoClearnetAdvertise, QuicRuntime.IsAvailable()))
        {
            foreach (var host in DetectNetworkHosts())
            {
                endpoints.Add(new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = host,
                    Port = overlayOptions.QuicListenPort,
                    Scope = TransportScope.ControlAndData,
                    Preference = 0,
                    Cost = 0
                });
            }
        }

        // Add Tor onion endpoint if enabled and configured
        if (transportOptions.Tor.Enabled && transportOptions.Tor.AdvertiseOnion && !string.IsNullOrEmpty(transportOptions.Tor.OnionAddress))
        {
            endpoints.Add(new TransportEndpoint
            {
                TransportType = TransportType.TorOnionQuic,
                Host = transportOptions.Tor.OnionAddress,
                Port = transportOptions.Tor.OnionPort ?? 443, // Default onion port
                Scope = transportOptions.Tor.AllowDataOverTor ? TransportScope.ControlAndData : TransportScope.Control,
                Preference = 1, // Second preference after direct
                Cost = 10 // Higher cost due to latency
            });
        }

        // Add I2P endpoint if enabled and configured
        if (transportOptions.I2P.Enabled && transportOptions.I2P.AdvertiseI2P && !string.IsNullOrEmpty(transportOptions.I2P.DestinationAddress))
        {
            endpoints.Add(new TransportEndpoint
            {
                TransportType = TransportType.I2PQuic,
                Host = transportOptions.I2P.DestinationAddress,
                Port = 443, // Standard port for I2P destinations
                Scope = transportOptions.I2P.AllowDataOverI2p ? TransportScope.ControlAndData : TransportScope.Control,
                Preference = 2, // Third preference
                Cost = 15 // Even higher cost due to I2P latency
            });
        }

        logger.LogDebug("Built {Count} transport endpoints: {Endpoints}",
            endpoints.Count, string.Join(", ", endpoints.Select(e => $"{e.TransportType}({e.Preference})")));

        return endpoints;
    }

    internal static bool TryParseAdvertisedEndpoint(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        if (endpoint[0] == '[')
        {
            var closingBracketIndex = endpoint.IndexOf(']');
            if (closingBracketIndex <= 1 ||
                closingBracketIndex + 2 >= endpoint.Length ||
                endpoint[closingBracketIndex + 1] != ':' ||
                !int.TryParse(endpoint[(closingBracketIndex + 2)..], out port))
            {
                return false;
            }

            host = endpoint[1..closingBracketIndex];
            return port is > 0 and <= ushort.MaxValue;
        }

        var separatorIndex = endpoint.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == endpoint.Length - 1)
        {
            return false;
        }

        host = endpoint[..separatorIndex];
        return int.TryParse(endpoint[(separatorIndex + 1)..], out port) && port is > 0 and <= ushort.MaxValue;
    }

    internal static bool ShouldAdvertiseDirectTransport(bool enableDirect, bool privacyModeNoClearnetAdvertise, bool quicIsSupported)
    {
        return enableDirect && !privacyModeNoClearnetAdvertise && quicIsSupported;
    }

    internal static string FormatUdpLegacyEndpoint(string host, int port)
    {
        return host.Contains(':', StringComparison.Ordinal) ? $"udp://[{host}]:{port}" : $"udp://{host}:{port}";
    }

    private IEnumerable<string> DetectLegacyNetworkEndpoints()
    {
        return DetectNetworkHosts().Select(host => FormatUdpLegacyEndpoint(host, overlayOptions.ListenPort));
    }

    /// <summary>
    /// Detects routable host addresses from active network interfaces.
    /// </summary>
    private IEnumerable<string> DetectNetworkHosts()
    {
        var hosts = new List<string>();

        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in networkInterfaces)
            {
                var ipProperties = ni.GetIPProperties();

                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    var ip = unicast.Address;
                    if (IsPubliclyRoutableAddress(ip))
                    {
                        hosts.Add(ip.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDHT] Failed to detect network endpoints");
        }

        return hosts.Distinct();
    }

    internal static bool IsPubliclyRoutableAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IpRangeClassifier.Classify(ip) != IpRangeClassifier.IpClassification.Public)
        {
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return !IsNonAdvertisablePublicIpv4(bytes);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !ip.IsIPv6SiteLocal;
        }

        return false;
    }

    private static bool IsNonAdvertisablePublicIpv4(byte[] bytes)
    {
        return
            (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
            (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
            (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
            (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) ||
            (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19));
    }
}
