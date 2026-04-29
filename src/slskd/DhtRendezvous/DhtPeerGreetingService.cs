// <copyright file="DhtPeerGreetingService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soulseek;

/// <summary>
/// Automatically sends greeting messages to newly connected DHT mesh peers.
/// Celebrates the first few connections to the mesh network!
/// </summary>
public sealed class DhtPeerGreetingService : BackgroundService
{
    private readonly ILogger<DhtPeerGreetingService> _logger;
    private readonly MeshNeighborRegistry _neighborRegistry;
    private readonly ISoulseekClient _soulseekClient;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _greetedPeers = new(StringComparer.OrdinalIgnoreCase);
    private int _subscriptionsAttached;

    private static readonly string[] FirstConnectionMessages = new[]
    {
        "🎉 Hello! We just connected via the slskdn mesh network! This is my first mesh peer - exciting!",
        "👋 Hey there, fellow slskdn user! You're my first mesh peer! The future of P2P is here!",
        "🌐 Wow, mesh connection established! You're my first - let's celebrate decentralization!",
    };

    private static readonly string[] RegularGreetings = new[]
    {
        "🔗 slskdn mesh connection established! Happy sharing!",
        "👋 Connected via slskdn mesh. Welcome to the network!",
        "🌐 Mesh peer connection successful! Greetings from slskdn!",
    };

    /// <summary>
    /// Maximum number of peers to auto-greet (to avoid spam).
    /// </summary>
    public int MaxAutoGreetings { get; set; } = 10;

    /// <summary>
    /// Whether auto-greetings are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    private int _greetingCount = 0;

    public DhtPeerGreetingService(
        ILogger<DhtPeerGreetingService> logger,
        MeshNeighborRegistry neighborRegistry,
        ISoulseekClient soulseekClient)
    {
        _logger = logger;
        _neighborRegistry = neighborRegistry;
        _soulseekClient = soulseekClient;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        AttachNeighborSubscriptions();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

        _logger.LogInformation("DHT Peer Greeting Service started (max greetings: {Max})", MaxAutoGreetings);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        DetachNeighborSubscriptions();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        DetachNeighborSubscriptions();
        base.Dispose();
    }

    private void OnFirstNeighborConnected(object? sender, MeshNeighborEventArgs e)
    {
        if (!Enabled || e.Username is null)
        {
            return;
        }

        // Special message for the very first mesh peer ever!
        QueueGreeting(e.Username, isFirstEver: true);
    }

    private void OnNeighborAdded(object? sender, MeshNeighborEventArgs e)
    {
        if (!Enabled || e.Username is null)
        {
            return;
        }

        // Skip if this is the first peer (handled by FirstNeighborConnected)
        if (_neighborRegistry.Count == 1)
        {
            return;
        }

        // Only auto-greet the first few peers
        if (_greetingCount >= MaxAutoGreetings)
        {
            _logger.LogDebug("Skipping auto-greeting for {Username} (limit reached)", OverlayLogSanitizer.Username(e.Username));
            return;
        }

        // Don't greet the same peer twice
        if (_greetedPeers.ContainsKey(e.Username))
        {
            _logger.LogDebug("Already greeted {Username}, skipping", OverlayLogSanitizer.Username(e.Username));
            return;
        }

        QueueGreeting(e.Username, isFirstEver: false);
    }

    private async Task SendGreetingAsync(string username, bool isFirstEver)
    {
        if (!_soulseekClient.State.HasFlag(SoulseekClientStates.Connected))
        {
            _logger.LogDebug("Not connected to Soulseek, skipping greeting to {Username}", OverlayLogSanitizer.Username(username));
            _greetedPeers.TryRemove(username, out _);
            return;
        }

        try
        {
            // Pick a random message
            var messages = isFirstEver ? FirstConnectionMessages : RegularGreetings;
            var message = messages[Random.Shared.Next(messages.Length)];

            _logger.LogInformation("Sending mesh greeting to {Username}: {Message}", OverlayLogSanitizer.Username(username), message);

            await _soulseekClient.SendPrivateMessageAsync(username, message);

            _greetedPeers[username] = DateTimeOffset.UtcNow;
            Interlocked.Increment(ref _greetingCount);

            _logger.LogDebug("Greeting sent successfully to {Username} (total: {Count})",
                OverlayLogSanitizer.Username(username), _greetingCount);
        }
        catch (Exception ex)
        {
            _greetedPeers.TryRemove(username, out _);
            _logger.LogWarning(ex, "Failed to send greeting to {Username}", OverlayLogSanitizer.Username(username));
        }
    }

    private void QueueGreeting(string username, bool isFirstEver)
    {
        if (!_greetedPeers.TryAdd(username, DateTimeOffset.MinValue))
        {
            _logger.LogDebug("Already greeted {Username}, skipping", OverlayLogSanitizer.Username(username));
            return;
        }

        _ = SendGreetingAsync(username, isFirstEver);
    }

    private void AttachNeighborSubscriptions()
    {
        if (Interlocked.Exchange(ref _subscriptionsAttached, 1) == 1)
        {
            return;
        }

        _neighborRegistry.NeighborAdded += OnNeighborAdded;
        _neighborRegistry.FirstNeighborConnected += OnFirstNeighborConnected;
    }

    private void DetachNeighborSubscriptions()
    {
        if (Interlocked.Exchange(ref _subscriptionsAttached, 0) == 0)
        {
            return;
        }

        _neighborRegistry.NeighborAdded -= OnNeighborAdded;
        _neighborRegistry.FirstNeighborConnected -= OnFirstNeighborConnected;
    }

    /// <summary>
    /// Manually send a greeting to a peer.
    /// </summary>
    public Task GreetPeerAsync(string username)
    {
        return SendGreetingAsync(username, isFirstEver: false);
    }

    /// <summary>
    /// Get greeting service statistics.
    /// </summary>
    public GreetingStats GetStats()
    {
        return new GreetingStats
        {
            TotalGreetingsSent = _greetingCount,
            MaxAutoGreetings = MaxAutoGreetings,
            GreetedPeers = _greetedPeers.Keys.ToArray(),
            Enabled = Enabled,
        };
    }
}

/// <summary>
/// Greeting service statistics.
/// </summary>
public sealed class GreetingStats
{
    public int TotalGreetingsSent { get; init; }
    public int MaxAutoGreetings { get; init; }
    public string[] GreetedPeers { get; init; } = Array.Empty<string>();
    public bool Enabled { get; init; }
}
