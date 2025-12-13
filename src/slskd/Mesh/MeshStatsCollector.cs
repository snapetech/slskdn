namespace slskd.Mesh;

using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregates transport statistics from mesh services for diagnostics.
/// </summary>
public class MeshStatsCollector
{
    private readonly ILogger<MeshStatsCollector> logger;
    private readonly Lazy<INatDetector> natDetector;
    private readonly Lazy<Dht.InMemoryDhtClient> dhtClient;
    private readonly Lazy<Overlay.QuicOverlayServer> overlayServer;
    private readonly Lazy<Overlay.QuicOverlayClient> overlayClient;

    public MeshStatsCollector(
        ILogger<MeshStatsCollector> logger,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        
        // Use Lazy to avoid circular dependencies and handle optional services
        this.natDetector = new Lazy<INatDetector>(() => 
            serviceProvider.GetService(typeof(INatDetector)) as INatDetector);
        this.dhtClient = new Lazy<Dht.InMemoryDhtClient>(() => 
            serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient);
        this.overlayServer = new Lazy<Overlay.QuicOverlayServer>(() => 
            serviceProvider.GetService(typeof(Overlay.QuicOverlayServer)) as Overlay.QuicOverlayServer);
        this.overlayClient = new Lazy<Overlay.QuicOverlayClient>(() => 
            serviceProvider.GetService(typeof(Overlay.IOverlayClient)) as Overlay.QuicOverlayClient);
    }

    /// <summary>
    /// Gets current mesh transport statistics.
    /// </summary>
    public MeshTransportStats GetStats()
    {
        try
        {
            var dhtNodes = 0;
            var overlayConnections = 0;
            var natType = NatType.Unknown;

            // DHT node count
            if (dhtClient.Value != null)
            {
                try
                {
                    dhtNodes = dhtClient.Value.GetNodeCount();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to get DHT node count");
                }
            }

            // Overlay connection counts
            if (overlayServer.Value != null)
            {
                try
                {
                    overlayConnections += overlayServer.Value.GetActiveConnectionCount();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to get overlay server connection count");
                }
            }

            if (overlayClient.Value != null)
            {
                try
                {
                    overlayConnections += overlayClient.Value.GetActiveConnectionCount();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to get overlay client connection count");
                }
            }

            // NAT type
            if (natDetector.Value is StunNatDetector stunDetector)
            {
                try
                {
                    natType = stunDetector.LastDetectedType;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to get NAT type");
                }
            }

            return new MeshTransportStats(
                ActiveDhtSessions: dhtNodes,
                ActiveOverlaySessions: overlayConnections,
                ActiveMirroredSessions: 0, // Not implemented yet
                DetectedNatType: natType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to collect mesh transport stats");
            return new MeshTransportStats(0, 0, 0, NatType.Unknown);
        }
    }
}














