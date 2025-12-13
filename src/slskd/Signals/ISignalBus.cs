namespace slskd.Signals;

/// <summary>
/// Central signal routing and deduplication service.
/// </summary>
public interface ISignalBus
{
    /// <summary>
    /// Send a signal to the target peer via preferred channels.
    /// </summary>
    /// <param name="signal">The signal to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task SendAsync(Signal signal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to incoming signals.
    /// </summary>
    /// <param name="handler">Handler function to process incoming signals.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task SubscribeAsync(Func<Signal, CancellationToken, Task> handler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a channel handler for signal delivery.
    /// </summary>
    /// <param name="channel">The channel type.</param>
    /// <param name="handler">The channel handler.</param>
    void RegisterChannelHandler(SignalChannel channel, ISignalChannelHandler handler);

    /// <summary>
    /// Get current statistics.
    /// </summary>
    SignalBusStatistics GetStatistics();
}

/// <summary>
/// Statistics for the signal bus.
/// </summary>
public sealed class SignalBusStatistics
{
    public long SignalsSent { get; init; }
    public long SignalsReceived { get; init; }
    public long DuplicateSignalsDropped { get; init; }
    public long ExpiredSignalsDropped { get; init; }
}















