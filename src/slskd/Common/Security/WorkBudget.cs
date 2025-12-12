using System;
using System.Collections.Concurrent;

namespace slskd.Common.Security;

/// <summary>
/// Work budget system for preventing amplification attacks.
/// Tracks and limits the amount of "work" (expensive operations) that can be triggered
/// by incoming service calls.
/// </summary>
public class WorkBudget
{
    private int _remainingUnits;
    private readonly int _initialUnits;

    /// <summary>
    /// Creates a work budget with the specified initial units.
    /// </summary>
    /// <param name="initialUnits">Initial work units available</param>
    public WorkBudget(int initialUnits)
    {
        if (initialUnits < 0)
            throw new ArgumentException("Initial units must be non-negative", nameof(initialUnits));

        _initialUnits = initialUnits;
        _remainingUnits = initialUnits;
    }

    /// <summary>
    /// Gets the remaining work units.
    /// </summary>
    public int RemainingUnits => _remainingUnits;

    /// <summary>
    /// Gets the initial work units.
    /// </summary>
    public int InitialUnits => _initialUnits;

    /// <summary>
    /// Gets the consumed work units.
    /// </summary>
    public int ConsumedUnits => _initialUnits - _remainingUnits;

    /// <summary>
    /// Gets whether the budget is exhausted.
    /// </summary>
    public bool IsExhausted => _remainingUnits <= 0;

    /// <summary>
    /// Attempts to consume the specified number of work units.
    /// </summary>
    /// <param name="units">Number of units to consume</param>
    /// <param name="reason">Reason for consumption (for logging/debugging)</param>
    /// <returns>True if units were consumed, false if insufficient budget</returns>
    public bool TryConsume(int units, string reason = "")
    {
        if (units < 0)
            throw new ArgumentException("Units must be non-negative", nameof(units));

        if (units == 0)
            return true;

        // Thread-safe decrement with boundary check
        var currentRemaining = _remainingUnits;
        while (currentRemaining >= units)
        {
            var newRemaining = currentRemaining - units;
            var original = System.Threading.Interlocked.CompareExchange(
                ref _remainingUnits,
                newRemaining,
                currentRemaining);

            if (original == currentRemaining)
            {
                // Successfully consumed
                return true;
            }

            // Race condition, retry with new value
            currentRemaining = original;
        }

        // Insufficient budget
        return false;
    }

    /// <summary>
    /// Attempts to consume a predefined work cost.
    /// </summary>
    /// <param name="cost">Predefined work cost</param>
    /// <returns>True if units were consumed, false if insufficient budget</returns>
    public bool TryConsume(WorkCost cost)
    {
        return TryConsume(cost.Units, cost.Description);
    }
}

/// <summary>
/// Predefined work costs for common operations.
/// </summary>
public static class WorkCosts
{
    /// <summary>
    /// Cost of a Soulseek search operation (5 units - expensive).
    /// </summary>
    public static readonly WorkCost SoulseekSearch = new(5, "Soulseek search");

    /// <summary>
    /// Cost of a Soulseek browse operation (3 units - expensive).
    /// </summary>
    public static readonly WorkCost SoulseekBrowse = new(3, "Soulseek browse");

    /// <summary>
    /// Cost of a torrent metadata fetch (3 units - network-bound).
    /// </summary>
    public static readonly WorkCost TorrentMetadataFetch = new(3, "Torrent metadata fetch");

    /// <summary>
    /// Cost of an outbound mesh RPC call (1 unit - relatively cheap).
    /// </summary>
    public static readonly WorkCost MeshRpcCall = new(1, "Mesh RPC call");

    /// <summary>
    /// Cost of a catalog fetch via proxy (2 units - network + parsing).
    /// </summary>
    public static readonly WorkCost CatalogFetch = new(2, "Catalog fetch");

    /// <summary>
    /// Cost of a content chunk relay (1 unit - file I/O).
    /// </summary>
    public static readonly WorkCost ContentChunkRelay = new(1, "Content chunk relay");

    /// <summary>
    /// Cost of a trusted relay message (1 unit - forwarding).
    /// </summary>
    public static readonly WorkCost TrustedRelay = new(1, "Trusted relay");

    /// <summary>
    /// Cost of VirtualSoulfind intent creation (2 units - planning overhead).
    /// </summary>
    public static readonly WorkCost VirtualSoulfindIntent = new(2, "VirtualSoulfind intent");

    /// <summary>
    /// Cost of VirtualSoulfind plan execution (varies, but baseline of 3).
    /// </summary>
    public static readonly WorkCost VirtualSoulfindPlanExecution = new(3, "VirtualSoulfind plan execution");
}

/// <summary>
/// Represents a work cost with description.
/// </summary>
public sealed record WorkCost(int Units, string Description)
{
    /// <summary>
    /// Gets the number of work units.
    /// </summary>
    public int Units { get; init; } = Units;

    /// <summary>
    /// Gets the description of this cost.
    /// </summary>
    public string Description { get; init; } = Description;
}

/// <summary>
/// Configuration options for work budget system.
/// </summary>
public class WorkBudgetOptions
{
    /// <summary>
    /// Maximum work units per call (default: 10).
    /// Prevents a single call from triggering excessive downstream work.
    /// </summary>
    public int MaxWorkUnitsPerCall { get; set; } = 10;

    /// <summary>
    /// Maximum work units per peer per minute (default: 50).
    /// Prevents a single peer from monopolizing expensive operations.
    /// </summary>
    public int MaxWorkUnitsPerPeerPerMinute { get; set; } = 50;

    /// <summary>
    /// Enable work budget enforcement (default: true).
    /// Can be disabled for testing or debugging.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Tracks per-peer work budget consumption over time.
/// </summary>
public class PeerWorkBudgetTracker
{
    private readonly WorkBudgetOptions _options;
    private readonly ConcurrentDictionary<string, PeerWorkWindow> _peerWindows = new();

    /// <summary>
    /// Creates a peer work budget tracker with the specified options.
    /// </summary>
    public PeerWorkBudgetTracker(WorkBudgetOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates a work budget for a peer based on their current consumption.
    /// </summary>
    /// <param name="peerId">Peer identifier</param>
    /// <returns>Work budget with remaining units for this peer</returns>
    public WorkBudget CreateBudgetForPeer(string peerId)
    {
        if (!_options.Enabled)
        {
            // If disabled, give unlimited budget
            return new WorkBudget(int.MaxValue / 2); // Divide by 2 to avoid overflow
        }

        var now = DateTimeOffset.UtcNow;
        var window = _peerWindows.AddOrUpdate(
            peerId,
            _ => new PeerWorkWindow
            {
                ConsumedUnits = 0,
                WindowStart = now
            },
            (_, existing) =>
            {
                // Reset window if expired (> 1 minute old)
                if (now - existing.WindowStart > TimeSpan.FromMinutes(1))
                {
                    return new PeerWorkWindow
                    {
                        ConsumedUnits = 0,
                        WindowStart = now
                    };
                }
                return existing;
            });

        // Calculate remaining units for this peer in current window
        var remainingPeerUnits = Math.Max(0, _options.MaxWorkUnitsPerPeerPerMinute - window.ConsumedUnits);

        // Budget for this call is the minimum of per-call limit and remaining peer quota
        var budgetForCall = Math.Min(_options.MaxWorkUnitsPerCall, remainingPeerUnits);

        return new WorkBudget(budgetForCall);
    }

    /// <summary>
    /// Records work consumption for a peer after a call completes.
    /// </summary>
    /// <param name="peerId">Peer identifier</param>
    /// <param name="consumedUnits">Units consumed during the call</param>
    public void RecordConsumption(string peerId, int consumedUnits)
    {
        if (!_options.Enabled || consumedUnits <= 0)
            return;

        _peerWindows.AddOrUpdate(
            peerId,
            _ => new PeerWorkWindow
            {
                ConsumedUnits = consumedUnits,
                WindowStart = DateTimeOffset.UtcNow
            },
            (_, existing) =>
            {
                existing.ConsumedUnits += consumedUnits;
                return existing;
            });
    }

    /// <summary>
    /// Gets work budget metrics for monitoring.
    /// </summary>
    public WorkBudgetMetrics GetMetrics()
    {
        var now = DateTimeOffset.UtcNow;
        var activePeers = _peerWindows
            .Where(kvp => now - kvp.Value.WindowStart <= TimeSpan.FromMinutes(1))
            .ToList();

        return new WorkBudgetMetrics
        {
            TotalPeersTracked = _peerWindows.Count,
            ActivePeersLastMinute = activePeers.Count,
            TotalWorkUnitsConsumedLastMinute = activePeers.Sum(kvp => kvp.Value.ConsumedUnits),
            PeersNearQuota = activePeers
                .Where(kvp => kvp.Value.ConsumedUnits > _options.MaxWorkUnitsPerPeerPerMinute * 0.8)
                .Select(kvp => new PeerWorkInfo
                {
                    PeerId = kvp.Key,
                    ConsumedUnits = kvp.Value.ConsumedUnits,
                    RemainingUnits = Math.Max(0, _options.MaxWorkUnitsPerPeerPerMinute - kvp.Value.ConsumedUnits)
                })
                .ToList()
        };
    }
}

/// <summary>
/// Per-peer work consumption window.
/// </summary>
internal class PeerWorkWindow
{
    public int ConsumedUnits { get; set; }
    public DateTimeOffset WindowStart { get; set; }
}

/// <summary>
/// Work budget metrics for monitoring.
/// </summary>
public sealed record WorkBudgetMetrics
{
    public int TotalPeersTracked { get; init; }
    public int ActivePeersLastMinute { get; init; }
    public int TotalWorkUnitsConsumedLastMinute { get; init; }
    public List<PeerWorkInfo> PeersNearQuota { get; init; } = new();
}

/// <summary>
/// Per-peer work information.
/// </summary>
public sealed record PeerWorkInfo
{
    public string PeerId { get; init; } = string.Empty;
    public int ConsumedUnits { get; init; }
    public int RemainingUnits { get; init; }
}

