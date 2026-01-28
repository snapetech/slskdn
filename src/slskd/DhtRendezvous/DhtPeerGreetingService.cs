// <copyright file="DhtPeerGreetingService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
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
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

        _neighborRegistry.NeighborAdded += OnNeighborAdded;
        _neighborRegistry.FirstNeighborConnected += OnFirstNeighborConnected;

        _logger.LogInformation("DHT Peer Greeting Service started (max greetings: {Max})", MaxAutoGreetings);
    }
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _neighborRegistry.NeighborAdded -= OnNeighborAdded;
        _neighborRegistry.FirstNeighborConnected -= OnFirstNeighborConnected;
        
        return base.StopAsync(cancellationToken);
    }
    
    private void OnFirstNeighborConnected(object? sender, MeshNeighborEventArgs e)
    {
        if (!Enabled || e.Username is null)
        {
            return;
        }
        
        // Special message for the very first mesh peer ever!
        _ = SendGreetingAsync(e.Username, isFirstEver: true);
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
            _logger.LogDebug("Skipping auto-greeting for {Username} (limit reached)", e.Username);
            return;
        }
        
        // Don't greet the same peer twice
        if (_greetedPeers.ContainsKey(e.Username))
        {
            _logger.LogDebug("Already greeted {Username}, skipping", e.Username);
            return;
        }
        
        _ = SendGreetingAsync(e.Username, isFirstEver: false);
    }
    
    private async Task SendGreetingAsync(string username, bool isFirstEver)
    {
        if (!_soulseekClient.State.HasFlag(SoulseekClientStates.Connected))
        {
            _logger.LogDebug("Not connected to Soulseek, skipping greeting to {Username}", username);
            return;
        }
        
        try
        {
            // Pick a random message
            var random = new Random();
            var messages = isFirstEver ? FirstConnectionMessages : RegularGreetings;
            var message = messages[random.Next(messages.Length)];
            
            _logger.LogInformation("Sending mesh greeting to {Username}: {Message}", username, message);
            
            await _soulseekClient.SendPrivateMessageAsync(username, message);
            
            _greetedPeers[username] = DateTimeOffset.UtcNow;
            Interlocked.Increment(ref _greetingCount);
            
            _logger.LogDebug("Greeting sent successfully to {Username} (total: {Count})", 
                username, _greetingCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send greeting to {Username}", username);
        }
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

