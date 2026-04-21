// <copyright file="MeshOverlayConnector.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Search;
using slskd.DhtRendezvous.Security;
using slskd.Mesh;
using slskd.Mesh.ServiceFabric;

/// <summary>
/// Makes outbound overlay connections to mesh peers discovered via DHT.
/// </summary>
public sealed class MeshOverlayConnector : IMeshOverlayConnector
{
    private readonly ILogger<MeshOverlayConnector> _logger;
    private readonly IOptionsMonitor<slskd.Options> _optionsMonitor;
    private readonly CertificateManager _certificateManager;
    private readonly CertificatePinStore _pinStore;
    private readonly OverlayRateLimiter _rateLimiter;
    private readonly OverlayBlocklist _blocklist;
    private readonly MeshNeighborRegistry _registry;
    private readonly IMeshSyncService _meshSyncService;
    private readonly IMeshSearchRpcHandler _meshSearchRpcHandler;
    private readonly MeshOverlayRequestRouter _requestRouter;
    private readonly MeshServiceRouter? _serviceRouter;
    private readonly ConcurrentDictionary<string, EndpointAttemptState> _endpointAttemptStates = new();
    private int _pendingConnections;
    private long _successfulConnections;
    private long _failedConnections;
    private long _connectTimeoutFailures;
    private long _noRouteFailures;
    private long _connectionRefusedFailures;
    private long _connectionResetFailures;
    private long _tlsEofFailures;
    private long _tlsHandshakeFailures;
    private long _protocolHandshakeFailures;
    private long _registrationFailures;
    private long _blockedPeerFailures;
    private long _unknownFailures;
    private long _endpointCooldownSkips;
    private static readonly TimeSpan EndpointFailureBaseCooldown = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EndpointFailureMaxCooldown = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Maximum concurrent connection attempts.
    /// </summary>
    public const int MaxConcurrentAttempts = 3;

    private string LocalUsername => (_optionsMonitor.CurrentValue?.Soulseek?.Username ?? "unknown").Trim();

    public MeshOverlayConnector(
        ILogger<MeshOverlayConnector> logger,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        CertificateManager certificateManager,
        CertificatePinStore pinStore,
        OverlayRateLimiter rateLimiter,
        OverlayBlocklist blocklist,
        MeshNeighborRegistry registry,
        IMeshSyncService meshSyncService,
        IMeshSearchRpcHandler meshSearchRpcHandler,
        MeshOverlayRequestRouter requestRouter,
        MeshServiceRouter? serviceRouter = null)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _certificateManager = certificateManager;
        _pinStore = pinStore;
        _rateLimiter = rateLimiter;
        _blocklist = blocklist;
        _registry = registry;
        _meshSyncService = meshSyncService;
        _meshSearchRpcHandler = meshSearchRpcHandler;
        _requestRouter = requestRouter;
        _serviceRouter = serviceRouter;
    }

    public int PendingConnections => _pendingConnections;
    public long SuccessfulConnections => _successfulConnections;
    public long FailedConnections => _failedConnections;

    public async Task<int> ConnectToCandidatesAsync(
        IEnumerable<IPEndPoint> candidates,
        CancellationToken cancellationToken = default)
    {
        var successCount = 0;
        var shuffled = candidates
            .Distinct(IPEndPointComparer.Instance)
            .ToList();

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        foreach (var endpoint in shuffled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (_registry.Count >= MeshNeighborRegistry.MaxNeighbors)
            {
                _logger.LogDebug("Registry at max capacity, stopping connection attempts");
                break;
            }

            if (_registry.IsConnectedTo(endpoint))
            {
                continue;
            }

            if (_blocklist.IsBlocked(endpoint.Address))
            {
                continue;
            }

            var connection = await ConnectToEndpointAsync(endpoint, cancellationToken);
            if (connection is not null)
            {
                successCount++;
            }
        }

        return successCount;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The connection is either registered for ongoing ownership or explicitly disconnected on failure paths.")]
    public async Task<MeshOverlayConnection?> ConnectToEndpointAsync(
        IPEndPoint endpoint,
        CancellationToken cancellationToken = default)
    {
        if (_registry.IsConnectedTo(endpoint))
        {
            _logger.LogDebug("Already connected to {Endpoint}", OverlayLogSanitizer.Endpoint(endpoint));
            return null;
        }

        if (_blocklist.IsBlocked(endpoint.Address))
        {
            _logger.LogDebug("Endpoint {Endpoint} is blocked", OverlayLogSanitizer.Endpoint(endpoint));
            return null;
        }

        if (TryGetEndpointCooldown(endpoint, out var cooldownRemaining, out var endpointState))
        {
            Interlocked.Increment(ref _endpointCooldownSkips);
            _logger.LogDebug(
                "Skipping overlay connect to {Endpoint}; endpoint cooling down for {CooldownSeconds}s after {FailureReason} streak={FailureCount}",
                OverlayLogSanitizer.Endpoint(endpoint),
                Math.Max(1, (int)Math.Ceiling(cooldownRemaining.TotalSeconds)),
                endpointState?.LastFailureReason ?? OverlayConnectionFailureReason.Unknown,
                endpointState?.ConsecutiveFailureCount ?? 0);
            return null;
        }

        if (_pendingConnections >= MaxConcurrentAttempts)
        {
            _logger.LogDebug("Too many pending connections, skipping {Endpoint}", OverlayLogSanitizer.Endpoint(endpoint));
            return null;
        }

        Interlocked.Increment(ref _pendingConnections);

        try
        {
            _logger.LogDebug("Connecting to mesh peer at {Endpoint}", OverlayLogSanitizer.Endpoint(endpoint));

            var clientCert = _certificateManager.GetOrCreateServerCertificate();
            var connection = await MeshOverlayConnection.ConnectAsync(endpoint, clientCert, cancellationToken);

            try
            {
                var ack = await connection.PerformClientHandshakeAsync(
                    LocalUsername,
                    overlayPort: _optionsMonitor.CurrentValue?.DhtRendezvous?.OverlayPort,
                    cancellationToken: cancellationToken);

                if (_blocklist.IsBlocked(ack.Username))
                {
                    _logger.LogWarning("Connected to blocked user {Username}, disconnecting", OverlayLogSanitizer.Username(ack.Username));
                    await connection.DisconnectAsync("Blocked", cancellationToken);
                    RecordFailure(OverlayConnectionFailureReason.BlockedPeer, endpoint, ack.Username);
                    return null;
                }

                if (connection.CertificateThumbprint is not null)
                {
                    var pinResult = _pinStore.CheckPin(ack.Username, connection.CertificateThumbprint);

                    switch (pinResult)
                    {
                        case PinCheckResult.NotPinned:
                            _logger.LogInformation(
                                "TOFU: First connection to {Username}, pinning certificate {Thumbprint}",
                                OverlayLogSanitizer.Username(ack.Username),
                                connection.CertificateThumbprint?[..16] + "...");
                            _pinStore.SetPin(ack.Username, connection.CertificateThumbprint ?? string.Empty);
                            break;

                        case PinCheckResult.Valid:
                            _pinStore.TouchPin(ack.Username);
                            break;

                        case PinCheckResult.Mismatch:
                            _logger.LogWarning(
                                "Certificate pin mismatch for {Username}; rotating stored pin to newly presented certificate.",
                                OverlayLogSanitizer.Username(ack.Username));
                            _pinStore.RotatePin(ack.Username, connection.CertificateThumbprint ?? string.Empty);
                            break;
                    }
                }

                if (!await _registry.RegisterAsync(connection))
                {
                    _logger.LogDebug("Failed to register connection to {Username}", OverlayLogSanitizer.Username(ack.Username));
                    await connection.DisconnectAsync("Registration failed", cancellationToken);
                    RecordFailure(OverlayConnectionFailureReason.RegistrationFailed, endpoint, ack.Username);
                    return null;
                }

                Interlocked.Increment(ref _successfulConnections);
                RecordSuccess(endpoint, ack.Username);

                _logger.LogInformation(
                    "Connected to mesh peer {Username}@{Endpoint} (features: {Features})",
                    OverlayLogSanitizer.Username(ack.Username),
                    OverlayLogSanitizer.Endpoint(endpoint),
                    string.Join(", ", (IEnumerable<string>?)ack.Features ?? Array.Empty<string>()));

                var registeredConnection = connection;
                _ = RunOutboundMessageLoopAsync(registeredConnection, CancellationToken.None);
                connection = null;
                return registeredConnection;
            }
            catch (Exception ex)
            {
                var reason = ClassifyFailure(ex);
                _logger.LogDebug(ex, "Handshake failed with {Endpoint} ({FailureReason})", OverlayLogSanitizer.Endpoint(endpoint), reason);
                _rateLimiter.RecordViolation(endpoint.Address);
                if (connection != null)
                {
                    await connection.DisposeAsync();
                    connection = null;
                }

                RecordFailure(reason, endpoint);
                return null;
            }
        }
        catch (Exception ex)
        {
            var reason = ClassifyFailure(ex);
            _logger.LogDebug(ex, "Failed to connect to {Endpoint} ({FailureReason})", OverlayLogSanitizer.Endpoint(endpoint), reason);
            RecordFailure(reason, endpoint);
            return null;
        }
        finally
        {
            Interlocked.Decrement(ref _pendingConnections);
        }
    }

    private async Task RunOutboundMessageLoopAsync(MeshOverlayConnection connection, CancellationToken cancellationToken)
    {
        var disconnectReason = "shutdown";

        try
        {
            while (!cancellationToken.IsCancellationRequested && connection.IsConnected)
            {
                if (connection.IsIdle())
                {
                    disconnectReason = "idle-timeout";
                    _logger.LogDebug("Outbound connection to {Username} idle, disconnecting", OverlayLogSanitizer.Username(connection.Username));
                    break;
                }

                try
                {
                    var rawMessage = await connection.ReadRawMessageAsync(cancellationToken).ConfigureAwait(false);
                    var messageType = SecureMessageFramer.ExtractMessageType(rawMessage);

                    switch (messageType)
                    {
                        case OverlayMessageType.Ping:
                            var ping = SecureMessageFramer.DeserializeMessage<PingMessage>(rawMessage);
                            await connection.WriteMessageAsync(new PongMessage { Timestamp = ping.Timestamp }, cancellationToken).ConfigureAwait(false);
                            break;

                        case OverlayMessageType.Pong:
                            break;

                        case OverlayMessageType.Disconnect:
                            disconnectReason = "peer-disconnect";
                            goto cleanup;

                        case OverlayMessageType.MeshSearchReq:
                            var meshSearchReq = SecureMessageFramer.DeserializeMessage<MeshSearchRequestMessage>(rawMessage);
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

                            var meshSearchResp = await _meshSearchRpcHandler.HandleAsync(meshSearchReq, cancellationToken).ConfigureAwait(false);
                            await connection.WriteMessageAsync(meshSearchResp, cancellationToken).ConfigureAwait(false);
                            break;

                        case OverlayMessageType.MeshSearchResp:
                            var meshSearchResponse = SecureMessageFramer.DeserializeMessage<MeshSearchResponseMessage>(rawMessage);
                            if (!_requestRouter.TryCompleteMeshSearchResponse(connection, meshSearchResponse))
                            {
                                _logger.LogDebug("Unexpected mesh_search_resp from {Username}, ignoring", OverlayLogSanitizer.Username(connection.Username));
                            }

                            break;

                        case OverlayMessageType.MeshServiceCall:
                            await HandleMeshServiceCallAsync(connection, rawMessage, cancellationToken).ConfigureAwait(false);
                            break;

                        case OverlayMessageType.MeshServiceReply:
                            var meshServiceReply = SecureMessageFramer.DeserializeMessage<MeshServiceReplyMessage>(rawMessage);
                            if (!_requestRouter.TryCompleteMeshServiceReply(connection, ToServiceReply(meshServiceReply)))
                            {
                                _logger.LogDebug("Unexpected mesh_service_reply from {Username}, ignoring", OverlayLogSanitizer.Username(connection.Username));
                            }

                            break;

                        default:
                            if (connection.Username is not null)
                            {
                                await HandleMeshMessageAsync(connection, rawMessage, messageType, cancellationToken).ConfigureAwait(false);
                            }

                            break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    disconnectReason = "cancellation";
                    break;
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (EndOfStreamException)
                {
                    disconnectReason = "remote-eof";
                    _logger.LogDebug("Outbound connection closed by {Username}", OverlayLogSanitizer.Username(connection.Username));
                    break;
                }
                catch (slskd.DhtRendezvous.Security.ProtocolViolationException ex)
                {
                    disconnectReason = "protocol-violation";
                    _logger.LogWarning("Protocol violation from {Username}: {Error}", OverlayLogSanitizer.Username(connection.Username), ex.Message);
                    _rateLimiter.RecordViolation(connection.RemoteAddress);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            disconnectReason = "message-loop-error";
            _logger.LogDebug(ex, "Error in outbound message loop for {Username}", OverlayLogSanitizer.Username(connection.Username));
        }

    cleanup:
        _logger.LogInformation(
            "Outbound mesh session ended for {Username}@{Endpoint}: ageSeconds={AgeSeconds} reason={Reason}",
            OverlayLogSanitizer.Username(connection.Username),
            OverlayLogSanitizer.Endpoint(connection.RemoteEndPoint),
            Math.Max(0, (int)(DateTimeOffset.UtcNow - connection.ConnectedAt).TotalSeconds),
            disconnectReason);
        await _registry.UnregisterAsync(connection).ConfigureAwait(false);
        _requestRouter.RemoveConnection(connection);
        _rateLimiter.RecordDisconnection(connection.RemoteAddress);
        _rateLimiter.RemoveConnection(connection.ConnectionId);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    private async Task HandleMeshMessageAsync(MeshOverlayConnection connection, byte[] rawMessage, string? messageType, CancellationToken cancellationToken)
    {
        try
        {
            Mesh.Messages.MeshMessage? meshMessage = messageType switch
            {
                "mesh_sync_hello" => SecureMessageFramer.DeserializeMessage<Mesh.Messages.MeshHelloMessage>(rawMessage),
                "mesh_req_delta" => SecureMessageFramer.DeserializeMessage<Mesh.Messages.MeshReqDeltaMessage>(rawMessage),
                "mesh_push_delta" => SecureMessageFramer.DeserializeMessage<Mesh.Messages.MeshPushDeltaMessage>(rawMessage),
                "mesh_req_key" => SecureMessageFramer.DeserializeMessage<Mesh.Messages.MeshReqKeyMessage>(rawMessage),
                "mesh_ack" => SecureMessageFramer.DeserializeMessage<Mesh.Messages.MeshAckMessage>(rawMessage),
                _ => null,
            };

            if (meshMessage == null)
            {
                _logger.LogDebug("Unknown message type {Type} from {Username}, ignoring", messageType, OverlayLogSanitizer.Username(connection.Username));
                return;
            }

            var response = await _meshSyncService.HandleMessageAsync(connection.Username!, meshMessage, cancellationToken).ConfigureAwait(false);
            if (response != null)
            {
                await connection.WriteMessageAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (slskd.DhtRendezvous.Security.ProtocolViolationException ex)
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
        var callMessage = SecureMessageFramer.DeserializeMessage<MeshServiceCallMessage>(rawMessage);
        if (_serviceRouter == null)
        {
            await connection.WriteMessageAsync(ToMeshServiceReplyMessage(new ServiceReply
            {
                CorrelationId = callMessage.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Mesh service router unavailable",
            }), cancellationToken).ConfigureAwait(false);
            return;
        }

        var call = new ServiceCall
        {
            CorrelationId = callMessage.CorrelationId,
            ServiceName = callMessage.ServiceName,
            Method = callMessage.Method,
            Payload = callMessage.Payload,
        };

        var reply = await _serviceRouter.RouteAsync(call, connection.Username ?? connection.ConnectionId, connection.CertificateThumbprint, cancellationToken).ConfigureAwait(false);
        await connection.WriteMessageAsync(ToMeshServiceReplyMessage(reply), cancellationToken).ConfigureAwait(false);
    }

    private static ServiceReply ToServiceReply(MeshServiceReplyMessage message)
    {
        return new ServiceReply
        {
            CorrelationId = message.CorrelationId,
            StatusCode = message.StatusCode,
            Payload = message.Payload,
            ErrorMessage = message.ErrorMessage,
        };
    }

    private static MeshServiceReplyMessage ToMeshServiceReplyMessage(ServiceReply reply)
    {
        return new MeshServiceReplyMessage
        {
            CorrelationId = reply.CorrelationId,
            StatusCode = reply.StatusCode,
            Payload = reply.Payload,
            ErrorMessage = reply.ErrorMessage,
        };
    }

    public MeshOverlayConnectorStats GetStats()
    {
        return new MeshOverlayConnectorStats
        {
            PendingConnections = _pendingConnections,
            SuccessfulConnections = _successfulConnections,
            FailedConnections = _failedConnections,
            FailureReasons = new OverlayConnectionFailureStats
            {
                ConnectTimeouts = _connectTimeoutFailures,
                NoRouteFailures = _noRouteFailures,
                ConnectionRefusedFailures = _connectionRefusedFailures,
                ConnectionResetFailures = _connectionResetFailures,
                TlsEofFailures = _tlsEofFailures,
                TlsHandshakeFailures = _tlsHandshakeFailures,
                ProtocolHandshakeFailures = _protocolHandshakeFailures,
                RegistrationFailures = _registrationFailures,
                BlockedPeerFailures = _blockedPeerFailures,
                UnknownFailures = _unknownFailures,
            },
            EndpointCooldownSkips = _endpointCooldownSkips,
            TopProblemEndpoints = _endpointAttemptStates.Values
                .Where(state => state.TotalFailures > 0)
                .OrderByDescending(state => state.ConsecutiveFailureCount)
                .ThenByDescending(state => state.LastFailureAt)
                .Take(5)
                .Select(state => new OverlayEndpointHealthStats
                {
                    Endpoint = OverlayLogSanitizer.Endpoint(state.Endpoint),
                    ConsecutiveFailureCount = state.ConsecutiveFailureCount,
                    TotalFailures = state.TotalFailures,
                    LastFailureReason = state.LastFailureReason.ToString(),
                    LastFailureAt = state.LastFailureAt,
                    SuppressedUntil = state.SuppressedUntil,
                    LastSuccessAt = state.LastSuccessAt,
                    LastUsername = state.LastUsername,
                })
                .ToList(),
        };
    }

    internal static OverlayConnectionFailureReason ClassifyFailure(Exception exception)
    {
        var exceptions = Flatten(exception).ToList();

        if (exceptions.OfType<slskd.DhtRendezvous.Security.ProtocolViolationException>().Any())
        {
            return OverlayConnectionFailureReason.ProtocolHandshake;
        }

        if (exceptions.OfType<SocketException>().Any(se =>
                se.SocketErrorCode is SocketError.NetworkUnreachable or SocketError.HostUnreachable))
        {
            return OverlayConnectionFailureReason.NoRoute;
        }

        if (exceptions.OfType<SocketException>().Any(se => se.SocketErrorCode == SocketError.ConnectionRefused))
        {
            return OverlayConnectionFailureReason.ConnectionRefused;
        }

        if (exceptions.OfType<SocketException>().Any(se => se.SocketErrorCode == SocketError.ConnectionReset))
        {
            return OverlayConnectionFailureReason.ConnectionReset;
        }

        if (exceptions.OfType<OperationCanceledException>().Any())
        {
            return OverlayConnectionFailureReason.ConnectTimeout;
        }

        if (exceptions.Any(IsTlsEofFailure))
        {
            return OverlayConnectionFailureReason.TlsEof;
        }

        if (exceptions.Any(ex => ex is AuthenticationException) ||
            exceptions.Any(ex => ex is IOException && ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase)))
        {
            return OverlayConnectionFailureReason.TlsHandshake;
        }

        return OverlayConnectionFailureReason.Unknown;
    }

    private static bool IsTlsEofFailure(Exception exception)
    {
        if (exception is EndOfStreamException)
        {
            return true;
        }

        if (exception is IOException or AuthenticationException)
        {
            var message = exception.Message ?? string.Empty;
            return message.Contains("unexpected eof", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("0 bytes from the transport stream", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("received an unexpected EOF", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("EOF", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        var queue = new Queue<Exception>();
        queue.Enqueue(exception);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    queue.Enqueue(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                queue.Enqueue(current.InnerException);
            }
        }
    }

    private void RecordFailure(OverlayConnectionFailureReason reason, IPEndPoint endpoint, string? username = null)
    {
        Interlocked.Increment(ref _failedConnections);
        var now = DateTimeOffset.UtcNow;
        var endpointState = _endpointAttemptStates.AddOrUpdate(
            GetEndpointKey(endpoint),
            _ => EndpointAttemptState.CreateFailure(endpoint, reason, username, now, GetFailureCooldown(1)),
            (_, existing) => existing.WithFailure(reason, username, now, GetFailureCooldown(existing.ConsecutiveFailureCount + 1)));

        switch (reason)
        {
            case OverlayConnectionFailureReason.ConnectTimeout:
                Interlocked.Increment(ref _connectTimeoutFailures);
                break;
            case OverlayConnectionFailureReason.NoRoute:
                Interlocked.Increment(ref _noRouteFailures);
                break;
            case OverlayConnectionFailureReason.ConnectionRefused:
                Interlocked.Increment(ref _connectionRefusedFailures);
                break;
            case OverlayConnectionFailureReason.ConnectionReset:
                Interlocked.Increment(ref _connectionResetFailures);
                break;
            case OverlayConnectionFailureReason.TlsEof:
                Interlocked.Increment(ref _tlsEofFailures);
                break;
            case OverlayConnectionFailureReason.TlsHandshake:
                Interlocked.Increment(ref _tlsHandshakeFailures);
                break;
            case OverlayConnectionFailureReason.ProtocolHandshake:
                Interlocked.Increment(ref _protocolHandshakeFailures);
                break;
            case OverlayConnectionFailureReason.RegistrationFailed:
                Interlocked.Increment(ref _registrationFailures);
                break;
            case OverlayConnectionFailureReason.BlockedPeer:
                Interlocked.Increment(ref _blockedPeerFailures);
                break;
            default:
                Interlocked.Increment(ref _unknownFailures);
                break;
        }

        if (endpointState.ConsecutiveFailureCount >= 3)
        {
            _logger.LogDebug(
                "Overlay endpoint {Endpoint} failure streak={FailureCount} lastReason={FailureReason} coolingDownUntil={SuppressedUntil}",
                OverlayLogSanitizer.Endpoint(endpoint),
                endpointState.ConsecutiveFailureCount,
                reason,
                endpointState.SuppressedUntil);
        }

        if (username is not null)
        {
            _logger.LogDebug("Recorded overlay failure {FailureReason} for {Username}@{Endpoint}", reason, OverlayLogSanitizer.Username(username), OverlayLogSanitizer.Endpoint(endpoint));
            return;
        }

        _logger.LogDebug("Recorded overlay failure {FailureReason} for {Endpoint}", reason, OverlayLogSanitizer.Endpoint(endpoint));
    }

    private void RecordSuccess(IPEndPoint endpoint, string? username)
    {
        var endpointKey = GetEndpointKey(endpoint);
        if (_endpointAttemptStates.TryGetValue(endpointKey, out var state))
        {
            _endpointAttemptStates[endpointKey] = state.WithSuccess(DateTimeOffset.UtcNow, username);
        }
    }

    internal static TimeSpan GetFailureCooldown(int consecutiveFailures)
    {
        if (consecutiveFailures <= 1)
        {
            return EndpointFailureBaseCooldown;
        }

        var multiplier = Math.Min(8, 1 << Math.Min(consecutiveFailures - 1, 3));
        var cooldown = TimeSpan.FromTicks(EndpointFailureBaseCooldown.Ticks * multiplier);
        return cooldown <= EndpointFailureMaxCooldown ? cooldown : EndpointFailureMaxCooldown;
    }

    internal static bool IsEndpointCoolingDown(DateTimeOffset now, DateTimeOffset? suppressedUntil)
    {
        return suppressedUntil.HasValue && suppressedUntil.Value > now;
    }

    private bool TryGetEndpointCooldown(IPEndPoint endpoint, out TimeSpan cooldownRemaining, out EndpointAttemptState? endpointState)
    {
        var now = DateTimeOffset.UtcNow;
        if (_endpointAttemptStates.TryGetValue(GetEndpointKey(endpoint), out endpointState) &&
            IsEndpointCoolingDown(now, endpointState.SuppressedUntil))
        {
            cooldownRemaining = endpointState.SuppressedUntil - now;
            return true;
        }

        cooldownRemaining = TimeSpan.Zero;
        endpointState = null;
        return false;
    }

    private static string GetEndpointKey(IPEndPoint endpoint)
    {
        return endpoint.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{endpoint.Address}]:{endpoint.Port}"
            : $"{endpoint.Address}:{endpoint.Port}";
    }

    private sealed class EndpointAttemptState
    {
        public required IPEndPoint Endpoint { get; init; }
        public required OverlayConnectionFailureReason LastFailureReason { get; init; }
        public required DateTimeOffset LastFailureAt { get; init; }
        public required DateTimeOffset SuppressedUntil { get; init; }
        public required int ConsecutiveFailureCount { get; init; }
        public required long TotalFailures { get; init; }
        public DateTimeOffset? LastSuccessAt { get; init; }
        public string? LastUsername { get; init; }

        public static EndpointAttemptState CreateFailure(
            IPEndPoint endpoint,
            OverlayConnectionFailureReason reason,
            string? username,
            DateTimeOffset now,
            TimeSpan cooldown)
        {
            return new EndpointAttemptState
            {
                Endpoint = endpoint,
                LastFailureReason = reason,
                LastFailureAt = now,
                SuppressedUntil = now.Add(cooldown),
                ConsecutiveFailureCount = 1,
                TotalFailures = 1,
                LastUsername = username,
            };
        }

        public EndpointAttemptState WithFailure(
            OverlayConnectionFailureReason reason,
            string? username,
            DateTimeOffset now,
            TimeSpan cooldown)
        {
            return new EndpointAttemptState
            {
                Endpoint = Endpoint,
                LastFailureReason = reason,
                LastFailureAt = now,
                SuppressedUntil = now.Add(cooldown),
                ConsecutiveFailureCount = ConsecutiveFailureCount + 1,
                TotalFailures = TotalFailures + 1,
                LastSuccessAt = LastSuccessAt,
                LastUsername = username ?? LastUsername,
            };
        }

        public EndpointAttemptState WithSuccess(DateTimeOffset now, string? username)
        {
            return new EndpointAttemptState
            {
                Endpoint = Endpoint,
                LastFailureReason = LastFailureReason,
                LastFailureAt = LastFailureAt,
                SuppressedUntil = now,
                ConsecutiveFailureCount = 0,
                TotalFailures = TotalFailures,
                LastSuccessAt = now,
                LastUsername = username ?? LastUsername,
            };
        }
    }

    private sealed class IPEndPointComparer : IEqualityComparer<IPEndPoint>
    {
        public static readonly IPEndPointComparer Instance = new();

        public bool Equals(IPEndPoint? x, IPEndPoint? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return Equals(x.Address, y.Address) && x.Port == y.Port;
        }

        public int GetHashCode(IPEndPoint obj)
        {
            return HashCode.Combine(obj.Address, obj.Port);
        }
    }
}
