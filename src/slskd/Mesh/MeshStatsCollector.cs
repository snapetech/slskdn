// <copyright file="MeshStatsCollector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Aggregates transport statistics from mesh services for diagnostics.
/// </summary>
public class MeshStatsCollector : IMeshStatsCollector
{
    private readonly ILogger<MeshStatsCollector> logger;
    private readonly Lazy<INatDetector> natDetector;
    private readonly Lazy<Dht.InMemoryDhtClient> dhtClient;
    private readonly Lazy<Overlay.QuicOverlayServer> overlayServer;
    private readonly Lazy<Overlay.QuicOverlayClient> overlayClient;

    // Statistics tracking
    private long messagesSent;
    private long messagesReceived;
    private long dhtOperations;
    private long peerChurnEvents;
    private readonly Stopwatch dhtOpsTimer = new();
    private int routingTableSize;
    private int bootstrapPeers;

    // Public methods for tracking statistics
    public void RecordMessageSent() => Interlocked.Increment(ref messagesSent);
    public void RecordMessageReceived() => Interlocked.Increment(ref messagesReceived);
    public void RecordDhtOperation() => Interlocked.Increment(ref dhtOperations);
    public void RecordPeerChurn() => Interlocked.Increment(ref peerChurnEvents);
    public void UpdateRoutingTableSize(int size) => routingTableSize = size;
    public void UpdateBootstrapPeers(int count) => bootstrapPeers = count;

    public MeshStatsCollector(
        ILogger<MeshStatsCollector> logger,
        IServiceProvider serviceProvider)
    {
        logger.LogInformation("[MeshStatsCollector] Constructor called");
        this.logger = logger;
        
        // Use Lazy to avoid circular dependencies and handle optional services
        logger.LogInformation("[MeshStatsCollector] Creating lazy service resolvers...");
        this.natDetector = new Lazy<INatDetector>(() => 
            serviceProvider.GetService(typeof(INatDetector)) as INatDetector);
        this.dhtClient = new Lazy<Dht.InMemoryDhtClient>(() => 
            serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient);
        this.overlayServer = new Lazy<Overlay.QuicOverlayServer>(() => 
            serviceProvider.GetService(typeof(Overlay.QuicOverlayServer)) as Overlay.QuicOverlayServer);
        this.overlayClient = new Lazy<Overlay.QuicOverlayClient>(() => 
            serviceProvider.GetService(typeof(Overlay.IOverlayClient)) as Overlay.QuicOverlayClient);
        logger.LogInformation("[MeshStatsCollector] Constructor completed");
    }

    /// <summary>
    /// Gets current mesh transport statistics.
    /// </summary>
        public async Task<MeshTransportStats> GetStatsAsync()
    {
        try
        {
            var dhtNodes = 0;
            var overlayConnections = 0;
            var natType = NatType.Unknown;
            var totalPeers = 0;
            double dhtOpsPerSecond = 0.0;

            // DHT statistics
            if (dhtClient.Value != null)
            {
                try
                {
                    dhtNodes = dhtClient.Value.GetNodeCount();
                    totalPeers = dhtNodes; // For now, total peers = DHT nodes

                    // Calculate DHT operations per second
                    var elapsedSeconds = dhtOpsTimer.Elapsed.TotalSeconds;
                    if (elapsedSeconds > 0)
                    {
                        dhtOpsPerSecond = dhtOperations / elapsedSeconds;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to get DHT statistics");
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

            // NAT type - perform detection if not already known
            if (natDetector.Value is StunNatDetector stunDetector)
            {
                try
                {
                    // If we don't have a cached result, perform detection
                    // Use cached value if available to avoid blocking health checks
                    if (stunDetector.LastDetectedType == NatType.Unknown)
                    {
                        logger.LogDebug("Performing NAT detection for mesh stats");
                        // Add timeout to NAT detection to prevent hanging health checks
                        using var natTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        natType = await stunDetector.DetectAsync(natTimeoutCts.Token);
                    }
                    else
                    {
                        natType = stunDetector.LastDetectedType;
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("NAT detection timed out - using Unknown");
                    natType = NatType.Unknown;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to detect NAT type");
                    natType = NatType.Unknown;
                }
            }

            return new MeshTransportStats(
                ActiveDhtSessions: dhtNodes,
                ActiveOverlaySessions: overlayConnections,
                ActiveMirroredSessions: 0, // Not implemented yet
                DetectedNatType: natType,
                TotalPeers: totalPeers,
                MessagesSent: messagesSent,
                MessagesReceived: messagesReceived,
                DhtOperationsPerSecond: dhtOpsPerSecond,
                RoutingTableSize: routingTableSize,
                BootstrapPeers: bootstrapPeers,
                PeerChurnEvents: peerChurnEvents);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to collect mesh transport stats");
            return new MeshTransportStats(0, 0, 0, NatType.Unknown);
        }
    }
}
