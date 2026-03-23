namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using Xunit;

public class CallbackInfrastructureTests
{
    [Fact]
    public void TimedCounter_WhenElapsedInterleavesWithCountUp_PreservesTotalCount()
    {
        long captured = -1;
        using var counter = new TimedCounter(TimeSpan.FromMinutes(1), value => captured = value);

        counter.CountUp(2);
        counter.CountUp(3);

        var elapsedMethod = typeof(TimedCounter).GetMethod(
            "Elapsed",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TimedCounter.Elapsed method was not found.");

        elapsedMethod.Invoke(counter, [null, null!]);

        Assert.Equal(5, captured);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public void RateLimiter_WhenStagedActionThrows_DropsFailedActionBeforeNextTick()
    {
        using var rateLimiter = new RateLimiter(interval: 1000);
        var attempts = 0;

        rateLimiter.Invoke(() => { });
        rateLimiter.Invoke(() =>
        {
            attempts++;
            throw new InvalidOperationException("boom");
        });

        var elapsedMethod = typeof(RateLimiter).GetMethod(
            "Timer_Elapsed",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RateLimiter.Timer_Elapsed method was not found.");

        Assert.Throws<TargetInvocationException>(() => elapsedMethod.Invoke(rateLimiter, [null, EventArgs.Empty]));

        elapsedMethod.Invoke(rateLimiter, [null, EventArgs.Empty]);

        Assert.Equal(1, attempts);
    }
}
