namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using Xunit;

public class ManagedStateTests
{
    [Fact]
    public void SetValue_WhenOneListenerThrows_StillInvokesRemainingListeners()
    {
        var state = new ManagedState<State>();
        var invokedHealthyListener = false;

        using var _ = state.OnChange(_ => throw new InvalidOperationException("boom"));
        using var __ = state.OnChange(change => invokedHealthyListener = change.Current.Value == 1);

        var exception = Assert.Throws<AggregateException>(() => state.SetValue(current =>
        {
            current.Value = 1;
            return current;
        }));

        Assert.True(invokedHealthyListener);
        Assert.Single(exception.InnerExceptions);
        Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
        Assert.Equal(1, state.CurrentValue.Value);
    }

    [Fact]
    public void SetValue_WhenSubscriptionIsDisposedAfterSnapshot_DoesNotInvokeDisposedListener()
    {
        var state = new ManagedState<State>();
        var invokedDisposedListener = false;
        var registration = state.OnChange(_ => invokedDisposedListener = true);
        var changedDelegate = GetChangedDelegate(state);

        registration.Dispose();

        foreach (Action<(State? Previous, State Current)> handler in changedDelegate.GetInvocationList())
        {
            handler.Invoke((new State(), new State { Value = 1 }));
        }

        Assert.False(invokedDisposedListener);
    }

    private static Action<(State? Previous, State Current)> GetChangedDelegate(ManagedState<State> state)
    {
        var field = typeof(ManagedState<State>).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Changed event field was not found.");

        return (Action<(State? Previous, State Current)>)field.GetValue(state)!;
    }

    public class State
    {
        public int Value { get; set; }
    }
}
