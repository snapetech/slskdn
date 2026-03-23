namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using Serilog.Events;
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
    public void RateLimiter_WhenStagedActionThrows_IsolatesFailureAndAllowsLaterTicks()
    {
        using var rateLimiter = new RateLimiter(interval: 1000);
        var attempts = 0;
        var successfulTicks = 0;

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

        elapsedMethod.Invoke(rateLimiter, [null, EventArgs.Empty]);

        rateLimiter.Invoke(() => successfulTicks++);
        elapsedMethod.Invoke(rateLimiter, [null, EventArgs.Empty]);

        Assert.Equal(1, attempts);
        Assert.Equal(1, successfulTicks);
    }

    [Fact]
    public void RateLimiter_Dispose_DisposesConcurrencySemaphore()
    {
        var rateLimiter = new RateLimiter(interval: 1000, concurrencyLimit: 1);

        var semaphoreProperty = typeof(RateLimiter).GetProperty(
            "ConcurrentExecutionPreventionSemaphore",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RateLimiter semaphore property was not found.");

        var semaphore = (System.Threading.SemaphoreSlim)semaphoreProperty.GetValue(rateLimiter)!;

        rateLimiter.Dispose();

        Assert.Throws<ObjectDisposedException>(() => semaphore.Wait(0));
    }

    [Fact]
    public void RateLimiter_Dispose_WhenFlushActionThrows_StillDisposesOwnedResources()
    {
        var rateLimiter = new RateLimiter(interval: 1000, concurrencyLimit: 1, flushOnDispose: true);

        var semaphoreProperty = typeof(RateLimiter).GetProperty(
            "ConcurrentExecutionPreventionSemaphore",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RateLimiter semaphore property was not found.");
        var semaphore = (System.Threading.SemaphoreSlim)semaphoreProperty.GetValue(rateLimiter)!;

        rateLimiter.Invoke(() => { });
        rateLimiter.Invoke(() => throw new InvalidOperationException("boom"));

        var exception = Assert.Throws<InvalidOperationException>(() => rateLimiter.Dispose());

        Assert.Equal("boom", exception.Message);
        Assert.Throws<ObjectDisposedException>(() => semaphore.Wait(0));
    }

    [Fact]
    public void TimedCounter_WhenElapsedCallbackThrows_StillResetsAndAllowsLaterTicks()
    {
        var attempts = 0;
        using var counter = new TimedCounter(TimeSpan.FromMinutes(1), _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException("boom");
            }
        });

        var elapsedMethod = typeof(TimedCounter).GetMethod(
            "Elapsed",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TimedCounter.Elapsed method was not found.");

        counter.CountUp(2);
        elapsedMethod.Invoke(counter, [null, null!]);

        Assert.Equal(0, counter.Count);

        counter.CountUp(3);
        elapsedMethod.Invoke(counter, [null, null!]);

        Assert.Equal(2, attempts);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public void ExponentialMovingAverage_WhenOnUpdateThrows_StillUpdatesValueAndAllowsLaterUpdates()
    {
        var attempts = 0;
        var average = new ExponentialMovingAverage(0.5, _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException("boom");
            }
        });

        average.Update(10);

        Assert.True(average.Initialized);
        Assert.Equal(10, average.Value);

        average.Update(20);

        Assert.Equal(2, attempts);
        Assert.Equal(15, average.Value);
    }

    [Fact]
    public void DelegatingSink_WhenObserverThrows_SwallowsFailureAndAllowsLaterEmit()
    {
        var attempts = 0;
        var sink = new DelegatingSink(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException("boom");
            }
        });

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            MessageTemplate.Empty,
            []);

        sink.Emit(logEvent);
        sink.Emit(logEvent);

        Assert.Equal(2, attempts);
    }
}
