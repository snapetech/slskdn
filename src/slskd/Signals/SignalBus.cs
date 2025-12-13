namespace slskd.Signals;

using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Central signal routing and deduplication service.
/// </summary>
public class SignalBus : ISignalBus, IDisposable
{
    private readonly ILogger<SignalBus> logger;
    private readonly SignalSystemOptions options;
    private readonly ConcurrentDictionary<SignalChannel, ISignalChannelHandler> channelHandlers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> seenSignalIds = new(); // LRU cache for deduplication
    private readonly SemaphoreSlim seenSignalIdsLock = new(1, 1);
    private readonly List<Func<Signal, CancellationToken, Task>> subscribers = new();
    private readonly SemaphoreSlim subscribersLock = new(1, 1);
    private bool disposed;

    // Statistics
    private long signalsSent;
    private long signalsReceived;
    private long duplicateSignalsDropped;
    private long expiredSignalsDropped;

    public SignalBus(
        ILogger<SignalBus> logger,
        IOptionsMonitor<SignalSystemOptions> optionsMonitor)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        options = optionsMonitor?.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));

        // Start cleanup task for expired signal IDs
        _ = Task.Run(CleanupExpiredSignalIdsAsync);
    }

    /// <inheritdoc />
    public void RegisterChannelHandler(SignalChannel channel, ISignalChannelHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        channelHandlers[channel] = handler;
        logger.LogInformation("Registered signal channel handler: {Channel}", channel);

        // Start receiving from this channel
        _ = handler.StartReceivingAsync(OnSignalReceivedAsync, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task SendAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        if (signal == null)
            throw new ArgumentNullException(nameof(signal));

        if (signal.IsExpired(DateTimeOffset.UtcNow))
        {
            logger.LogWarning("Attempted to send expired signal: {SignalId}", signal.SignalId);
            return;
        }

        logger.LogDebug("Sending signal: {Type} from {FromPeerId} to {ToPeerId} via {Channels}",
            signal.Type, signal.FromPeerId, signal.ToPeerId, string.Join(", ", signal.PreferredChannels));

        // Try each preferred channel in order
        foreach (var channel in signal.PreferredChannels)
        {
            if (!channelHandlers.TryGetValue(channel, out var handler))
            {
                logger.LogDebug("Channel handler not registered: {Channel}", channel);
                continue;
            }

            if (!handler.CanSendTo(signal.ToPeerId))
            {
                logger.LogDebug("Channel {Channel} cannot send to peer {PeerId}", channel, signal.ToPeerId);
                continue;
            }

            try
            {
                await handler.SendAsync(signal, cancellationToken);
                Interlocked.Increment(ref signalsSent);
                logger.LogDebug("Signal {SignalId} sent successfully via {Channel}", signal.SignalId, channel);
                return; // Success, don't try other channels
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send signal {SignalId} via {Channel}, trying next channel",
                    signal.SignalId, channel);
                // Continue to next channel
            }
        }

        logger.LogWarning("Failed to send signal {SignalId} via any preferred channel", signal.SignalId);
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(Func<Signal, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        await subscribersLock.WaitAsync(cancellationToken);
        try
        {
            subscribers.Add(handler);
            logger.LogDebug("Subscribed to signal bus (total subscribers: {Count})", subscribers.Count);
        }
        finally
        {
            subscribersLock.Release();
        }
    }

    /// <summary>
    /// Called when a signal is received from any channel.
    /// This method is public for testing purposes but should only be called by channel handlers.
    /// </summary>
    public async Task OnSignalReceivedAsync(Signal signal, CancellationToken cancellationToken)
    {
        if (signal == null)
            return;

        // Deduplication: Check if we've seen this SignalId before
        await seenSignalIdsLock.WaitAsync(cancellationToken);
        try
        {
            if (seenSignalIds.ContainsKey(signal.SignalId))
            {
                Interlocked.Increment(ref duplicateSignalsDropped);
                logger.LogDebug("Dropping duplicate signal: {SignalId}", signal.SignalId);
                return;
            }

            // Check expiration
            if (signal.IsExpired(DateTimeOffset.UtcNow))
            {
                Interlocked.Increment(ref expiredSignalsDropped);
                logger.LogDebug("Dropping expired signal: {SignalId}", signal.SignalId);
                return;
            }

            // Add to seen cache
            seenSignalIds[signal.SignalId] = signal.SentAt;
            Interlocked.Increment(ref signalsReceived);
        }
        finally
        {
            seenSignalIdsLock.Release();
        }

        // Forward to all subscribers
        await subscribersLock.WaitAsync(cancellationToken);
        List<Func<Signal, CancellationToken, Task>> currentSubscribers;
        try
        {
            currentSubscribers = new List<Func<Signal, CancellationToken, Task>>(subscribers);
        }
        finally
        {
            subscribersLock.Release();
        }

        logger.LogDebug("Forwarding signal {SignalId} ({Type}) to {Count} subscribers",
            signal.SignalId, signal.Type, currentSubscribers.Count);

        var tasks = currentSubscribers.Select(sub => sub(signal, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Cleanup expired signal IDs from the deduplication cache.
    /// </summary>
    private async Task CleanupExpiredSignalIdsAsync()
    {
        while (!disposed)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None);

                await seenSignalIdsLock.WaitAsync();
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    var expired = seenSignalIds
                        .Where(kvp => now > kvp.Value + TimeSpan.FromHours(1)) // Keep IDs for 1 hour
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var signalId in expired)
                    {
                        seenSignalIds.TryRemove(signalId, out _);
                    }

                    // Enforce cache size limit
                    var removed = 0;
                    if (seenSignalIds.Count > options.DeduplicationCacheSize)
                    {
                        var toRemove = seenSignalIds
                            .OrderBy(kvp => kvp.Value)
                            .Take(seenSignalIds.Count - options.DeduplicationCacheSize)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var signalId in toRemove)
                        {
                            if (seenSignalIds.TryRemove(signalId, out _))
                                removed++;
                        }
                    }

                    if (expired.Count > 0 || removed > 0)
                    {
                        logger.LogDebug("Cleaned up {Expired} expired and {Removed} excess signal IDs from cache",
                            expired.Count, removed);
                    }
                }
                finally
                {
                    seenSignalIdsLock.Release();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in signal ID cleanup task");
            }
        }
    }

    /// <inheritdoc />
    public SignalBusStatistics GetStatistics()
    {
        return new SignalBusStatistics
        {
            SignalsSent = Interlocked.Read(ref signalsSent),
            SignalsReceived = Interlocked.Read(ref signalsReceived),
            DuplicateSignalsDropped = Interlocked.Read(ref duplicateSignalsDropped),
            ExpiredSignalsDropped = Interlocked.Read(ref expiredSignalsDropped)
        };
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        seenSignalIdsLock?.Dispose();
        subscribersLock?.Dispose();
    }
}















