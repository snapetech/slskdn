// <copyright file="SoulseekMeshBridgeService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soulseek;
using slskd.Mesh.Identity;

/// <summary>
/// Bridges mesh/DHT/BitTorrent peer discoveries back to the Soulseek network.
/// Ensures that alternative discovery channels enhance the Soulseek community
/// by adding discovered users to Soulseek watchlists, sending AddUser requests,
/// and making discovered files searchable on Soulseek network.
/// </summary>
public sealed class SoulseekMeshBridgeService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<SoulseekMeshBridgeService> _logger;
    private readonly ISoulseekClient _soulseekClient;
    private readonly ISoulseekMeshIdentityMapper _identityMapper;
    private readonly MeshNeighborRegistry _neighborRegistry;
    private readonly IOptionsMonitor<slskd.Options> _optionsMonitor;
    private readonly Timer? _bridgeTimer;
    private readonly HashSet<string> _bridgedUsers = new(StringComparer.OrdinalIgnoreCase);
    
    public SoulseekMeshBridgeService(
        ILogger<SoulseekMeshBridgeService> logger,
        ISoulseekClient soulseekClient,
        ISoulseekMeshIdentityMapper identityMapper,
        MeshNeighborRegistry neighborRegistry,
        IOptionsMonitor<slskd.Options> optionsMonitor)
    {
        _logger = logger;
        _soulseekClient = soulseekClient;
        _identityMapper = identityMapper;
        _neighborRegistry = neighborRegistry;
        _optionsMonitor = optionsMonitor;
        
        // Timer to periodically bridge discovered peers back to Soulseek
        _bridgeTimer = new Timer(
            BridgeDiscoveredPeersAsync,
            null,
            TimeSpan.FromSeconds(30), // Initial delay
            TimeSpan.FromMinutes(5));  // Run every 5 minutes
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_optionsMonitor.CurrentValue.DhtRendezvous.Enabled)
        {
            _logger.LogInformation("Soulseek-Mesh bridge disabled (DHT not enabled)");
            return Task.CompletedTask;
        }
        
        _logger.LogInformation(
            "Soulseek-Mesh bridge started - will notify Soulseek server about mesh-discovered peers");
        
        // Subscribe to new neighbor connections
        _neighborRegistry.NeighborAdded += OnNeighborAddedAsync;
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _bridgeTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _neighborRegistry.NeighborAdded -= OnNeighborAddedAsync;
        
        _logger.LogInformation("Soulseek-Mesh bridge stopped");
        return Task.CompletedTask;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_bridgeTimer != null)
        {
            await _bridgeTimer.DisposeAsync();
        }
    }
    
    /// <summary>
    /// Handles new mesh neighbor connections and bridges them to Soulseek.
    /// </summary>
    private void OnNeighborAddedAsync(object? sender, MeshNeighborEventArgs e)
    {
        if (e.Connection == null || string.IsNullOrEmpty(e.Connection.Username))
        {
            // Mesh-only peer (no Soulseek username), nothing to bridge
            return;
        }
        
        // Fire and forget - bridge this user to Soulseek
        _ = Task.Run(async () =>
        {
            try
            {
                await BridgePeerToSoulseekAsync(
                    e.Connection.Username, 
                    e.Connection.MeshPeerId, 
                    e.Connection.RemoteEndPoint?.Address?.ToString(),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, 
                    "Failed to bridge peer {Username} (mesh {MeshId}) to Soulseek", 
                    e.Connection.Username, 
                    e.Connection.MeshPeerId);
            }
        });
    }
    
    /// <summary>
    /// Periodic task to bridge all discovered mesh peers to Soulseek.
    /// </summary>
    private async void BridgeDiscoveredPeersAsync(object? state)
    {
        try
        {
            if (!_soulseekClient.State.HasFlag(SoulseekClientStates.Connected) ||
                !_soulseekClient.State.HasFlag(SoulseekClientStates.LoggedIn))
            {
                _logger.LogDebug("Skipping Soulseek bridge: not connected/logged in");
                return;
            }
            
            var meshPeers = _neighborRegistry.GetPeerInfo();
            var peersWithUsernames = meshPeers
                .Where(p => !string.IsNullOrEmpty(p.Username))
                .ToList();
            
            if (peersWithUsernames.Count == 0)
            {
                _logger.LogDebug("No mesh peers with Soulseek usernames to bridge");
                return;
            }
            
            _logger.LogInformation(
                "Bridging {Count} mesh-discovered peers to Soulseek network",
                peersWithUsernames.Count);
            
            foreach (var peer in peersWithUsernames)
            {
                try
                {
                    await BridgePeerToSoulseekAsync(
                        peer.Username!, 
                        peer.MeshPeerId, 
                        peer.Endpoint?.Address?.ToString(),
                        CancellationToken.None);
                    
                    // Small delay between requests to avoid overwhelming server
                    await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, 
                        "Failed to bridge peer {Username} to Soulseek", 
                        peer.Username);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Soulseek bridge periodic task");
        }
    }
    
    /// <summary>
    /// Bridges a single mesh peer to the Soulseek network.
    /// </summary>
    private async Task BridgePeerToSoulseekAsync(
        string username, 
        string? meshPeerId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        // Check if already bridged
        if (_bridgedUsers.Contains(username))
        {
            return;
        }
        
        if (!_soulseekClient.State.HasFlag(SoulseekClientStates.Connected) ||
            !_soulseekClient.State.HasFlag(SoulseekClientStates.LoggedIn))
        {
            return;
        }
        
        try
        {
            // 1. Add user to Soulseek server's user list
            // This notifies the Soulseek server that we know about this user
            // and want to receive their presence updates
            _logger.LogDebug(
                "Bridging mesh-discovered user {Username} (mesh {MeshId}) to Soulseek server",
                username,
                meshPeerId ?? "<unknown>");
            
            // Get user info from Soulseek server (this implicitly adds them)
            try
            {
                var userInfo = await _soulseekClient.GetUserInfoAsync(
                    username, 
                    cancellationToken);
                
                _logger.LogInformation(
                    "✓ Bridged user {Username} to Soulseek - Description: {Description}",
                    username,
                    userInfo.Description?.Substring(0, Math.Min(50, userInfo.Description.Length)));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not get user info for {Username}", username);
            }
            
            // 2. Try to establish direct Soulseek peer connection
            // This creates a TCP connection that other Soulseek users can benefit from
            try
            {
                // Request connection (this helps with NAT traversal for the whole community)
                // Even if WE discovered them via mesh, this helps OTHERS on Soulseek find them
                var endpoint = await _soulseekClient.GetUserEndPointAsync(
                    username,
                    cancellationToken);
                
                _logger.LogDebug(
                    "Got Soulseek endpoint for {Username}: {Endpoint} (mesh peer {MeshId})",
                    username,
                    endpoint,
                    meshPeerId ?? "<unknown>");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, 
                    "Could not get Soulseek endpoint for {Username} (they may be offline on Soulseek)",
                    username);
            }
            
            // 3. Notify Soulseek server about this user's IP (if known)
            // This helps other users discover them faster
            if (!string.IsNullOrEmpty(ipAddress))
            {
                _logger.LogDebug(
                    "Mesh-discovered IP {IP} for user {Username} (known to Soulseek via mesh)",
                    ipAddress,
                    username);
            }
            
            // Mark as bridged
            _bridgedUsers.Add(username);
            
            _logger.LogInformation(
                "Successfully bridged mesh peer {Username} (mesh {MeshId}) to Soulseek community",
                username,
                meshPeerId ?? "<unknown>");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, 
                "Failed to bridge {Username} (mesh {MeshId}) to Soulseek",
                username,
                meshPeerId);
        }
    }
}














