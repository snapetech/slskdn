// <copyright file="MeshNeighborRegistry.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registry of active mesh overlay connections.
/// Tracks connected peers and provides lookup by username or endpoint.
/// </summary>
public sealed class MeshNeighborRegistry : IAsyncDisposable
{
    private readonly ILogger<MeshNeighborRegistry> _logger;
    private readonly ConcurrentDictionary<string, MeshOverlayConnection> _connectionsByUsername = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<IPEndPoint, MeshOverlayConnection> _connectionsByEndpoint = new();
    private readonly SemaphoreSlim _registrationLock = new(1, 1);
    
    /// <summary>
    /// Maximum number of mesh neighbors.
    /// </summary>
    public const int MaxNeighbors = 10;
    
    /// <summary>
    /// Minimum number of neighbors before triggering discovery.
    /// </summary>
    public const int MinNeighbors = 3;
    
    /// <summary>
    /// Raised when a new neighbor is added to the mesh.
    /// </summary>
    public event EventHandler<MeshNeighborEventArgs>? NeighborAdded;
    
    /// <summary>
    /// Raised when the very first neighbor is added (mesh bootstrap complete).
    /// </summary>
    public event EventHandler<MeshNeighborEventArgs>? FirstNeighborConnected;
    
    private bool _firstNeighborEventFired = false;
    
    /// <summary>
    /// Event raised when a neighbor is removed.
    /// </summary>
    public event EventHandler<MeshNeighborEventArgs>? NeighborRemoved;
    
    public MeshNeighborRegistry(ILogger<MeshNeighborRegistry> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Number of active neighbors.
    /// </summary>
    public int Count => _connectionsByUsername.Count;
    
    /// <summary>
    /// Whether we need more neighbors.
    /// </summary>
    public bool NeedsMoreNeighbors => Count < MinNeighbors;
    
    /// <summary>
    /// Whether we're at max capacity.
    /// </summary>
    public bool IsFull => Count >= MaxNeighbors;
    
    /// <summary>
    /// Register a new mesh neighbor.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <returns>True if registered, false if rejected (duplicate, full, etc).</returns>
    public async Task<bool> RegisterAsync(MeshOverlayConnection connection)
    {
        if (connection.Username is null)
        {
            _logger.LogWarning("Cannot register connection without username");
            return false;
        }
        
        await _registrationLock.WaitAsync();
        try
        {
            // Check capacity
            if (IsFull)
            {
                _logger.LogDebug("Registry full, rejecting {Username}", connection.Username);
                return false;
            }
            
            // Check for duplicate username
            if (_connectionsByUsername.ContainsKey(connection.Username))
            {
                _logger.LogDebug("Already connected to {Username}", connection.Username);
                return false;
            }
            
            // Check for duplicate endpoint
            if (_connectionsByEndpoint.ContainsKey(connection.RemoteEndPoint))
            {
                _logger.LogDebug("Already connected to {Endpoint}", connection.RemoteEndPoint);
                return false;
            }
            
            // Register
            _connectionsByUsername[connection.Username] = connection;
            _connectionsByEndpoint[connection.RemoteEndPoint] = connection;
            
            var isFirstNeighbor = Count == 1 && !_firstNeighborEventFired;
            
            _logger.LogInformation(
                "Registered mesh neighbor {Username} from {Endpoint} (total: {Count}){First}",
                connection.Username,
                connection.RemoteEndPoint,
                Count,
                isFirstNeighbor ? " 🎉 First neighbor connected!" : "");
            
            NeighborAdded?.Invoke(this, new MeshNeighborEventArgs(connection));
            
            // Fire first neighbor event only once per session
            if (isFirstNeighbor)
            {
                _firstNeighborEventFired = true;
                FirstNeighborConnected?.Invoke(this, new MeshNeighborEventArgs(connection));
            }
            
            return true;
        }
        finally
        {
            _registrationLock.Release();
        }
    }
    
    /// <summary>
    /// Unregister a mesh neighbor.
    /// </summary>
    public async Task UnregisterAsync(MeshOverlayConnection connection)
    {
        await _registrationLock.WaitAsync();
        try
        {
            var removed = false;
            
            if (connection.Username is not null)
            {
                removed |= _connectionsByUsername.TryRemove(connection.Username, out _);
            }
            
            removed |= _connectionsByEndpoint.TryRemove(connection.RemoteEndPoint, out _);
            
            if (removed)
            {
                _logger.LogInformation(
                    "Unregistered mesh neighbor {Username} (remaining: {Count})",
                    connection.Username ?? connection.RemoteEndPoint.ToString(),
                    Count);
                
                NeighborRemoved?.Invoke(this, new MeshNeighborEventArgs(connection));
            }
        }
        finally
        {
            _registrationLock.Release();
        }
    }
    
    /// <summary>
    /// Unregister by username.
    /// </summary>
    public async Task<bool> UnregisterByUsernameAsync(string username)
    {
        if (_connectionsByUsername.TryGetValue(username, out var connection))
        {
            await UnregisterAsync(connection);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if we're connected to a username.
    /// </summary>
    public bool IsConnectedTo(string username)
    {
        return _connectionsByUsername.ContainsKey(username);
    }
    
    /// <summary>
    /// Check if we're connected to an endpoint.
    /// </summary>
    public bool IsConnectedTo(IPEndPoint endpoint)
    {
        return _connectionsByEndpoint.ContainsKey(endpoint);
    }
    
    /// <summary>
    /// Get connection by username.
    /// </summary>
    public MeshOverlayConnection? GetConnection(string username)
    {
        _connectionsByUsername.TryGetValue(username, out var connection);
        return connection;
    }
    
    /// <summary>
    /// Get connection by endpoint.
    /// </summary>
    public MeshOverlayConnection? GetConnection(IPEndPoint endpoint)
    {
        _connectionsByEndpoint.TryGetValue(endpoint, out var connection);
        return connection;
    }
    
    /// <summary>
    /// Get all active connections.
    /// </summary>
    public IReadOnlyList<MeshOverlayConnection> GetAllConnections()
    {
        return _connectionsByUsername.Values.ToList();
    }
    
    /// <summary>
    /// Get information about all mesh peers.
    /// </summary>
    public IReadOnlyList<MeshPeerInfo> GetPeerInfo()
    {
        return _connectionsByUsername.Values
            .Where(c => c.Username is not null)
            .Select(c => new MeshPeerInfo
            {
                MeshPeerId = c.ConnectionId, // Use connection ID as mesh peer ID
                Username = c.Username,
                Endpoint = c.RemoteEndPoint,
                Features = c.Features,
                ConnectedAt = c.ConnectedAt,
                LastActivity = c.LastActivity,
                CertificateThumbprint = c.CertificateThumbprint,
                PeerVersion = null, // TODO: Add PeerVersion to MeshOverlayConnection
            })
            .ToList();
    }
    
    /// <summary>
    /// Remove stale (disconnected or idle) connections.
    /// </summary>
    /// <returns>Number of connections removed.</returns>
    public async Task<int> CleanupStaleConnectionsAsync()
    {
        var stale = _connectionsByUsername.Values
            .Where(c => !c.IsConnected || c.IsIdle())
            .ToList();
        
        foreach (var connection in stale)
        {
            _logger.LogDebug(
                "Cleaning up stale connection to {Username} (connected: {Connected}, idle: {Idle})",
                connection.Username,
                connection.IsConnected,
                connection.IsIdle());
            
            await UnregisterAsync(connection);
            await connection.DisposeAsync();
        }
        
        return stale.Count;
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connectionsByUsername.Values)
        {
            try
            {
                await connection.DisconnectAsync("Shutting down");
            }
            catch
            {
                // Best effort
            }
        }
        
        _connectionsByUsername.Clear();
        _connectionsByEndpoint.Clear();
        _registrationLock.Dispose();
    }
}

/// <summary>
/// Event args for neighbor events.
/// </summary>
public sealed class MeshNeighborEventArgs : EventArgs
{
    public MeshOverlayConnection Connection { get; }
    public string? Username => Connection.Username;
    public IPEndPoint Endpoint => Connection.RemoteEndPoint;
    
    public MeshNeighborEventArgs(MeshOverlayConnection connection)
    {
        Connection = connection;
    }
}

