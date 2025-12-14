// <copyright file="PeerDescriptorPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly Mesh.ServiceFabric.Services.HolePunchMeshService? holePunchService;
    private readonly MeshTransportOptions transportOptions;
    private readonly DescriptorSigningService signingService;

    public PeerDescriptorPublisher(
        ILogger<PeerDescriptorPublisher> logger,
        IMeshDhtClient dht,
        IOptions<MeshOptions> options,
        INatDetector natDetector,
        IOptions<MeshTransportOptions> transportOptions,
        DescriptorSigningService signingService)
    {
        this.logger = logger;
        this.dht = dht;
        this.options = options.Value;
        this.natDetector = natDetector;
        this.transportOptions = transportOptions.Value;
        this.signingService = signingService;
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
        var legacyEndpoints = await BuildLegacyEndpointsAsync(ct);

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

        // Sign the descriptor
        var signature = signingService.SignDescriptor(descriptor, new byte[32]); // TODO: Use actual private key
        descriptor.Signature = signature;

        var key = $"mesh:peer:{descriptor.PeerId}";
        var ttlSeconds = options.PeerDescriptorRefresh.DescriptorTtlSeconds;
        await dht.PutAsync(key, descriptor, ttlSeconds: ttlSeconds, ct: ct);

        logger.LogInformation("[MeshDHT] Published self descriptor {PeerId} seq={Sequence} endpoints={LegacyCount} transports={TransportCount} privacy={PrivacyMode}",
            descriptor.PeerId, descriptor.SequenceNumber, descriptor.Endpoints.Count, descriptor.TransportEndpoints.Count,
            transportOptions.Tor.PrivacyModeNoClearnetAdvertise ? "enabled" : "disabled");
    }

    private async Task<List<string>> BuildLegacyEndpointsAsync(CancellationToken ct)
    {
        // Start with configured endpoints
        var endpoints = new List<string>(options.SelfEndpoints);

        // If no configured endpoints, try to detect actual network interfaces
        if (!endpoints.Any())
        {
            endpoints.AddRange(DetectNetworkEndpoints());
            logger.LogInformation("[MeshDHT] No configured endpoints, detected {Count} network interfaces", endpoints.Count);
        }
        else
        {
            // Supplement configured endpoints with detected ones if they differ
            var detectedEndpoints = DetectNetworkEndpoints();
            foreach (var detected in detectedEndpoints)
            {
                if (!endpoints.Contains(detected))
                {
                    endpoints.Add(detected);
                    logger.LogDebug("[MeshDHT] Added detected endpoint: {Endpoint}", detected);
                }
            }
        }

        // Add relay endpoints
        if (options.RelayEndpoints != null)
        {
            endpoints.AddRange(options.RelayEndpoints);
        }

        return endpoints.Distinct().ToList();
    }

    private List<TransportEndpoint> BuildTransportEndpoints()
    {
        var endpoints = new List<TransportEndpoint>();

        // Add direct QUIC endpoint if enabled and not in privacy mode
        if (transportOptions.EnableDirect && !transportOptions.Tor.PrivacyModeNoClearnetAdvertise)
        {
            // Use detected network endpoints for direct connectivity
            var detectedEndpoints = DetectNetworkEndpoints();
            foreach (var endpointStr in detectedEndpoints)
            {
                try
                {
                    // Parse "host:port" format
                    var parts = endpointStr.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                    {
                        endpoints.Add(new TransportEndpoint
                        {
                            TransportType = TransportType.DirectQuic,
                            Host = parts[0],
                            Port = port,
                            Scope = TransportScope.ControlAndData,
                            Preference = 0, // Highest preference for direct
                            Cost = 0
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create direct QUIC endpoint from {Endpoint}", endpointStr);
                }
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

    /// <summary>
    /// Detects available network endpoints from active network interfaces.
    /// Returns endpoints in the format "ip:port" for common ports.
    /// </summary>
    private IEnumerable<string> DetectNetworkEndpoints()
    {
        var endpoints = new List<string>();

        try
        {
            // Get all active network interfaces
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in networkInterfaces)
            {
                var ipProperties = ni.GetIPProperties();

                // Get IPv4 addresses
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        var ip = unicast.Address;
                        if (!IPAddress.IsLoopback(ip) && ip.ToString() != "0.0.0.0")
                        {
                            // Add common Soulseek ports
                            endpoints.Add($"{ip}:2234"); // Default Soulseek port
                            endpoints.Add($"{ip}:2235"); // Alternative port
                        }
                    }
                }

                // Get IPv6 addresses (global scope only)
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        var ip = unicast.Address;
                        if (!IPAddress.IsLoopback(ip) &&
                            ip.ToString() != "::" &&
                            !ip.ToString().StartsWith("fe80::", StringComparison.OrdinalIgnoreCase)) // Skip link-local
                        {
                            // Add IPv6 endpoints with brackets
                            endpoints.Add($"[{ip}]:2234");
                            endpoints.Add($"[{ip}]:2235");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MeshDHT] Failed to detect network endpoints");
        }

        return endpoints;
    }
}
