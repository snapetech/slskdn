namespace slskd.VirtualSoulfind.Bridge;

using System.Collections.Concurrent;

/// <summary>
/// Interface for bridge dashboard data.
/// </summary>
public interface IBridgeDashboard
{
    /// <summary>
    /// Get complete dashboard data.
    /// </summary>
    Task<BridgeDashboardData> GetDashboardDataAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get connected clients.
    /// </summary>
    Task<List<ConnectedClient>> GetConnectedClientsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get bridge statistics.
    /// </summary>
    Task<BridgeStatistics> GetStatsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Record a proxied request.
    /// </summary>
    void RecordRequest(string clientId, string requestType);
    
    /// <summary>
    /// Record mesh benefit (avoided Soulseek traffic).
    /// </summary>
    void RecordMeshBenefit(long bytesViaMesh);
}

/// <summary>
/// Bridge dashboard data.
/// </summary>
public class BridgeDashboardData
{
    public BridgeHealthStatus Health { get; set; } = new();
    public List<ConnectedClient> ConnectedClients { get; set; } = new();
    public BridgeStatistics Stats { get; set; } = new();
    public MeshBenefits MeshBenefits { get; set; } = new();
}

/// <summary>
/// Connected legacy client.
/// </summary>
public class ConnectedClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientType { get; set; } = "Unknown"; // Nicotine+, SoulseekQt, etc.
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset ConnectedAt { get; set; }
    public int RequestCount { get; set; }
    public DateTimeOffset LastActivity { get; set; }
}

/// <summary>
/// Bridge statistics.
/// </summary>
public class BridgeStatistics
{
    public int TotalConnections { get; set; }
    public int CurrentConnections { get; set; }
    public int TotalSearches { get; set; }
    public int TotalDownloads { get; set; }
    public int TotalRoomJoins { get; set; }
    public long TotalBytesProxied { get; set; }
    public TimeSpan Uptime { get; set; }
}

/// <summary>
/// Mesh benefits tracking.
/// </summary>
public class MeshBenefits
{
    public long BytesViaMesh { get; set; }
    public long BytesViaSoulseek { get; set; }
    public double MeshPercentage => BytesViaMesh + BytesViaSoulseek > 0
        ? (double)BytesViaMesh / (BytesViaMesh + BytesViaSoulseek) * 100
        : 0;
    public int DisasterModeActivations { get; set; }
    public TimeSpan TimeInDisasterMode { get; set; }
}

/// <summary>
/// Bridge dashboard service.
/// </summary>
public class BridgeDashboard : IBridgeDashboard
{
    private readonly ILogger<BridgeDashboard> logger;
    private readonly ISoulfindBridgeService bridgeService;
    private readonly ConcurrentDictionary<string, ConnectedClient> clients = new();
    private readonly BridgeStatistics stats = new();
    private readonly MeshBenefits benefits = new();
    private readonly DateTimeOffset startTime = DateTimeOffset.UtcNow;

    public BridgeDashboard(
        ILogger<BridgeDashboard> logger,
        ISoulfindBridgeService bridgeService)
    {
        this.logger = logger;
        this.bridgeService = bridgeService;
    }

    public async Task<BridgeDashboardData> GetDashboardDataAsync(CancellationToken ct)
    {
        logger.LogDebug("[VSF-BRIDGE-DASHBOARD] Generating dashboard data");

        var health = await bridgeService.GetHealthAsync(ct);

        stats.CurrentConnections = clients.Count;
        stats.Uptime = DateTimeOffset.UtcNow - startTime;

        var dashboard = new BridgeDashboardData
        {
            Health = health,
            ConnectedClients = clients.Values.OrderByDescending(c => c.LastActivity).ToList(),
            Stats = stats,
            MeshBenefits = benefits
        };

        return dashboard;
    }

    public Task<List<ConnectedClient>> GetConnectedClientsAsync(CancellationToken ct)
    {
        var clientList = clients.Values
            .OrderByDescending(c => c.LastActivity)
            .ToList();

        return Task.FromResult(clientList);
    }

    public Task<BridgeStatistics> GetStatsAsync(CancellationToken ct)
    {
        stats.CurrentConnections = clients.Count;
        stats.Uptime = DateTimeOffset.UtcNow - startTime;

        return Task.FromResult(stats);
    }

    public void RecordRequest(string clientId, string requestType)
    {
        if (!clients.TryGetValue(clientId, out var client))
        {
            client = new ConnectedClient
            {
                ClientId = clientId,
                ConnectedAt = DateTimeOffset.UtcNow
            };
            clients[clientId] = client;
            stats.TotalConnections++;
        }

        client.RequestCount++;
        client.LastActivity = DateTimeOffset.UtcNow;

        // Update type-specific counters
        switch (requestType.ToLowerInvariant())
        {
            case "search":
                stats.TotalSearches++;
                break;
            case "download":
                stats.TotalDownloads++;
                break;
            case "room":
                stats.TotalRoomJoins++;
                break;
        }

        logger.LogDebug("[VSF-BRIDGE-DASHBOARD] Recorded {RequestType} from {ClientId}",
            requestType, clientId);
    }

    public void RecordMeshBenefit(long bytesViaMesh)
    {
        benefits.BytesViaMesh += bytesViaMesh;
        stats.TotalBytesProxied += bytesViaMesh;

        logger.LogDebug("[VSF-BRIDGE-DASHBOARD] Recorded {Bytes} bytes via mesh", bytesViaMesh);
    }
}
