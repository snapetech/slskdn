// <copyright file="MeshNeighborRegistry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
    private readonly ConcurrentDictionary<string, MeshNeighborConnectionSet> _connectionsByUsername = new(StringComparer.OrdinalIgnoreCase);
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
    public int Count => _connectionsByUsername.Count(kvp => kvp.Value.HasAny);

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

        var registered = false;
        var firstNeighborConnected = false;
        MeshNeighborEventArgs? eventArgs = null;

        await _registrationLock.WaitAsync();
        try
        {
            var hasExistingSet = _connectionsByUsername.TryGetValue(connection.Username, out var existingSetForCapacity);
            if (IsFull && !hasExistingSet)
            {
                _logger.LogDebug("Registry full, rejecting {Username}", OverlayLogSanitizer.Username(connection.Username));
                return false;
            }

            var set = existingSetForCapacity ?? _connectionsByUsername.GetOrAdd(connection.Username, _ => new MeshNeighborConnectionSet());
            var existingForDirection = connection.IsOutbound ? set.Outbound : set.Inbound;
            if (existingForDirection is not null)
            {
                _logger.LogDebug("Already connected to {Username} with {Direction} overlay connection", OverlayLogSanitizer.Username(connection.Username), connection.IsOutbound ? "outbound" : "inbound");
                return false;
            }

            if (_connectionsByEndpoint.ContainsKey(connection.RemoteEndPoint))
            {
                _logger.LogDebug("Already connected to {Endpoint}", OverlayLogSanitizer.Endpoint(connection.RemoteEndPoint));
                return false;
            }

            if (connection.IsOutbound)
            {
                set.Outbound = connection;
            }
            else
            {
                set.Inbound = connection;
            }

            _connectionsByEndpoint[connection.RemoteEndPoint] = connection;

            var isFirstNeighbor = Count == 1 && !_firstNeighborEventFired;

            _logger.LogInformation(
                "Registered {Direction} mesh neighbor {Username} from {Endpoint} (total peers: {Count}){First}",
                connection.IsOutbound ? "outbound" : "inbound",
                OverlayLogSanitizer.Username(connection.Username),
                OverlayLogSanitizer.Endpoint(connection.RemoteEndPoint),
                Count,
                isFirstNeighbor ? " 🎉 First neighbor connected!" : string.Empty);

            registered = true;
            firstNeighborConnected = isFirstNeighbor;
            eventArgs = new MeshNeighborEventArgs(connection);

            // Fire first neighbor event only once per session
            if (isFirstNeighbor)
            {
                _firstNeighborEventFired = true;
            }
        }
        finally
        {
            _registrationLock.Release();
        }

        if (registered && eventArgs is not null)
        {
            RaiseNeighborEvent(NeighborAdded, eventArgs, nameof(NeighborAdded));

            if (firstNeighborConnected)
            {
                RaiseNeighborEvent(FirstNeighborConnected, eventArgs, nameof(FirstNeighborConnected));
            }
        }

        return registered;
    }

    /// <summary>
    /// Unregister a mesh neighbor.
    /// </summary>
    public async Task UnregisterAsync(MeshOverlayConnection connection)
    {
        var removed = false;
        MeshNeighborEventArgs? eventArgs = null;

        await _registrationLock.WaitAsync();
        try
        {
            if (connection.Username is not null &&
                _connectionsByUsername.TryGetValue(connection.Username, out var set))
            {
                if (ReferenceEquals(set.Inbound, connection))
                {
                    set.Inbound = null;
                    removed = true;
                }

                if (ReferenceEquals(set.Outbound, connection))
                {
                    set.Outbound = null;
                    removed = true;
                }

                if (!set.HasAny)
                {
                    _connectionsByUsername.TryRemove(connection.Username, out _);
                    eventArgs = new MeshNeighborEventArgs(connection);
                }
            }

            if (_connectionsByEndpoint.TryGetValue(connection.RemoteEndPoint, out var registeredByEndpoint) &&
                ReferenceEquals(registeredByEndpoint, connection))
            {
                removed |= _connectionsByEndpoint.TryRemove(connection.RemoteEndPoint, out _);
            }

            if (removed)
            {
                _logger.LogInformation(
                    "Unregistered mesh neighbor {Username} (remaining: {Count})",
                    connection.Username is not null ? OverlayLogSanitizer.Username(connection.Username) : OverlayLogSanitizer.Endpoint(connection.RemoteEndPoint),
                    Count);

            }
        }
        finally
        {
            _registrationLock.Release();
        }

        if (removed && eventArgs is not null)
        {
            RaiseNeighborEvent(NeighborRemoved, eventArgs, nameof(NeighborRemoved));
        }
    }

    /// <summary>
    /// Unregister by username.
    /// </summary>
    public async Task<bool> UnregisterByUsernameAsync(string username)
    {
        if (_connectionsByUsername.TryGetValue(username, out var set))
        {
            foreach (var connection in set.GetAll().ToList())
            {
                await UnregisterAsync(connection);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if we're connected to a username.
    /// </summary>
    public bool IsConnectedTo(string username) => _connectionsByUsername.TryGetValue(username, out var set) && set.HasAny;

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
        _connectionsByUsername.TryGetValue(username, out var set);
        return set?.Outbound ?? set?.Inbound;
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
        return _connectionsByUsername.Values.SelectMany(set => set.GetAll()).ToList();
    }

    /// <summary>
    /// Get information about all mesh peers.
    /// </summary>
    public IReadOnlyList<MeshPeerInfo> GetPeerInfo()
    {
        return GetAllConnections()
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
                PeerVersion = null,
                IsOutbound = c.IsOutbound,
            })
            .ToList();
    }

    /// <summary>
    /// Remove stale (disconnected or idle) connections.
    /// </summary>
    /// <returns>Number of connections removed.</returns>
    public async Task<int> CleanupStaleConnectionsAsync()
    {
        var stale = GetAllConnections()
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
        foreach (var connection in GetAllConnections())
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

    private void RaiseNeighborEvent(
        EventHandler<MeshNeighborEventArgs>? handlers,
        MeshNeighborEventArgs args,
        string eventName)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<MeshNeighborEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mesh neighbor subscriber failed for {EventName}", eventName);
            }
        }
    }
}

internal sealed class MeshNeighborConnectionSet
{
    public MeshOverlayConnection? Inbound { get; set; }
    public MeshOverlayConnection? Outbound { get; set; }
    public bool HasAny => Inbound is not null || Outbound is not null;

    public IEnumerable<MeshOverlayConnection> GetAll()
    {
        if (Inbound is not null)
        {
            yield return Inbound;
        }

        if (Outbound is not null)
        {
            yield return Outbound;
        }
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
