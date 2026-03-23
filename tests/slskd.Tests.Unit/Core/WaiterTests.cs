namespace slskd.Tests.Unit.Core;

using System;
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
}
