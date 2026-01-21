// <copyright file="VirtualSoulfindMeshService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using slskd.VirtualSoulfind.ShadowIndex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Mesh service adapter for VirtualSoulfind / Shadow Index functionality.
/// Wraps existing IShadowIndexQuery for MBID-based music discovery.
/// </summary>
public class VirtualSoulfindMeshService : IMeshService
{
    private readonly ILogger<VirtualSoulfindMeshService> _logger;
    private readonly IShadowIndexQuery _shadowIndexQuery;

    public VirtualSoulfindMeshService(
        ILogger<VirtualSoulfindMeshService> logger,
        IShadowIndexQuery shadowIndexQuery)
    {
        _logger = logger;
        _shadowIndexQuery = shadowIndexQuery;
    }

    public string ServiceName => "shadow-index";

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "[VirtualSoulfind] Handling call: {Method} from {PeerId}",
                call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "QueryByMbid" => await HandleQueryByMbidAsync(call, context, cancellationToken),
                "QueryBatch" => await HandleQueryBatchAsync(call, context, cancellationToken),
                _ => new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.MethodNotFound,
                    ErrorMessage = $"Unknown method: {call.Method}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VirtualSoulfind] Error handling call: {Method}", call.Method);
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = "Internal error"
            };
        }
    }

    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming not implemented for shadow-index service");
    }

    private async Task<ServiceReply> HandleQueryByMbidAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<QueryByMbidRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.MBID))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "MBID is required"
            };
        }

        // Validate MBID format (basic check)
        if (!IsValidMbid(request.MBID))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid MBID format"
            };
        }

        var result = await _shadowIndexQuery.QueryAsync(request.MBID, cancellationToken);
        
        if (result == null)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = "No data found for MBID"
            };
        }

        // Privacy: Do NOT include raw peer IDs or Soulseek usernames
        // Return only aggregated counts and canonical variants
        var safeResult = new
        {
            MBID = result.MBID,
            PeerCount = result.TotalPeerCount,
            CanonicalVariants = result.CanonicalVariants?.Take(10).Select(v => (object)v).ToList() ?? new List<object>(),
            LastUpdated = result.LastUpdated
            // Explicitly NOT including: result.PeerIds (PII)
        };

        var response = JsonSerializer.Serialize(safeResult);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandleQueryBatchAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<QueryBatchRequest>(call.Payload);
        if (request == null || request.MBIDs == null || request.MBIDs.Count == 0)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "MBIDs list is required"
            };
        }

        // Rate limit: max 20 MBIDs per batch query
        if (request.MBIDs.Count > 20)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.PayloadTooLarge,
                ErrorMessage = "Too many MBIDs (max 20 per batch)"
            };
        }

        // Validate all MBIDs
        var invalidMbids = request.MBIDs.Where(m => !IsValidMbid(m)).ToList();
        if (invalidMbids.Any())
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = $"Invalid MBIDs: {string.Join(", ", invalidMbids.Take(3))}"
            };
        }

        var results = await _shadowIndexQuery.QueryBatchAsync(request.MBIDs, cancellationToken);
        
        // Privacy: Strip PII from all results
        var safeResults = results.ToDictionary(
            kvp => kvp.Key,
            kvp => new
            {
                MBID = kvp.Value.MBID,
                PeerCount = kvp.Value.TotalPeerCount,
                CanonicalVariants = kvp.Value.CanonicalVariants?.Take(10).Select(v => (object)v).ToList() ?? new List<object>(),
                LastUpdated = kvp.Value.LastUpdated
                // Explicitly NOT including: PeerIds (PII)
            });

        var response = JsonSerializer.Serialize(safeResults);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    /// <summary>
    /// Basic MBID format validation.
    /// MBIDs are typically UUIDs (36 chars with dashes) or other identifiers.
    /// </summary>
    private static bool IsValidMbid(string mbid)
    {
        if (string.IsNullOrWhiteSpace(mbid))
            return false;

        // Basic sanity checks
        if (mbid.Length < 8 || mbid.Length > 100)
            return false;

        // Check for obviously malicious content
        if (mbid.Contains("..") || mbid.Contains("/") || mbid.Contains("\\"))
            return false;

        return true;
    }
}

// Request DTOs
public record QueryByMbidRequest
{
    public string MBID { get; init; } = string.Empty;
}

public record QueryBatchRequest
{
    public List<string> MBIDs { get; init; } = new();
}
