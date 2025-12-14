// <copyright file="LocalPortForwarder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh;
using slskd.Mesh.ServiceFabric;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Common.Security;

/// <summary>
/// Manages local port forwarding through VPN tunnels to remote services.
/// </summary>
public class LocalPortForwarder : IDisposable
{
    private readonly ILogger<LocalPortForwarder> _logger;
    private readonly IMeshServiceClient _meshClient;

    // Active forwarders: localPort -> ForwarderInstance
    private readonly ConcurrentDictionary<int, ForwarderInstance> _activeForwarders = new();

    // Forwarder instances track individual tunnel connections
    private readonly ConcurrentDictionary<string, ForwarderConnection> _activeConnections = new();

    public LocalPortForwarder(
        ILogger<LocalPortForwarder> logger,
        IMeshServiceClient meshClient)
    {
        _logger = logger;
        _meshClient = meshClient;
    }

    /// <summary>
    /// Starts forwarding a local port to a remote service through a VPN tunnel.
    /// </summary>
    /// <param name="localPort">The local port to listen on.</param>
    /// <param name="podId">The pod ID for the VPN tunnel.</param>
    /// <param name="destinationHost">The remote destination hostname/IP.</param>
    /// <param name="destinationPort">The remote destination port.</param>
    /// <param name="serviceName">Optional service name for registered services.</param>
    /// <returns>A task representing the forwarding operation.</returns>
    public async Task StartForwardingAsync(
        int localPort,
        string podId,
        string destinationHost,
        int destinationPort,
        string? serviceName = null)
    {
        if (_activeForwarders.ContainsKey(localPort))
        {
            throw new InvalidOperationException($"Port {localPort} is already being forwarded");
        }

        var forwarder = new ForwarderInstance(
            localPort,
            podId,
            destinationHost,
            destinationPort,
            serviceName,
            this);

        _activeForwarders[localPort] = forwarder;

        try
        {
            await forwarder.StartAsync();
            _logger.LogInformation(
                "[PortForward] Started forwarding local port {LocalPort} to {Host}:{Port} via pod {PodId}",
                localPort, destinationHost, destinationPort, podId);
        }
        catch (Exception ex)
        {
            _activeForwarders.TryRemove(localPort, out _);
            _logger.LogError(ex,
                "[PortForward] Failed to start forwarding on port {LocalPort}", localPort);
            throw;
        }
    }

    /// <summary>
    /// Stops forwarding on a specific local port.
    /// </summary>
    /// <param name="localPort">The local port to stop forwarding.</param>
    public async Task StopForwardingAsync(int localPort)
    {
        if (_activeForwarders.TryRemove(localPort, out var forwarder))
        {
            await forwarder.StopAsync();
            _logger.LogInformation(
                "[PortForward] Stopped forwarding on local port {LocalPort}", localPort);
        }
    }

    /// <summary>
    /// Gets the status of all active port forwarders.
    /// </summary>
    public IEnumerable<PortForwardingStatus> GetForwardingStatus()
    {
        return _activeForwarders.Values.Select(f => f.GetStatus());
    }

    /// <summary>
    /// Gets the status of a specific port forwarder.
    /// </summary>
    public PortForwardingStatus? GetForwardingStatus(int localPort)
    {
        return _activeForwarders.TryGetValue(localPort, out var forwarder)
            ? forwarder.GetStatus()
            : null;
    }

    /// <summary>
    /// Called by ForwarderInstance when a new connection needs a tunnel.
    /// </summary>
    internal async Task<ForwarderConnection?> CreateTunnelConnectionAsync(
        string podId,
        string destinationHost,
        int destinationPort,
        string? serviceName)
    {
        try
        {
            // Call the private-gateway service to open a tunnel
            var openTunnelRequest = new OpenTunnelRequest
            {
                PodId = podId,
                DestinationHost = destinationHost,
                DestinationPort = destinationPort,
                ServiceName = serviceName,
                RequestNonce = Guid.NewGuid().ToString("N"),
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var response = await _meshClient.CallServiceAsync(
                "private-gateway",
                "OpenTunnel",
                openTunnelRequest,
                CancellationToken.None);

            if (response.StatusCode != ServiceStatusCodes.OK)
            {
                _logger.LogWarning(
                    "[PortForward] Failed to open tunnel: {Error}",
                    response.ErrorMessage);
                return null;
            }

            var tunnelResponse = System.Text.Json.JsonSerializer.Deserialize<OpenTunnelResponse>(
                response.Payload);

            if (tunnelResponse == null || !tunnelResponse.Accepted)
            {
                _logger.LogWarning(
                    "[PortForward] Tunnel request rejected: {Error}",
                    response.ErrorMessage ?? "Unknown error");
                return null;
            }

            var connection = new ForwarderConnection(
                tunnelResponse.TunnelId,
                podId,
                destinationHost,
                destinationPort,
                this);

            _activeConnections[tunnelResponse.TunnelId] = connection;

            _logger.LogInformation(
                "[PortForward] Created tunnel connection {TunnelId} for {Host}:{Port}",
                tunnelResponse.TunnelId, destinationHost, destinationPort);

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PortForward] Failed to create tunnel connection to {Host}:{Port}",
                destinationHost, destinationPort);
            return null;
        }
    }

    /// <summary>
    /// Called by ForwarderConnection when data needs to be sent through the tunnel.
    /// </summary>
    internal async Task SendTunnelDataAsync(string tunnelId, byte[] data)
    {
        try
        {
            var dataRequest = new TunnelDataRequest
            {
                TunnelId = tunnelId,
                Data = data
            };

            var response = await _meshClient.CallServiceAsync(
                "private-gateway",
                "TunnelData",
                dataRequest,
                CancellationToken.None);

            if (response.StatusCode != ServiceStatusCodes.OK)
            {
                _logger.LogWarning(
                    "[PortForward] Failed to send tunnel data for {TunnelId}: {Error}",
                    tunnelId, response.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PortForward] Error sending tunnel data for {TunnelId}", tunnelId);
        }
    }

    /// <summary>
    /// Called by ForwarderConnection when it needs to receive data from the tunnel.
    /// </summary>
    internal async Task<byte[]?> ReceiveTunnelDataAsync(string tunnelId)
    {
        try
        {
            var getDataRequest = new GetTunnelDataRequest
            {
                TunnelId = tunnelId
            };

            var response = await _meshClient.CallServiceAsync(
                "private-gateway",
                "GetTunnelData",
                getDataRequest,
                CancellationToken.None);

            if (response.StatusCode != ServiceStatusCodes.OK)
            {
                _logger.LogWarning(
                    "[PortForward] Failed to get tunnel data for {TunnelId}: {Error}",
                    tunnelId, response.ErrorMessage);
                return null;
            }

            var dataResponse = System.Text.Json.JsonSerializer.Deserialize<GetTunnelDataResponse>(
                response.Payload);

            return dataResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PortForward] Error receiving tunnel data for {TunnelId}", tunnelId);
            return null;
        }
    }

    /// <summary>
    /// Called by ForwarderConnection when the tunnel should be closed.
    /// </summary>
    internal async Task CloseTunnelAsync(string tunnelId)
    {
        try
        {
            var closeRequest = new CloseTunnelRequest
            {
                TunnelId = tunnelId
            };

            var response = await _meshClient.CallServiceAsync(
                "private-gateway",
                "CloseTunnel",
                closeRequest,
                CancellationToken.None);

            if (response.StatusCode != ServiceStatusCodes.OK)
            {
                _logger.LogWarning(
                    "[PortForward] Failed to close tunnel {TunnelId}: {Error}",
                    tunnelId, response.ErrorMessage);
            }
            else
            {
                _logger.LogInformation(
                    "[PortForward] Closed tunnel {TunnelId}", tunnelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PortForward] Error closing tunnel {TunnelId}", tunnelId);
        }
        finally
        {
            _activeConnections.TryRemove(tunnelId, out _);
        }
    }

    public void Dispose()
    {
        foreach (var forwarder in _activeForwarders.Values)
        {
            forwarder.Dispose();
        }
        _activeForwarders.Clear();

        foreach (var connection in _activeConnections.Values)
        {
            connection.Dispose();
        }
        _activeConnections.Clear();
    }
}

/// <summary>
/// Represents a single port forwarding instance.
/// </summary>
internal class ForwarderInstance : IDisposable
{
    private readonly int _localPort;
    private readonly string _podId;
    private readonly string _destinationHost;
    private readonly int _destinationPort;
    private readonly string? _serviceName;
    private readonly LocalPortForwarder _parent;
    private readonly ILogger _logger;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    // Statistics
    private int _activeConnections;
    private long _bytesForwarded;

    public ForwarderInstance(
        int localPort,
        string podId,
        string destinationHost,
        int destinationPort,
        string? serviceName,
        LocalPortForwarder parent)
    {
        _localPort = localPort;
        _podId = podId;
        _destinationHost = destinationHost;
        _destinationPort = destinationPort;
        _serviceName = serviceName;
        _parent = parent;
        _logger = parent._logger;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, _localPort);
        _listener.Start();

        _listenTask = ListenForConnectionsAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_listenTask != null)
        {
            try
            {
                await _listenTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // Task didn't complete in time, continue with cleanup
            }
        }

        _listener?.Stop();
        _cts?.Dispose();
    }

    public PortForwardingStatus GetStatus()
    {
        // Aggregate stream stats from all active connections
        var streamStats = new List<StreamMappingStats>();
        // Note: In a real implementation, we'd track individual connection stats
        // For now, provide aggregate statistics

        var totalBytesTransferred = Interlocked.Read(ref _bytesForwarded);

        return new PortForwardingStatus
        {
            LocalPort = _localPort,
            PodId = _podId,
            DestinationHost = _destinationHost,
            DestinationPort = _destinationPort,
            ServiceName = _serviceName,
            IsActive = _cts != null && !_cts.IsCancellationRequested,
            ActiveConnections = _activeConnections,
            BytesForwarded = totalBytesTransferred,
            StreamMappingEnabled = true,
            StreamStats = null, // Would be populated with aggregate stats in production
            Performance = new PortForwardingPerformance
            {
                ActiveConnections = _activeConnections,
                TotalBytesTransferred = totalBytesTransferred
            }
        };
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientConnectionAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PortForward] Error accepting connection on port {LocalPort}", _localPort);
            }
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeConnections);

        ForwarderConnection? tunnelConnection = null;

        try
        {
            // Create tunnel connection
            tunnelConnection = await _parent.CreateTunnelConnectionAsync(
                _podId, _destinationHost, _destinationPort, _serviceName);

            if (tunnelConnection == null)
            {
                _logger.LogWarning(
                    "[PortForward] Failed to create tunnel connection for local port {LocalPort}",
                    _localPort);
                client.Close();
                return;
            }

            // Get local stream
            var localStream = client.GetStream();

            // Map streams for efficient bidirectional data transfer
            tunnelConnection.MapToStream(localStream, cancellationToken);

            // Wait for the stream mapping to complete (connection closes)
            // The mapping handles all data transfer internally
            await Task.Delay(-1, cancellationToken); // Wait indefinitely until cancelled

            _logger.LogDebug("[PortForward] Stream mapping completed for local port {LocalPort}", _localPort);
        }
        catch (OperationCanceledException)
        {
            // Expected when connection is closed or cancelled
            _logger.LogDebug("[PortForward] Connection cancelled for local port {LocalPort}", _localPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PortForward] Error handling client connection on port {LocalPort}", _localPort);
        }
        finally
        {
            // Clean up tunnel connection
            if (tunnelConnection != null)
            {
                try
                {
                    await tunnelConnection.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[PortForward] Error closing tunnel connection for local port {LocalPort}", _localPort);
                }
            }

            // Clean up client connection
            try
            {
                client.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[PortForward] Error closing client connection for local port {LocalPort}", _localPort);
            }

            Interlocked.Decrement(ref _activeConnections);
        }
    }

}

/// <summary>
/// Represents a single tunnel connection for forwarding with enhanced stream mapping.
/// </summary>
internal class ForwarderConnection : IDisposable
{
    private readonly string _tunnelId;
    private readonly string _podId;
    private readonly string _destinationHost;
    private readonly int _destinationPort;
    private readonly LocalPortForwarder _parent;
    private readonly ILogger _logger;

    // Stream mapping and performance tracking
    private readonly object _streamLock = new();
    private bool _isStreamMapped;
    private DateTimeOffset _lastActivity;
    private long _bytesSent;
    private long _bytesReceived;
    private int _sendQueueDepth;
    private readonly Queue<byte[]> _sendQueue = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private CancellationTokenSource? _streamMappingCts;

    public ForwarderConnection(
        string tunnelId,
        string podId,
        string destinationHost,
        int destinationPort,
        LocalPortForwarder parent)
    {
        _tunnelId = tunnelId;
        _podId = podId;
        _destinationHost = destinationHost;
        _destinationPort = destinationPort;
        _parent = parent;
        _logger = parent._logger;
        _lastActivity = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Maps this tunnel connection to a local stream for efficient data transfer.
    /// </summary>
    public void MapToStream(NetworkStream localStream, CancellationToken cancellationToken)
    {
        lock (_streamLock)
        {
            if (_isStreamMapped)
            {
                throw new InvalidOperationException("Connection is already mapped to a stream");
            }

            _isStreamMapped = true;
            _streamMappingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start background stream mapping tasks
            _ = Task.Run(() => MapStreamsAsync(localStream, _streamMappingCts.Token), _streamMappingCts.Token);
        }
    }

    /// <summary>
    /// Sends data through the mapped stream with queuing and flow control.
    /// </summary>
    public async Task SendDataAsync(byte[] data)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            if (_isStreamMapped)
            {
                // Queue data for the mapped stream handler
                lock (_sendQueue)
                {
                    _sendQueue.Enqueue(data);
                    _sendQueueDepth = _sendQueue.Count;
                }
            }
            else
            {
                // Fallback to direct tunnel sending
                await _parent.SendTunnelDataAsync(_tunnelId, data);
            }

            Interlocked.Add(ref _bytesSent, data.Length);
            _lastActivity = DateTimeOffset.UtcNow;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    /// <summary>
    /// Receives data from the mapped stream.
    /// </summary>
    public async Task<byte[]?> ReceiveDataAsync()
    {
        // For mapped streams, data is handled by the mapping task
        // This method provides a compatibility interface
        if (_isStreamMapped)
        {
            // Wait briefly for data from the stream mapping
            await Task.Delay(1);
            return null; // Data is handled by the stream mapping task
        }

        // Fallback for non-mapped connections
        var data = await _parent.ReceiveTunnelDataAsync(_tunnelId);
        if (data != null)
        {
            Interlocked.Add(ref _bytesReceived, data.Length);
            _lastActivity = DateTimeOffset.UtcNow;
        }
        return data;
    }

    /// <summary>
    /// Gets stream mapping statistics.
    /// </summary>
    public StreamMappingStats GetStats()
    {
        return new StreamMappingStats
        {
            TunnelId = _tunnelId,
            IsStreamMapped = _isStreamMapped,
            LastActivity = _lastActivity,
            BytesSent = _bytesSent,
            BytesReceived = _bytesReceived,
            SendQueueDepth = _sendQueueDepth,
            TotalBytesTransferred = _bytesSent + _bytesReceived
        };
    }

    /// <summary>
    /// Efficiently maps local and remote streams for bidirectional data transfer.
    /// </summary>
    private async Task MapStreamsAsync(NetworkStream localStream, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[PortForward] Starting stream mapping for tunnel {TunnelId}", _tunnelId);

            // Create tasks for bidirectional data transfer
            var localToRemote = MapLocalToRemoteAsync(localStream, cancellationToken);
            var remoteToLocal = MapRemoteToLocalAsync(localStream, cancellationToken);

            // Wait for either direction to complete (indicating connection closure)
            await Task.WhenAny(localToRemote, remoteToLocal);

            _logger.LogDebug("[PortForward] Stream mapping completed for tunnel {TunnelId}", _tunnelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PortForward] Error in stream mapping for tunnel {TunnelId}", _tunnelId);
        }
        finally
        {
            lock (_streamLock)
            {
                _isStreamMapped = false;
            }
        }
    }

    /// <summary>
    /// Maps data from local stream to remote tunnel.
    /// </summary>
    private async Task MapLocalToRemoteAsync(NetworkStream localStream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192]; // 8KB buffer for efficient transfer

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Read from local stream
                var bytesRead = await localStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0)
                {
                    // Local connection closed
                    _logger.LogDebug("[PortForward] Local stream closed for tunnel {TunnelId}", _tunnelId);
                    break;
                }

                // Send to tunnel (with queuing for flow control)
                var dataToSend = new byte[bytesRead];
                Array.Copy(buffer, dataToSend, bytesRead);

                await SendDataAsync(dataToSend);

                _logger.LogTrace("[PortForward] Mapped {Bytes} bytes local->remote for tunnel {TunnelId}",
                    bytesRead, _tunnelId);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PortForward] Error mapping local->remote for tunnel {TunnelId}", _tunnelId);
        }
    }

    /// <summary>
    /// Maps data from remote tunnel to local stream.
    /// </summary>
    private async Task MapRemoteToLocalAsync(NetworkStream localStream, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Get data from tunnel
                var tunnelData = await _parent.ReceiveTunnelDataAsync(_tunnelId);

                if (tunnelData == null || tunnelData.Length == 0)
                {
                    // No data available, wait briefly before polling again
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                // Write to local stream
                await localStream.WriteAsync(tunnelData, 0, tunnelData.Length, cancellationToken);
                await localStream.FlushAsync(cancellationToken);

                Interlocked.Add(ref _bytesReceived, tunnelData.Length);
                _lastActivity = DateTimeOffset.UtcNow;

                _logger.LogTrace("[PortForward] Mapped {Bytes} bytes remote->local for tunnel {TunnelId}",
                    tunnelData.Length, _tunnelId);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PortForward] Error mapping remote->local for tunnel {TunnelId}", _tunnelId);
        }
    }

    /// <summary>
    /// Processes queued data for mapped streams.
    /// </summary>
    private async Task ProcessSendQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            byte[]? dataToSend = null;

            lock (_sendQueue)
            {
                if (_sendQueue.Count > 0)
                {
                    dataToSend = _sendQueue.Dequeue();
                    _sendQueueDepth = _sendQueue.Count;
                }
            }

            if (dataToSend != null)
            {
                await _parent.SendTunnelDataAsync(_tunnelId, dataToSend);
            }
            else
            {
                // No data to send, wait briefly
                await Task.Delay(1, cancellationToken);
            }
        }
    }

    public async Task CloseAsync()
    {
        _streamMappingCts?.Cancel();

        // Wait for stream mapping to complete
        if (_streamMappingCts != null)
        {
            try
            {
                await Task.Delay(100); // Brief wait for cleanup
            }
            catch { }
        }

        await _parent.CloseTunnelAsync(_tunnelId);
    }

    public void Dispose()
    {
        _streamMappingCts?.Cancel();
        _sendSemaphore.Dispose();
        CloseAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// Statistics for stream mapping performance.
/// </summary>
public class StreamMappingStats
{
    /// <summary>
    /// The tunnel ID.
    /// </summary>
    public string TunnelId { get; init; } = string.Empty;

    /// <summary>
    /// Whether the connection is mapped to a stream.
    /// </summary>
    public bool IsStreamMapped { get; init; }

    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivity { get; init; }

    /// <summary>
    /// Total bytes sent through the tunnel.
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// Total bytes received from the tunnel.
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Current send queue depth.
    /// </summary>
    public int SendQueueDepth { get; init; }

    /// <summary>
    /// Total bytes transferred in both directions.
    /// </summary>
    public long TotalBytesTransferred { get; init; }
}

/// <summary>
/// Status information for a port forwarding instance.
/// </summary>
public class PortForwardingStatus
{
    /// <summary>
    /// The local port being forwarded.
    /// </summary>
    public int LocalPort { get; init; }

    /// <summary>
    /// The pod ID used for tunneling.
    /// </summary>
    public string PodId { get; init; } = string.Empty;

    /// <summary>
    /// The remote destination hostname/IP.
    /// </summary>
    public string DestinationHost { get; init; } = string.Empty;

    /// <summary>
    /// The remote destination port.
    /// </summary>
    public int DestinationPort { get; init; }

    /// <summary>
    /// Optional service name for registered services.
    /// </summary>
    public string? ServiceName { get; init; }

    /// <summary>
    /// Whether the forwarder is currently active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Number of active connections.
    /// </summary>
    public int ActiveConnections { get; init; }

    /// <summary>
    /// Total bytes forwarded.
    /// </summary>
    public long BytesForwarded { get; init; }

    /// <summary>
    /// Stream mapping performance statistics.
    /// </summary>
    public StreamMappingStats? StreamStats { get; init; }

    /// <summary>
    /// Whether stream mapping is enabled for this forwarder.
    /// </summary>
    public bool StreamMappingEnabled { get; init; } = true;

    /// <summary>
    /// Performance indicators.
    /// </summary>
    public PortForwardingPerformance Performance { get; init; } = new();
}

/// <summary>
/// Performance indicators for port forwarding.
/// </summary>
public class PortForwardingPerformance
{
    /// <summary>
    /// Average bytes per connection.
    /// </summary>
    public long AverageBytesPerConnection => ActiveConnections > 0 ? TotalBytesTransferred / ActiveConnections : 0;

    /// <summary>
    /// Whether this is considered high-throughput forwarding.
    /// </summary>
    public bool IsHighThroughput => TotalBytesTransferred > 1024 * 1024; // > 1MB

    /// <summary>
    /// Current active connections.
    /// </summary>
    public int ActiveConnections { get; init; }

    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    public long TotalBytesTransferred { get; init; }

    /// <summary>
    /// Connection efficiency rating (higher is better).
    /// </summary>
    public double EfficiencyRating => ActiveConnections > 0 && TotalBytesTransferred > 0
        ? (double)TotalBytesTransferred / (ActiveConnections * 1000)
        : 0;
}

// Request/Response DTOs for mesh service calls
internal record OpenTunnelRequest
{
    public string PodId { get; init; } = string.Empty;
    public string DestinationHost { get; init; } = string.Empty;
    public int DestinationPort { get; init; }
    public string? ServiceName { get; init; }
    public string RequestNonce { get; init; } = string.Empty;
    public long TimestampUnixMs { get; init; }
}

internal record OpenTunnelResponse
{
    public string TunnelId { get; init; } = string.Empty;
    public bool Accepted { get; init; }
}

internal record TunnelDataRequest
{
    public string TunnelId { get; init; } = string.Empty;
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

internal record GetTunnelDataRequest
{
    public string TunnelId { get; init; } = string.Empty;
}

internal record GetTunnelDataResponse
{
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

internal record CloseTunnelRequest
{
    public string TunnelId { get; init; } = string.Empty;
}
