// <copyright file="HolePunchMeshService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.Nat;
using slskd.Mesh.ServiceFabric;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Mesh service for coordinating UDP hole punching between peers.
/// Enables NAT traversal for non-symmetric NAT types through rendezvous coordination.
/// </summary>
public class HolePunchMeshService : IMeshService
{
    private readonly ILogger<HolePunchMeshService> _logger;
    private readonly IUdpHolePuncher _holePuncher;
    private readonly IMeshServiceClient _meshClient;

    // Track active hole punch sessions
    private readonly ConcurrentDictionary<string, HolePunchSession> _activeSessions = new();

    public HolePunchMeshService(
        ILogger<HolePunchMeshService> logger,
        IUdpHolePuncher holePuncher,
        IMeshServiceClient meshClient)
    {
        _logger = logger;
        _holePuncher = holePuncher;
        _meshClient = meshClient;
    }

    public string ServiceName => "hole-punch";

    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming not implemented for hole punch service");
    }

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "[HolePunch] Handling call: {Method} from {PeerId}",
                call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "RequestPunch" => await HandleRequestPunchAsync(call, context, cancellationToken),
                "ConfirmPunch" => await HandleConfirmPunchAsync(call, context, cancellationToken),
                "CancelPunch" => await HandleCancelPunchAsync(call, context, cancellationToken),
                _ => new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.MethodNotFound,
                    ErrorMessage = $"Unknown hole punch method: {call.Method}",
                    Payload = Array.Empty<byte>()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HolePunch] Error handling call {Method}", call.Method);
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"Internal error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle a request to initiate hole punching with another peer.
    /// </summary>
    private async Task<ServiceReply> HandleRequestPunchAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<HolePunchRequest>(call.Payload);
            if (request?.TargetPeerId == null || request.LocalEndpoints == null || request.LocalEndpoints.Length == 0)
            {
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.InvalidPayload,
                    ErrorMessage = "Invalid HolePunchRequest: targetPeerId and localEndpoints required",
                    Payload = Array.Empty<byte>()
                };
            }

            var sessionId = Guid.NewGuid().ToString();
            var session = new HolePunchSession
            {
                SessionId = sessionId,
                InitiatorPeerId = context.RemotePeerId,
                TargetPeerId = request.TargetPeerId,
                InitiatorEndpoints = request.LocalEndpoints,
                Status = HolePunchStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            if (!_activeSessions.TryAdd(sessionId, session))
            {
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.UnknownError,
                    ErrorMessage = "Failed to create hole punch session",
                    Payload = Array.Empty<byte>()
                };
            }

            // Forward the request to the target peer
            var forwardRequest = new HolePunchForwardRequest
            {
                SessionId = sessionId,
                InitiatorPeerId = context.RemotePeerId,
                InitiatorEndpoints = request.LocalEndpoints
            };

            var forwardCall = new ServiceCall
            {
                ServiceName = "hole-punch",
                Method = "ConfirmPunch",
                Payload = JsonSerializer.SerializeToUtf8Bytes(forwardRequest)
            };

            try
            {
                var reply = await _meshClient.CallAsync(request.TargetPeerId, forwardCall, cancellationToken);

                if (reply.IsSuccess)
                {
                    var response = new HolePunchResponse
                    {
                        SessionId = sessionId,
                        Status = HolePunchStatus.Initiated,
                        Message = "Hole punch request forwarded to target peer"
                    };

                    var payload = JsonSerializer.SerializeToUtf8Bytes(response);

                    _logger.LogInformation(
                        "[HolePunch] Initiated hole punch session {SessionId} between {Initiator} and {Target}",
                        sessionId, context.RemotePeerId, request.TargetPeerId);

                    return new ServiceReply
                    {
                        CorrelationId = call.CorrelationId,
                        StatusCode = ServiceStatusCodes.OK,
                        Payload = payload
                    };
                }
                else
                {
                    _activeSessions.TryRemove(sessionId, out _);
                    return new ServiceReply
                    {
                        CorrelationId = call.CorrelationId,
                        StatusCode = ServiceStatusCodes.UnknownError,
                        ErrorMessage = $"Failed to contact target peer: {reply.ErrorMessage}",
                        Payload = Array.Empty<byte>()
                    };
                }
            }
            catch (Exception ex)
            {
                _activeSessions.TryRemove(sessionId, out _);
                _logger.LogWarning(ex, "[HolePunch] Failed to forward hole punch request to {Target}", request.TargetPeerId);
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.UnknownError,
                    ErrorMessage = $"Failed to contact target peer: {ex.Message}",
                    Payload = Array.Empty<byte>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HolePunch] Error in RequestPunch");
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"RequestPunch error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle confirmation from target peer to proceed with hole punching.
    /// </summary>
    private async Task<ServiceReply> HandleConfirmPunchAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<HolePunchForwardRequest>(call.Payload);
            if (request?.SessionId == null || request.InitiatorPeerId == null || request.InitiatorEndpoints == null)
            {
                return new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.InvalidPayload,
                    ErrorMessage = "Invalid HolePunchForwardRequest",
                    Payload = Array.Empty<byte>()
                };
            }

            // Update session with our endpoints
            if (!_activeSessions.TryGetValue(request.SessionId, out var session))
            {
                // Create session if it doesn't exist (we're the target)
                session = new HolePunchSession
                {
                    SessionId = request.SessionId,
                    InitiatorPeerId = request.InitiatorPeerId,
                    TargetPeerId = context.RemotePeerId,
                    InitiatorEndpoints = request.InitiatorEndpoints,
                    Status = HolePunchStatus.Confirming,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _activeSessions[request.SessionId] = session;
            }

            // Get our local endpoints for hole punching
            var ourEndpoints = GetOurEndpoints();
            session.TargetEndpoints = ourEndpoints;
            session.Status = HolePunchStatus.Ready;

            // Perform hole punching from our side
            await PerformHolePunchingAsync(session, cancellationToken);

            var response = new HolePunchResponse
            {
                SessionId = request.SessionId,
                Status = HolePunchStatus.Completed,
                TargetEndpoints = ourEndpoints,
                Message = "Hole punching completed"
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(response);

            _logger.LogInformation(
                "[HolePunch] Completed hole punch session {SessionId} for peers {Initiator} <-> {Target}",
                request.SessionId, request.InitiatorPeerId, context.RemotePeerId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = payload
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HolePunch] Error in ConfirmPunch");
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"ConfirmPunch error: {ex.Message}",
                Payload = Array.Empty<byte>()
            };
        }
    }

    /// <summary>
    /// Handle cancellation of a hole punch session.
    /// </summary>
    private Task<ServiceReply> HandleCancelPunchAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<HolePunchCancelRequest>(call.Payload);
            if (request?.SessionId != null && _activeSessions.TryRemove(request.SessionId, out _))
            {
                _logger.LogInformation(
                    "[HolePunch] Cancelled hole punch session {SessionId} by {PeerId}",
                    request.SessionId, context.RemotePeerId);
            }

            return Task.FromResult(new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Array.Empty<byte>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HolePunch] Error in CancelPunch");
            return Task.FromResult(new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = $"CancelPunch error: {ex.Message}",
                Payload = Array.Empty<byte>()
            });
        }
    }

    /// <summary>
    /// Perform the actual hole punching between peers.
    /// </summary>
    private async Task PerformHolePunchingAsync(HolePunchSession session, CancellationToken cancellationToken)
    {
        if (session.InitiatorEndpoints == null || session.TargetEndpoints == null)
        {
            _logger.LogWarning("[HolePunch] Missing endpoints for session {SessionId}", session.SessionId);
            return;
        }

        try
        {
            // Parse endpoints and attempt hole punching
            foreach (var initiatorEp in session.InitiatorEndpoints)
            {
                foreach (var targetEp in session.TargetEndpoints)
                {
                    if (IPEndPoint.TryParse(initiatorEp, out var initiatorEndpoint) &&
                        IPEndPoint.TryParse(targetEp, out var targetEndpoint))
                    {
                        _logger.LogDebug(
                            "[HolePunch] Attempting hole punch from {Local} to {Remote}",
                            initiatorEndpoint, targetEndpoint);

                        var result = await _holePuncher.TryPunchAsync(initiatorEndpoint, targetEndpoint, cancellationToken);

                        if (result.Success)
                        {
                            _logger.LogInformation(
                                "[HolePunch] Hole punch successful: {Local} <-> {Remote}",
                                result.LocalEndpoint, targetEndpoint);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "[HolePunch] Hole punch failed: {Local} -> {Remote}",
                                initiatorEndpoint, targetEndpoint);
                        }
                    }
                }
            }

            session.Status = HolePunchStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HolePunch] Error performing hole punching for session {SessionId}", session.SessionId);
            session.Status = HolePunchStatus.Failed;
        }
    }

    /// <summary>
    /// Get our local endpoints for hole punching.
    /// </summary>
    private string[] GetOurEndpoints()
    {
        // This is a simplified implementation - in practice you'd get actual listening endpoints
        // For now, return some default UDP ports
        return new[] { "0.0.0.0:2236", "[::]:2236" };
    }
}

/// <summary>
/// Hole punch session state.
/// </summary>
public class HolePunchSession
{
    public string SessionId { get; set; } = string.Empty;
    public string InitiatorPeerId { get; set; } = string.Empty;
    public string TargetPeerId { get; set; } = string.Empty;
    public string[]? InitiatorEndpoints { get; set; }
    public string[]? TargetEndpoints { get; set; }
    public HolePunchStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Hole punch status enumeration.
/// </summary>
public enum HolePunchStatus
{
    Pending,
    Initiated,
    Confirming,
    Ready,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Request DTO for hole punch initiation.
/// </summary>
public record HolePunchRequest
{
    public required string TargetPeerId { get; init; }
    public required string[] LocalEndpoints { get; init; }
}

/// <summary>
/// Forwarded request DTO for hole punch confirmation.
/// </summary>
public record HolePunchForwardRequest
{
    public required string SessionId { get; init; }
    public required string InitiatorPeerId { get; init; }
    public required string[] InitiatorEndpoints { get; init; }
}

/// <summary>
/// Response DTO for hole punch operations.
/// </summary>
public record HolePunchResponse
{
    public required string SessionId { get; init; }
    public required HolePunchStatus Status { get; init; }
    public string[]? TargetEndpoints { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Request DTO for hole punch cancellation.
/// </summary>
public record HolePunchCancelRequest
{
    public required string SessionId { get; init; }
}
