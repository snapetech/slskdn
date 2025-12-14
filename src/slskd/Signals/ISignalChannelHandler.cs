// <copyright file="ISignalChannelHandler.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Signals;

/// <summary>
/// Handler for delivering signals over a specific channel.
/// </summary>
public interface ISignalChannelHandler
{
    /// <summary>
    /// Check if this channel can send to the specified peer.
    /// </summary>
    /// <param name="peerId">Target peer ID.</param>
    /// <returns>True if the channel can deliver to this peer.</returns>
    bool CanSendTo(string peerId);

    /// <summary>
    /// Send a signal over this channel.
    /// </summary>
    /// <param name="signal">The signal to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task SendAsync(Signal signal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start receiving signals from this channel and forward them to the signal bus.
    /// </summary>
    /// <param name="onSignalReceived">Callback when a signal is received.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task StartReceivingAsync(Func<Signal, CancellationToken, Task> onSignalReceived, CancellationToken cancellationToken = default);
}
