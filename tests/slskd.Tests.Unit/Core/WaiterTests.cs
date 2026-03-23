namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

public class WaiterTests
{
    [Fact]
    public async Task CancelAll_WhenMultipleWaitsShareTheSameKey_CancelsAllOfThem()
    {
        using var waiter = new Waiter();
        var key = new WaitKey("message", 1);

        var firstWait = waiter.Wait(key);
        var secondWait = waiter.Wait(key);

        waiter.CancelAll();

        await Assert.ThrowsAsync<TaskCanceledException>(() => firstWait);
        await Assert.ThrowsAsync<TaskCanceledException>(() => secondWait);
        Assert.False(waiter.IsWaitingFor(key));
    }

    [Fact]
    public async Task CancelAll_WhenMultipleKeysExist_CancelsEveryPendingWait()
    {
        using var waiter = new Waiter();
        var firstKey = new WaitKey("message", 1);
        var secondKey = new WaitKey("message", 2);

        var firstWait = waiter.Wait(firstKey);
        var secondWait = waiter.Wait(secondKey);
        var thirdWait = waiter.Wait(secondKey);

        waiter.CancelAll();

        await Assert.ThrowsAsync<TaskCanceledException>(() => firstWait);
        await Assert.ThrowsAsync<TaskCanceledException>(() => secondWait);
        await Assert.ThrowsAsync<TaskCanceledException>(() => thirdWait);
        Assert.False(waiter.IsWaitingFor(firstKey));
        Assert.False(waiter.IsWaitingFor(secondKey));
    }

    [Fact]
    public void WaitIndefinitely_DoesNotAllocateTimeoutTokenSource()
    {
        using var waiter = new Waiter();
        var key = new WaitKey("message", 3);

        _ = waiter.WaitIndefinitely(key);

        var waits = waiter.GetType().GetProperty("Waits", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Waits property was not found.");
        var waitDictionary = (System.Collections.IDictionary)waits.GetValue(waiter)!;
        var queue = waitDictionary[key]!;
        var tryPeek = queue.GetType().GetMethod("TryPeek")
            ?? throw new InvalidOperationException("TryPeek method was not found.");
        var parameters = new object?[] { null };
        Assert.True((bool)tryPeek.Invoke(queue, parameters)!);

        var pendingWait = parameters[0]!;
        var timeoutTokenSourceProperty = pendingWait.GetType().GetProperty("TimeoutTokenSource", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TimeoutTokenSource property was not found.");

        Assert.Null(timeoutTokenSourceProperty.GetValue(pendingWait));
    }
}
