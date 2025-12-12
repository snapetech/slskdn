using slskd.Common.Security;
using System;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.Common.Security;

/// <summary>
/// Tests for work budget system.
/// </summary>
public class WorkBudgetTests
{
    [Fact]
    public void WorkBudget_StartsWithInitialUnits()
    {
        // Arrange & Act
        var budget = new WorkBudget(10);

        // Assert
        Assert.Equal(10, budget.RemainingUnits);
        Assert.Equal(10, budget.InitialUnits);
        Assert.Equal(0, budget.ConsumedUnits);
        Assert.False(budget.IsExhausted);
    }

    [Fact]
    public void TryConsume_WithSufficientBudget_ReturnsTrue()
    {
        // Arrange
        var budget = new WorkBudget(10);

        // Act
        var result = budget.TryConsume(5, "test operation");

        // Assert
        Assert.True(result);
        Assert.Equal(5, budget.RemainingUnits);
        Assert.Equal(5, budget.ConsumedUnits);
    }

    [Fact]
    public void TryConsume_WithInsufficientBudget_ReturnsFalse()
    {
        // Arrange
        var budget = new WorkBudget(10);

        // Act
        var result = budget.TryConsume(15, "expensive operation");

        // Assert
        Assert.False(result);
        Assert.Equal(10, budget.RemainingUnits); // Unchanged
        Assert.Equal(0, budget.ConsumedUnits);
    }

    [Fact]
    public void TryConsume_MultipleSmallConsumptions_WorksCorrectly()
    {
        // Arrange
        var budget = new WorkBudget(10);

        // Act & Assert
        Assert.True(budget.TryConsume(3, "op1"));
        Assert.Equal(7, budget.RemainingUnits);

        Assert.True(budget.TryConsume(4, "op2"));
        Assert.Equal(3, budget.RemainingUnits);

        Assert.True(budget.TryConsume(3, "op3"));
        Assert.Equal(0, budget.RemainingUnits);
        Assert.True(budget.IsExhausted);

        // Exhausted, should fail
        Assert.False(budget.TryConsume(1, "op4"));
    }

    [Fact]
    public void TryConsume_WithPredefinedCost_Works()
    {
        // Arrange
        var budget = new WorkBudget(10);

        // Act
        var result = budget.TryConsume(WorkCosts.SoulseekSearch);

        // Assert
        Assert.True(result);
        Assert.Equal(5, budget.RemainingUnits); // Soulseek search costs 5
    }

    [Fact]
    public void TryConsume_WithZeroUnits_ReturnsTrue()
    {
        // Arrange
        var budget = new WorkBudget(10);

        // Act
        var result = budget.TryConsume(0, "no-op");

        // Assert
        Assert.True(result);
        Assert.Equal(10, budget.RemainingUnits);
    }

    [Fact]
    public void TryConsume_ThreadSafe_HandlesRaceConditions()
    {
        // Arrange
        var budget = new WorkBudget(100);
        var successCount = 0;
        var tasks = new Task[20];

        // Act: 20 threads trying to consume 10 units each (200 total, but only 100 available)
        for (int i = 0; i < 20; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                if (budget.TryConsume(10, "concurrent op"))
                {
                    System.Threading.Interlocked.Increment(ref successCount);
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert: Exactly 10 operations should succeed (10 * 10 = 100 units)
        Assert.Equal(10, successCount);
        Assert.Equal(0, budget.RemainingUnits);
        Assert.True(budget.IsExhausted);
    }

    [Fact]
    public void PeerWorkBudgetTracker_CreatesBudgetForPeer()
    {
        // Arrange
        var options = new WorkBudgetOptions
        {
            MaxWorkUnitsPerCall = 10,
            MaxWorkUnitsPerPeerPerMinute = 50
        };
        var tracker = new PeerWorkBudgetTracker(options);

        // Act
        var budget = tracker.CreateBudgetForPeer("peer-1");

        // Assert: First call gets min(per-call, per-peer) = min(10, 50) = 10
        Assert.Equal(10, budget.InitialUnits);
    }

    [Fact]
    public void PeerWorkBudgetTracker_EnforcesPerPeerQuota()
    {
        // Arrange
        var options = new WorkBudgetOptions
        {
            MaxWorkUnitsPerCall = 10,
            MaxWorkUnitsPerPeerPerMinute = 30
        };
        var tracker = new PeerWorkBudgetTracker(options);

        // Act: First call
        var budget1 = tracker.CreateBudgetForPeer("peer-1");
        budget1.TryConsume(10, "call 1"); // Consume all
        tracker.RecordConsumption("peer-1", budget1.ConsumedUnits);

        // Second call
        var budget2 = tracker.CreateBudgetForPeer("peer-1");
        budget2.TryConsume(10, "call 2");
        tracker.RecordConsumption("peer-1", budget2.ConsumedUnits);

        // Third call
        var budget3 = tracker.CreateBudgetForPeer("peer-1");
        budget3.TryConsume(10, "call 3");
        tracker.RecordConsumption("peer-1", budget3.ConsumedUnits);

        // Fourth call should have limited budget (peer consumed 30, quota is 30)
        var budget4 = tracker.CreateBudgetForPeer("peer-1");

        // Assert: Peer has exhausted quota
        Assert.Equal(0, budget4.InitialUnits);
    }

    [Fact]
    public void PeerWorkBudgetTracker_IsolatesPeers()
    {
        // Arrange
        var options = new WorkBudgetOptions
        {
            MaxWorkUnitsPerCall = 10,
            MaxWorkUnitsPerPeerPerMinute = 20
        };
        var tracker = new PeerWorkBudgetTracker(options);

        // Act: Peer 1 exhausts quota
        var budget1A = tracker.CreateBudgetForPeer("peer-1");
        budget1A.TryConsume(10, "peer1-call1");
        tracker.RecordConsumption("peer-1", budget1A.ConsumedUnits);

        var budget1B = tracker.CreateBudgetForPeer("peer-1");
        budget1B.TryConsume(10, "peer1-call2");
        tracker.RecordConsumption("peer-1", budget1B.ConsumedUnits);

        // Peer 2 should have full quota
        var budget2 = tracker.CreateBudgetForPeer("peer-2");

        // Assert
        Assert.Equal(0, tracker.CreateBudgetForPeer("peer-1").InitialUnits); // Peer 1 exhausted
        Assert.Equal(10, budget2.InitialUnits); // Peer 2 unaffected
    }

    [Fact]
    public void PeerWorkBudgetTracker_GetMetrics_ReturnsCorrectData()
    {
        // Arrange
        var options = new WorkBudgetOptions
        {
            MaxWorkUnitsPerCall = 10,
            MaxWorkUnitsPerPeerPerMinute = 50
        };
        var tracker = new PeerWorkBudgetTracker(options);

        // Act: Generate some activity
        var budget1 = tracker.CreateBudgetForPeer("peer-1");
        budget1.TryConsume(5, "op1");
        tracker.RecordConsumption("peer-1", budget1.ConsumedUnits);

        var budget2 = tracker.CreateBudgetForPeer("peer-2");
        budget2.TryConsume(10, "op2");
        tracker.RecordConsumption("peer-2", budget2.ConsumedUnits);

        var metrics = tracker.GetMetrics();

        // Assert
        Assert.Equal(2, metrics.ActivePeersLastMinute);
        Assert.Equal(15, metrics.TotalWorkUnitsConsumedLastMinute);
        Assert.Equal(0, metrics.PeersNearQuota.Count); // Neither near 80% of 50
    }

    [Fact]
    public void WorkBudgetOptions_DisabledMode_GivesUnlimitedBudget()
    {
        // Arrange
        var options = new WorkBudgetOptions
        {
            Enabled = false,
            MaxWorkUnitsPerCall = 10
        };
        var tracker = new PeerWorkBudgetTracker(options);

        // Act
        var budget = tracker.CreateBudgetForPeer("peer-1");

        // Assert: Budget is very large (effectively unlimited)
        Assert.True(budget.InitialUnits > 1000000);
    }
}
