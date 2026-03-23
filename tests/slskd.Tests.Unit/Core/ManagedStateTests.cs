namespace slskd.Tests.Unit.Core;

using System;
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

    public class State
    {
        public int Value { get; set; }
    }
}
