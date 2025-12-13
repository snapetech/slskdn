using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.Nat;

namespace slskd.Mesh;

/// <summary>
/// Advanced mesh operations for diagnostics and experimental features.
/// Provides route diagnostics, transport statistics, and mesh health metrics.
/// </summary>
public class MeshAdvanced : IMeshAdvanced
{
    private readonly ILogger<MeshAdvanced> logger;
    private readonly IMeshDirectory directory;
    private readonly MeshStatsCollector statsCollector;
    private readonly IMeshDhtClient dhtClient;
    private readonly INatTraversalService natTraversal;

    public MeshAdvanced(
        ILogger<MeshAdvanced> logger,
        IMeshDirectory directory,
        MeshStatsCollector statsCollector,
        IMeshDhtClient dhtClient,
        INatTraversalService natTraversal)
    {
        this.logger = logger;
        this.directory = directory;
        this.statsCollector = statsCollector;
        this.dhtClient = dhtClient;
        this.natTraversal = natTraversal;
    }

    public async Task<IReadOnlyList<MeshPeerDescriptor>> DiscoverPeersAdvancedAsync(string contentId, MeshTransportPreference preference, CancellationToken ct = default)
    {
        var peers = await directory.FindPeersByContentAsync(contentId, ct);
        logger.LogDebug("[MeshAdvanced] Discover peers for {ContentId} pref={Pref} found={Count}", contentId, preference, peers.Count);
        return peers;
    }

    public async Task<IReadOnlyList<MeshRouteDiagnostics>> TraceRoutesAsync(string peerId, CancellationToken ct = default)
    {
        var diagnostics = new List<MeshRouteDiagnostics>();
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Step 1: Check DHT lookup path
            var dhtLookupStart = DateTimeOffset.UtcNow;
            var peerDescriptor = await directory.FindPeerByIdAsync(peerId, ct);
            var dhtLookupTime = (DateTimeOffset.UtcNow - dhtLookupStart).TotalMilliseconds;

            if (peerDescriptor != null)
            {
                // Peer found in DHT
                diagnostics.Add(new MeshRouteDiagnostics(
                    peerId,
                    "dht",
                    1,
                    false,
                    new[] { (long)dhtLookupTime }));

                logger.LogDebug("[MeshRoute] Peer {PeerId} found via DHT lookup in {Time}ms", peerId, dhtLookupTime);
            }
            else
            {
                // Peer not found in DHT, check if we can reach via overlay or other means
                diagnostics.Add(new MeshRouteDiagnostics(
                    peerId,
                    "dht",
                    1,
                    false,
                    new[] { (long)dhtLookupTime }));

                logger.LogDebug("[MeshRoute] Peer {PeerId} not found in DHT", peerId);
            }

            // Step 2: Check NAT traversal status
            var natCheckStart = DateTimeOffset.UtcNow;

            // Get current transport stats to see NAT type
            var transportStats = await statsCollector.GetStatsAsync();
            var natTraversalAttempted = transportStats.DetectedNatType != NatType.Direct &&
                                       transportStats.DetectedNatType != NatType.Unknown;

            var natCheckTime = (DateTimeOffset.UtcNow - natCheckStart).TotalMilliseconds;

            // Add NAT traversal diagnostic if applicable
            if (natTraversalAttempted)
            {
                diagnostics.Add(new MeshRouteDiagnostics(
                    peerId,
                    "nat-traversal",
                    0, // NAT traversal doesn't add hops
                    true,
                    new[] { (long)natCheckTime }));

                logger.LogDebug("[MeshRoute] NAT traversal would be attempted for {PeerId} (NAT: {NatType})",
                    peerId, transportStats.DetectedNatType);
            }

            // Step 3: Determine transport type that would be used
            var transportType = DetermineTransportType(peerDescriptor, transportStats);

            // If we have a peer descriptor with endpoints, simulate connection attempt timing
            if (peerDescriptor?.Address != null)
            {
                var connectionSimStart = DateTimeOffset.UtcNow;
                // Simulate connection timing (in real implementation this would be actual connection attempt)
                await Task.Delay(1, ct); // Minimal delay for simulation
                var connectionTime = (DateTimeOffset.UtcNow - connectionSimStart).TotalMilliseconds;

                diagnostics.Add(new MeshRouteDiagnostics(
                    peerId,
                    transportType,
                    peerDescriptor != null ? 1 : 0,
                    natTraversalAttempted,
                    new[] { (long)connectionTime }));

                logger.LogDebug("[MeshRoute] Transport {Transport} would be used for {PeerId} in {Time}ms",
                    transportType, peerId, connectionTime);
            }

            var totalTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            logger.LogInformation("[MeshRoute] Route diagnostics completed for {PeerId} in {Time}ms with {HopCount} hops",
                peerId, totalTime, diagnostics.Count);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MeshRoute] Failed to trace routes for {PeerId}", peerId);

            // Return basic diagnostic on error
            diagnostics.Add(new MeshRouteDiagnostics(
                peerId,
                "unknown",
                0,
                false,
                new[] { (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds }));
        }

        return diagnostics;
    }

    private string DetermineTransportType(MeshPeerDescriptor? peerDescriptor, MeshTransportStats transportStats)
    {
        // Determine the most likely transport type based on peer descriptor and current mesh state
        if (peerDescriptor?.Address != null)
        {
            // Direct endpoint available
            if (transportStats.DetectedNatType == NatType.Direct)
            {
                return "direct-udp";
            }
            else if (transportStats.DetectedNatType == NatType.Symmetric)
            {
                return "relay-fallback";
            }
            else
            {
                return "udp-hole-punch";
            }
        }
        else if (transportStats.ActiveOverlaySessions > 0)
        {
            // No direct endpoint, but overlay connections available
            return "overlay-relay";
        }
        else
        {
            // Fallback to DHT-only routing
            return "dht-relay";
        }
    }

    public async Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct = default)
    {
        // Get real stats from collector
        var stats = await statsCollector.GetStatsAsync();
        logger.LogDebug(
            "[MeshAdvanced] Transport stats: DHT={DhtNodes}, Overlay={OverlayConns}, NAT={NatType}",
            stats.ActiveDhtSessions,
            stats.ActiveOverlaySessions,
            stats.DetectedNatType);
        return stats;
    }
}
