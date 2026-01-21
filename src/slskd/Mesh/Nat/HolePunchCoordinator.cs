// <copyright file="HolePunchCoordinator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.Nat;

/// <summary>
/// Coordinates UDP hole punching between peers through the mesh overlay.
/// Handles the rendezvous protocol for NAT traversal.
/// </summary>
public interface IHolePunchCoordinator
{
    Task<HolePunchResult> RequestHolePunchAsync(
        string targetPeerId,
        string[] localEndpoints,
        CancellationToken cancellationToken = default);
}

public record HolePunchResult(
    bool Success,
    string? SessionId,
    string[]? RemoteEndpoints,
    string? ErrorMessage);

public class HolePunchCoordinator : IHolePunchCoordinator
{
    private readonly ILogger<HolePunchCoordinator> _logger;
    private readonly IMeshServiceClient _meshClient;
    private readonly INatDetector _natDetector;

    public HolePunchCoordinator(
        ILogger<HolePunchCoordinator> logger,
        IMeshServiceClient meshClient,
        INatDetector natDetector)
    {
        _logger = logger;
        _meshClient = meshClient;
        _natDetector = natDetector;
    }

    /// <summary>
    /// Request hole punching coordination with another peer.
    /// </summary>
    public async Task<HolePunchResult> RequestHolePunchAsync(
        string targetPeerId,
        string[] localEndpoints,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "[HolePunchCoord] Requesting hole punch with peer {TargetPeerId}",
                targetPeerId);

            var request = new HolePunchRequest
            {
                TargetPeerId = targetPeerId,
                LocalEndpoints = localEndpoints
            };

            var call = new ServiceCall
            {
                ServiceName = "hole-punch",
                Method = "RequestPunch",
                Payload = JsonSerializer.SerializeToUtf8Bytes(request)
            };

            // Call the hole punch service (could be on any node, but we'll use the target peer)
            var reply = await _meshClient.CallAsync(targetPeerId, call, cancellationToken);

            if (!reply.IsSuccess)
            {
                _logger.LogWarning(
                    "[HolePunchCoord] Hole punch request failed: {Error}",
                    reply.ErrorMessage);

                return new HolePunchResult(
                    false,
                    null,
                    null,
                    reply.ErrorMessage ?? "Unknown error");
            }

            var response = JsonSerializer.Deserialize<HolePunchResponse>(reply.Payload);

            if (response?.Status == HolePunchStatus.Initiated && !string.IsNullOrEmpty(response.SessionId))
            {
                _logger.LogInformation(
                    "[HolePunchCoord] Hole punch initiated with session {SessionId}",
                    response.SessionId);

                // Wait for the target peer to complete hole punching
                // In a real implementation, this might involve polling or events
                await Task.Delay(5000, cancellationToken); // Wait 5 seconds for completion

                return new HolePunchResult(
                    true,
                    response.SessionId,
                    response.TargetEndpoints,
                    response.Message);
            }
            else
            {
                return new HolePunchResult(
                    false,
                    response?.SessionId,
                    null,
                    response?.Message ?? "Hole punch initiation failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HolePunchCoord] Exception during hole punch request to {TargetPeerId}", targetPeerId);
            return new HolePunchResult(false, null, null, $"Exception: {ex.Message}");
        }
    }
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

