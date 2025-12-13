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
/// Tracks connected peers by mesh peer ID (primary) and optionally by username/endpoint.
/// </summary>
public sealed class MeshNeighborRegistry : IAsyncDisposable
{
    private readonly ILogger<MeshNeighborRegistry> _logger;
    private readonly ConcurrentDictionary<string, MeshOverlayConnection> _connectionsByMeshPeerId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MeshOverlayConnection> _connectionsByUsername = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<IPEndPoint, MeshOverlayConnection> _connectionsByEndpoint = new();
    private readonly ConcurrentDictionary<IPAddress, int> _connectionCountsByAddress = new();
    private readonly SemaphoreSlim _registrationLock = new(1, 1);
    
    /// <summary>
    /// Maximum number of mesh neighbors.
    /// </summary>
    public const int MaxNeighbors = 10;
    
    /// <summary>
    /// Maximum connections from a single IP address.
    /// </summary>
    public const int MaxConnectionsPerAddress = 3;
    
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
    public int Count => _connectionsByMeshPeerId.Count;
    
    /// <summary>
    /// Whether we need more neighbors.
    /// </summary>
    public bool NeedsMoreNeighbors => Count < MinNeighbors;
    
    /// <summary>
    /// Whether we're at max capacity.
    /// </summary>
    public bool IsFull => Count >= MaxNeighbors;
    
    /// <summary>
    /// Register a new mesh neighbor. Mesh peer ID is required, username is optional.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <returns>True if registered, false if rejected (duplicate, full, etc).</returns>
    public async Task<bool> RegisterAsync(MeshOverlayConnection connection)
    {
        if (string.IsNullOrEmpty(connection.MeshPeerId))
        {
            _logger.LogWarning("Cannot register connection without mesh peer ID");
            return false;
        }
        
        await _registrationLock.WaitAsync();
        try
        {
            // Check capacity
            if (IsFull)
            {
                _logger.LogDebug("Registry full, rejecting {MeshPeerId}", connection.MeshPeerId);
                return false;
            }
            
            // Check for duplicate mesh peer ID
            if (_connectionsByMeshPeerId.ContainsKey(connection.MeshPeerId))
            {
                _logger.LogDebug("Already connected to {MeshPeerId}", connection.MeshPeerId);
                return false;
            }
            
            // Check for duplicate endpoint
            if (_connectionsByEndpoint.ContainsKey(connection.RemoteEndPoint))
            {
                _logger.LogDebug("Already connected to {Endpoint}", connection.RemoteEndPoint);
                return false;
            }
            
            // SECURITY: Check per-address connection limit
            var address = connection.RemoteAddress;
            _connectionCountsByAddress.TryGetValue(address, out var currentCount);
            if (currentCount >= MaxConnectionsPerAddress)
            {
                _logger.LogWarning(
                    "Rejecting connection from {Address}: already have {Count} connections (max: {Max})",
                    address,
                    currentCount,
                    MaxConnectionsPerAddress);
                return false;
            }
            
            // Register by mesh peer ID (primary key)
            _connectionsByMeshPeerId[connection.MeshPeerId] = connection;
            _connectionsByEndpoint[connection.RemoteEndPoint] = connection;
            _connectionCountsByAddress[address] = currentCount + 1;
            
            // Also register by username if provided (optional alias)
            if (!string.IsNullOrEmpty(connection.Username))
            {
                _connectionsByUsername[connection.Username] = connection;
            }
            
            var isFirstNeighbor = Count == 1 && !_firstNeighborEventFired;
            
            var displayName = connection.Username ?? connection.MeshPeerId;
            _logger.LogInformation(
                "Registered mesh neighbor {DisplayName} (MeshPeerId: {MeshPeerId}) from {Endpoint} (total: {Count}){First}",
                displayName,
                connection.MeshPeerId,
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
            
            if (!string.IsNullOrEmpty(connection.MeshPeerId))
            {
                removed |= _connectionsByMeshPeerId.TryRemove(connection.MeshPeerId, out _);
            }
            
            if (!string.IsNullOrEmpty(connection.Username))
            {
                removed |= _connectionsByUsername.TryRemove(connection.Username, out _);
            }
            
            removed |= _connectionsByEndpoint.TryRemove(connection.RemoteEndPoint, out _);
            
            // Decrement per-address count
            var address = connection.RemoteAddress;
            _connectionCountsByAddress.AddOrUpdate(
                address,
                0, // If missing, set to 0 (shouldn't happen)
                (_, count) => Math.Max(0, count - 1)); // Decrement, but don't go negative
            
            // Clean up entry if count reaches 0
            if (_connectionCountsByAddress.TryGetValue(address, out var newCount) && newCount == 0)
            {
                _connectionCountsByAddress.TryRemove(address, out _);
            }
            
            if (removed)
            {
                var displayName = connection.Username ?? connection.MeshPeerId ?? connection.RemoteEndPoint.ToString();
                _logger.LogInformation(
                    "Unregistered mesh neighbor {DisplayName} (remaining: {Count})",
                    displayName,
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
    /// Get connection by mesh peer ID.
    /// </summary>
    public MeshOverlayConnection? GetConnectionByMeshPeerId(string meshPeerId)
    {
        _connectionsByMeshPeerId.TryGetValue(meshPeerId, out var connection);
        return connection;
    }
    
    /// <summary>
    /// Get connection by username (optional alias).
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
        return _connectionsByMeshPeerId.Values.ToList();
    }
    
    /// <summary>
    /// Get information about all mesh peers.
    /// </summary>
    public IReadOnlyList<MeshPeerInfo> GetPeerInfo()
    {
        return _connectionsByMeshPeerId.Values
            .Where(c => !string.IsNullOrEmpty(c.MeshPeerId))
            .Select(c => new MeshPeerInfo
            {
                MeshPeerId = c.MeshPeerId!,
                Username = c.Username, // May be null for mesh-only peers
                Endpoint = c.RemoteEndPoint,
                Features = c.Features,
                ConnectedAt = c.ConnectedAt,
                LastActivity = c.LastActivity,
                CertificateThumbprint = c.CertificateThumbprint,
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

