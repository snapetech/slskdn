using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public PeerDescriptorPublisher(
        ILogger<PeerDescriptorPublisher> logger,
        IMeshDhtClient dht,
        IOptions<MeshOptions> options,
        INatDetector natDetector)
    {
        this.logger = logger;
        this.dht = dht;
        this.options = options.Value;
        this.natDetector = natDetector;
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

        var descriptor = new MeshPeerDescriptor
        {
            PeerId = options.SelfPeerId,
            Endpoints = endpoints.Distinct().ToList(), // Remove duplicates
            NatType = nat.ToString().ToLowerInvariant(),
            RelayRequired = nat == NatType.Symmetric
        };

        var key = $"mesh:peer:{descriptor.PeerId}";
        var ttlSeconds = options.PeerDescriptorRefresh.DescriptorTtlSeconds;
        await dht.PutAsync(key, descriptor, ttlSeconds: ttlSeconds, ct: ct);
        logger.LogInformation("[MeshDHT] Published self descriptor {PeerId} endpoints={Count} nat={Nat}", descriptor.PeerId, descriptor.Endpoints.Count, descriptor.NatType);
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
