// <copyright file="MeshOverlayServer.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.DhtRendezvous.Search;
using slskd.DhtRendezvous.Security;
using slskd.Mesh;
using slskd.Mesh.ServiceFabric;

using ProtocolViolationException = slskd.DhtRendezvous.Security.ProtocolViolationException;

/// <summary>
/// TCP server for accepting inbound overlay connections from mesh peers.
/// Only runs if this client is beacon-capable (publicly reachable).
/// </summary>
public sealed class MeshOverlayServer : IMeshOverlayServer, IAsyncDisposable
{
    private readonly ILogger<MeshOverlayServer> _logger;
    private readonly IOptionsMonitor<slskd.Options> _optionsMonitor;
    private readonly CertificateManager _certificateManager;
    private readonly CertificatePinStore _pinStore;
    private readonly OverlayRateLimiter _rateLimiter;
    private readonly OverlayBlocklist _blocklist;
    private readonly MeshNeighborRegistry _registry;
    private readonly IMeshOverlayConnector _overlayConnector;
    private readonly IMeshSyncService _meshSyncService;
    private readonly IMeshSearchRpcHandler _meshSearchRpcHandler;
    private readonly MeshOverlayRequestRouter _requestRouter;
    private readonly MeshServiceRouter? _serviceRouter;
    private readonly DhtRendezvousOptions _dhtOptions;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private DateTimeOffset? _startedAt;
    private long _totalAccepted;
    private long _totalRejected;

    // Helper for deserializing raw messages (stateless, just need the JSON options)
    private readonly SecureMessageFramer _framerInstance = new(Stream.Null);

    private string LocalUsername => _optionsMonitor.CurrentValue?.Soulseek?.Username ?? "unknown";
    private int ListenPortConfig => _dhtOptions.OverlayPort;

    public MeshOverlayServer(
        ILogger<MeshOverlayServer> logger,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        CertificateManager certificateManager,
        CertificatePinStore pinStore,
        OverlayRateLimiter rateLimiter,
        OverlayBlocklist blocklist,
        MeshNeighborRegistry registry,
        IMeshOverlayConnector overlayConnector,
        IMeshSyncService meshSyncService,
        IMeshSearchRpcHandler meshSearchRpcHandler,
        MeshOverlayRequestRouter requestRouter,
        DhtRendezvousOptions dhtOptions,
        MeshServiceRouter? serviceRouter = null)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _certificateManager = certificateManager;
        _pinStore = pinStore;
        _rateLimiter = rateLimiter;
        _blocklist = blocklist;
        _registry = registry;
        _overlayConnector = overlayConnector ?? throw new ArgumentNullException(nameof(overlayConnector));
        _meshSyncService = meshSyncService;
        _meshSearchRpcHandler = meshSearchRpcHandler ?? throw new ArgumentNullException(nameof(meshSearchRpcHandler));
        _requestRouter = requestRouter ?? throw new ArgumentNullException(nameof(requestRouter));
        _dhtOptions = dhtOptions;
        _serviceRouter = serviceRouter;
    }

    public bool IsListening => _listener is not null;
    public int ListenPort => ListenPortConfig;
    public int ActiveConnections => _registry.Count;
    public long TotalConnectionsAccepted => _totalAccepted;
    public long TotalConnectionsRejected => _totalRejected;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            _logger.LogWarning("Server already running");
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, ListenPortConfig);

        try
        {
            _listener.Start();
            _startedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Mesh overlay server started on port {Port}",
                ListenPortConfig);

            _acceptLoopTask = AcceptLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start mesh overlay server on port {Port}", ListenPortConfig);
            _listener.Stop();
            _listener = null;
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_listener is null)
        {
            return;
        }

        _logger.LogInformation("Stopping mesh overlay server");

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        _listener.Stop();
        _listener = null;

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _startedAt = null;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);

                // Handle connection in background
                _ = HandleConnectionAsync(tcpClient, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error accepting connection");
            }
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Accepted connections are registered for ongoing ownership or explicitly disconnected on rejection and error paths.")]
    private async Task HandleConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
        var remoteIp = remoteEndPoint.Address;

        try
        {
            // Check blocklist
            if (_blocklist.IsBlocked(remoteIp))
            {
                _logger.LogDebug("Rejected connection from blocked IP {Ip}", remoteIp);
                Interlocked.Increment(ref _totalRejected);
                tcpClient.Dispose();
                return;
            }

            // Check rate limit
            var rateResult = _rateLimiter.CheckConnection(remoteIp);
            if (!rateResult)
            {
                _logger.LogDebug("Rejected connection from {Ip}: {Reason}", remoteIp, rateResult.Reason);
                Interlocked.Increment(ref _totalRejected);
                tcpClient.Dispose();
                return;
            }

            // Check if registry is full
            if (_registry.IsFull)
            {
                _logger.LogDebug("Rejected connection from {Ip}: registry full", remoteIp);
                Interlocked.Increment(ref _totalRejected);
                _rateLimiter.RecordDisconnection(remoteIp);
                tcpClient.Dispose();
                return;
            }

            _logger.LogDebug("Accepting connection from {Endpoint}", OverlayLogSanitizer.Endpoint(remoteEndPoint));

            // Establish TLS and perform handshake
            var serverCert = _certificateManager.GetOrCreateServerCertificate();
            var connection = await MeshOverlayConnection.AcceptAsync(tcpClient, serverCert, cancellationToken);

            try
            {
                // Perform protocol handshake
                var hello = await connection.PerformServerHandshakeAsync(
                    LocalUsername,
                    overlayPort: ListenPortConfig,
                    cancellationToken: cancellationToken);

                // Check if username is blocked
                if (_blocklist.IsBlocked(hello.Username))
                {
                    _logger.LogWarning("Rejected connection from blocked user {Username}", OverlayLogSanitizer.Username(hello.Username));
                    Interlocked.Increment(ref _totalRejected);
                    await connection.DisconnectAsync("Blocked", cancellationToken);
                    return;
                }

                // Check certificate pin (TOFU)
                if (connection.CertificateThumbprint is not null)
                {
                    var pinResult = _pinStore.CheckPin(hello.Username, connection.CertificateThumbprint);

                    switch (pinResult)
                    {
                        case PinCheckResult.NotPinned:
                            // First time seeing this user - pin their certificate
                            // SECURITY: Log at INFO level for TOFU visibility
                            _logger.LogInformation(
                                "TOFU: First connection from {Username}, pinning certificate {Thumbprint}",
                                OverlayLogSanitizer.Username(hello.Username),
                                connection.CertificateThumbprint?[..16] + "...");
                            _pinStore.SetPin(hello.Username, connection.CertificateThumbprint ?? string.Empty);
                            break;

                        case PinCheckResult.Valid:
                            // Certificate matches pin
                            _pinStore.TouchPin(hello.Username);
                            break;

                        case PinCheckResult.Mismatch:
                            _logger.LogWarning(
                                "Certificate pin mismatch for {Username}; rotating stored pin to newly presented certificate.",
                                OverlayLogSanitizer.Username(hello.Username));
                            _pinStore.RotatePin(hello.Username, connection.CertificateThumbprint ?? string.Empty);
                            break;
                    }
                }

                // Register the connection
                if (!await _registry.RegisterAsync(connection))
                {
                    _logger.LogDebug("Failed to register connection from {Username}", OverlayLogSanitizer.Username(hello.Username));
                    Interlocked.Increment(ref _totalRejected);
                    await connection.DisconnectAsync("Registration failed", cancellationToken);
                    return;
                }

                Interlocked.Increment(ref _totalAccepted);

                _logger.LogInformation(
                    "Accepted mesh connection from {Username}@{Endpoint} (features: {Features})",
                    OverlayLogSanitizer.Username(hello.Username),
                    OverlayLogSanitizer.Endpoint(remoteEndPoint),
                    string.Join(", ", (IEnumerable<string>?)hello.Features ?? Array.Empty<string>()));

                TryStartReciprocalOutboundConnection(remoteEndPoint.Address, hello);

                // Start message handling loop in background
                _ = HandleMessagesAsync(connection, cancellationToken);
                connection = null;
            }
            catch (Exception ex)
            {
                if (IsExpectedHandshakeNoise(ex))
                {
                    _logger.LogDebug("Ignoring non-overlay TLS noise from {Endpoint}: {Message}", OverlayLogSanitizer.Endpoint(remoteEndPoint), ex.Message);
                    Interlocked.Increment(ref _totalRejected);
                    _rateLimiter.RecordDisconnection(remoteIp);
                }
                else
                {
                    _logger.LogWarning(ex, "Handshake failed with {Endpoint}", OverlayLogSanitizer.Endpoint(remoteEndPoint));
                    Interlocked.Increment(ref _totalRejected);
                    _rateLimiter.RecordViolation(remoteIp);
                }

                if (connection != null)
                {
                    await connection.DisposeAsync();
                    connection = null;
                }
            }
        }
        catch (Exception ex)
        {
            if (IsExpectedHandshakeNoise(ex))
            {
                _logger.LogDebug("Ignoring non-overlay TLS noise from {Endpoint}: {Message}", OverlayLogSanitizer.Endpoint(remoteEndPoint), ex.Message);
            }
            else
            {
                _logger.LogWarning(ex, "Error handling connection from {Endpoint}", OverlayLogSanitizer.Endpoint(remoteEndPoint));
            }

            Interlocked.Increment(ref _totalRejected);
            _rateLimiter.RecordDisconnection(remoteIp);
            tcpClient.Dispose();
        }
    }

    private void TryStartReciprocalOutboundConnection(IPAddress remoteAddress, Messages.MeshHelloMessage hello)
    {
        if (hello.Username.Equals(LocalUsername, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Older peers do not advertise overlay_port yet. Try our configured overlay port as a compatibility
        // fallback; if they use a custom port, this harmlessly fails under the connector timeout/backoff path.
        var port = hello.OverlayPort is > 0 ? hello.OverlayPort.Value : ListenPortConfig;
        if (port <= 0)
        {
            return;
        }

        var endpoint = new IPEndPoint(remoteAddress, port);
        _ = Task.Run(async () =>
        {
            try
            {
                await _overlayConnector.ConnectToEndpointAsync(endpoint).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reciprocal overlay connect to {Username}@{Endpoint} failed: {Message}", OverlayLogSanitizer.Username(hello.Username), OverlayLogSanitizer.Endpoint(endpoint), ex.Message);
            }
        });
    }

    private async Task HandleMessagesAsync(MeshOverlayConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && connection.IsConnected)
            {
                // Check for idle timeout
                if (connection.IsIdle())
                {
                    _logger.LogDebug("Connection to {Username} idle, disconnecting", OverlayLogSanitizer.Username(connection.Username));
                    break;
                }

                // Send keepalive if needed
                if (connection.NeedsKeepalive())
                {
                    try
                    {
                        var rtt = await connection.PingAsync(cancellationToken);
                        _logger.LogTrace("Ping to {Username}: {Rtt}ms", OverlayLogSanitizer.Username(connection.Username), rtt.TotalMilliseconds);
                    }
                    catch
                    {
                        _logger.LogDebug("Keepalive failed for {Username}", OverlayLogSanitizer.Username(connection.Username));
                        break;
                    }
                }

                // Check message rate limit
                var rateResult = _rateLimiter.CheckMessage(connection.ConnectionId);
                if (!rateResult)
                {
                    _logger.LogWarning(
                        "Message rate limit exceeded for {Username}: {Reason}",
                        OverlayLogSanitizer.Username(connection.Username),
                        rateResult.Reason);
                    _rateLimiter.RecordViolation(connection.RemoteAddress);
                    break;
                }

                // Read next message
                try
                {
                    var rawMessage = await connection.ReadRawMessageAsync(cancellationToken);
                    var messageType = SecureMessageFramer.ExtractMessageType(rawMessage);

                    switch (messageType)
                    {
                        case Messages.OverlayMessageType.Ping:
                            var ping = _framerInstance.DeserializeMessage<Messages.PingMessage>(rawMessage);

                            // SECURITY: Validate ping message before responding
                            var pingValidation = MessageValidator.ValidatePing(ping);
                            if (!pingValidation.IsValid)
                            {
                                _logger.LogWarning("Invalid ping from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), pingValidation.Error);
                                _rateLimiter.RecordViolation(connection.RemoteAddress);
                                break;
                            }

                            await connection.WriteMessageAsync(new Messages.PongMessage { Timestamp = ping.Timestamp }, cancellationToken);
                            break;

                        case Messages.OverlayMessageType.Pong:
                            // Already handled by PingAsync
                            break;

                        case Messages.OverlayMessageType.Disconnect:
                            var disconnect = _framerInstance.DeserializeMessage<Messages.DisconnectMessage>(rawMessage);
                            var disconnectValidation = MessageValidator.ValidateDisconnect(disconnect);
                            if (!disconnectValidation.IsValid)
                            {
                                _logger.LogWarning("Invalid disconnect from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), disconnectValidation.Error);
                            }
                            else
                            {
                                _logger.LogDebug("Received disconnect from {Username}: {Reason}", OverlayLogSanitizer.Username(connection.Username), disconnect?.Reason ?? "no reason");
                            }

                            goto cleanup;

                        case Messages.OverlayMessageType.MeshSearchReq:
                            var meshSearchReq = _framerInstance.DeserializeMessage<Messages.MeshSearchRequestMessage>(rawMessage);
                            var reqVal = MessageValidator.ValidateMeshSearchReq(meshSearchReq);
                            if (!reqVal.IsValid)
                            {
                                _logger.LogWarning("Invalid mesh_search_req from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), reqVal.Error);
                                _rateLimiter.RecordViolation(connection.RemoteAddress);
                                break;
                            }

                            var meshRl = _rateLimiter.CheckMeshSearchRequest(connection.ConnectionId);
                            if (!meshRl)
                            {
                                _logger.LogWarning("Mesh search rate limit exceeded for {Username}: {Reason}", OverlayLogSanitizer.Username(connection.Username), meshRl.Reason);
                                break;
                            }

                            var meshSearchResp = await _meshSearchRpcHandler.HandleAsync(meshSearchReq, cancellationToken);
                            await connection.WriteMessageAsync(meshSearchResp, cancellationToken);
                            break;

                        case Messages.OverlayMessageType.MeshSearchResp:
                            var meshSearchResponse = _framerInstance.DeserializeMessage<Messages.MeshSearchResponseMessage>(rawMessage);
                            if (!_requestRouter.TryCompleteMeshSearchResponse(connection, meshSearchResponse))
                            {
                                _logger.LogDebug("Unexpected mesh_search_resp from {Username}, ignoring", OverlayLogSanitizer.Username(connection.Username));
                            }

                            break;

                        case Messages.OverlayMessageType.MeshServiceCall:
                            await HandleMeshServiceCallAsync(connection, rawMessage, cancellationToken);
                            break;

                        case Messages.OverlayMessageType.MeshServiceReply:
                            var meshServiceReply = _framerInstance.DeserializeMessage<Messages.MeshServiceReplyMessage>(rawMessage);
                            if (!_requestRouter.TryCompleteMeshServiceReply(connection, ToServiceReply(meshServiceReply)))
                            {
                                _logger.LogDebug("Unexpected mesh_service_reply from {Username}, ignoring", OverlayLogSanitizer.Username(connection.Username));
                            }

                            break;

                        default:
                            // Forward to mesh sync service for handling
                            if (connection.Username is not null)
                            {
                                await HandleMeshMessageAsync(connection, rawMessage, messageType, cancellationToken);
                            }

                            break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogTrace("No overlay message from {Username} before read timeout; keeping connection open", OverlayLogSanitizer.Username(connection.Username));
                    continue;
                }
                catch (System.IO.EndOfStreamException)
                {
                    _logger.LogDebug("Connection closed by {Username}", OverlayLogSanitizer.Username(connection.Username));
                    break;
                }
                catch (ProtocolViolationException ex)
                {
                    _logger.LogWarning("Protocol violation from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), ex.Message);
                    _rateLimiter.RecordViolation(connection.RemoteAddress);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in message loop for {Username}", OverlayLogSanitizer.Username(connection.Username));
        }

    cleanup:
        await _registry.UnregisterAsync(connection);
        _requestRouter.RemoveConnection(connection);
        _rateLimiter.RecordDisconnection(connection.RemoteAddress);
        _rateLimiter.RemoveConnection(connection.ConnectionId);
        await connection.DisposeAsync();
    }

    /// <summary>
    /// Handle mesh protocol messages by forwarding to MeshSyncService.
    /// </summary>
    private async Task HandleMeshMessageAsync(MeshOverlayConnection connection, byte[] rawMessage, string? messageType, CancellationToken cancellationToken)
    {
        try
        {
            // Try to parse as a mesh message
            Mesh.Messages.MeshMessage? meshMessage = messageType switch
            {
                "mesh_sync_hello" => _framerInstance.DeserializeMessage<Mesh.Messages.MeshHelloMessage>(rawMessage),
                "mesh_req_delta" => _framerInstance.DeserializeMessage<Mesh.Messages.MeshReqDeltaMessage>(rawMessage),
                "mesh_push_delta" => _framerInstance.DeserializeMessage<Mesh.Messages.MeshPushDeltaMessage>(rawMessage),
                "mesh_req_key" => _framerInstance.DeserializeMessage<Mesh.Messages.MeshReqKeyMessage>(rawMessage),
                "mesh_ack" => _framerInstance.DeserializeMessage<Mesh.Messages.MeshAckMessage>(rawMessage),
                _ => null,
            };

            if (meshMessage == null)
            {
                _logger.LogDebug("Unknown message type {Type} from {Username}, ignoring", messageType, OverlayLogSanitizer.Username(connection.Username));
                return;
            }

            _logger.LogDebug("Forwarding {Type} message from {Username} to MeshSyncService", messageType, OverlayLogSanitizer.Username(connection.Username));

            // Forward to mesh sync service
            var response = await _meshSyncService.HandleMessageAsync(connection.Username!, meshMessage, cancellationToken);

            // Send response if any
            if (response != null)
            {
                await connection.WriteMessageAsync(response, cancellationToken);
            }
        }
        catch (ProtocolViolationException ex)
        {
            _logger.LogWarning("Protocol violation parsing mesh message from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), ex.Message);
            _rateLimiter.RecordViolation(connection.RemoteAddress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling mesh message from {Username}", OverlayLogSanitizer.Username(connection.Username));
        }
    }

    private async Task HandleMeshServiceCallAsync(MeshOverlayConnection connection, byte[] rawMessage, CancellationToken cancellationToken)
    {
        var callMessage = _framerInstance.DeserializeMessage<Messages.MeshServiceCallMessage>(rawMessage);
        if (_serviceRouter == null)
        {
            await connection.WriteMessageAsync(ToMeshServiceReplyMessage(new ServiceReply
            {
                CorrelationId = callMessage.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Mesh service router unavailable",
            }), cancellationToken);
            return;
        }

        var call = new ServiceCall
        {
            CorrelationId = callMessage.CorrelationId,
            ServiceName = callMessage.ServiceName,
            Method = callMessage.Method,
            Payload = callMessage.Payload,
        };

        var reply = await _serviceRouter.RouteAsync(call, connection.Username ?? connection.ConnectionId, connection.CertificateThumbprint, cancellationToken);
        await connection.WriteMessageAsync(ToMeshServiceReplyMessage(reply), cancellationToken);
    }

    private static ServiceReply ToServiceReply(Messages.MeshServiceReplyMessage message)
    {
        return new ServiceReply
        {
            CorrelationId = message.CorrelationId,
            StatusCode = message.StatusCode,
            Payload = message.Payload,
            ErrorMessage = message.ErrorMessage,
        };
    }

    private static Messages.MeshServiceReplyMessage ToMeshServiceReplyMessage(ServiceReply reply)
    {
        return new Messages.MeshServiceReplyMessage
        {
            CorrelationId = reply.CorrelationId,
            StatusCode = reply.StatusCode,
            Payload = reply.Payload,
            ErrorMessage = reply.ErrorMessage,
        };
    }

    internal static bool IsExpectedHandshakeNoise(Exception exception)
    {
        if (exception is AuthenticationException authenticationException)
        {
            return authenticationException.Message.Contains("Cannot determine the frame size", StringComparison.Ordinal) ||
                authenticationException.Message.Contains("corrupted frame was received", StringComparison.Ordinal);
        }

        return exception.InnerException is not null && IsExpectedHandshakeNoise(exception.InnerException);
    }

    public MeshOverlayServerStats GetStats()
    {
        return new MeshOverlayServerStats
        {
            IsListening = IsListening,
            ListenPort = ListenPortConfig,
            ActiveConnections = ActiveConnections,
            TotalConnectionsAccepted = TotalConnectionsAccepted,
            TotalConnectionsRejected = TotalConnectionsRejected,
            StartedAt = _startedAt,
        };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
