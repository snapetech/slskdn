// <copyright file="VirtualSoulfindMeshService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.Transport;
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
    private readonly int _maxPayload;

    public VirtualSoulfindMeshService(
        ILogger<VirtualSoulfindMeshService> logger,
        IShadowIndexQuery shadowIndexQuery,
        IOptions<MeshOptions>? meshOptions = null)
    {
        _logger = logger;
        _shadowIndexQuery = shadowIndexQuery;
        _maxPayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
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
                    ErrorMessage = "Unknown method"
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

    public async Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        var requestPayload = await stream.ReceiveAsync(cancellationToken);
        if (requestPayload == null || requestPayload.Length == 0)
        {
            await stream.CloseAsync(cancellationToken);
            return;
        }

        var reply = await HandleQueryByMbidAsync(
            new ServiceCall
            {
                ServiceName = ServiceName,
                Method = "QueryByMbid",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = requestPayload,
            },
            context,
            cancellationToken);

        if (reply.StatusCode == ServiceStatusCodes.OK && reply.Payload.Length > 0)
        {
            await stream.SendAsync(reply.Payload, cancellationToken);
        }

        await stream.CloseAsync(cancellationToken);
    }

    private async Task<ServiceReply> HandleQueryByMbidAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var (request, err) = ServicePayloadParser.TryParseJson<QueryByMbidRequest>(call, _maxPayload);
        if (err != null) return err;
        var normalizedMbid = request?.MBID?.Trim();
        if (request == null || string.IsNullOrWhiteSpace(normalizedMbid))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "MBID is required"
            };
        }

        // Validate MBID format (basic check)
        if (!IsValidMbid(normalizedMbid))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid MBID format"
            };
        }

        var result = await _shadowIndexQuery.QueryAsync(normalizedMbid, cancellationToken);

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
        var (request, err) = ServicePayloadParser.TryParseJson<QueryBatchRequest>(call, _maxPayload);
        if (err != null) return err;
        var normalizedMbids = request?.MBIDs?
            .Where(static mbid => !string.IsNullOrWhiteSpace(mbid))
            .Select(static mbid => mbid.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (request == null || normalizedMbids == null || normalizedMbids.Count == 0)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "MBIDs list is required"
            };
        }

        // Rate limit: max 20 MBIDs per batch query
        if (normalizedMbids.Count > 20)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.PayloadTooLarge,
                ErrorMessage = "Too many MBIDs (max 20 per batch)"
            };
        }

        // Validate all MBIDs
        var invalidMbids = normalizedMbids.Where(m => !IsValidMbid(m)).ToList();
        if (invalidMbids.Any())
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid MBID list"
            };
        }

        var results = await _shadowIndexQuery.QueryBatchAsync(normalizedMbids, cancellationToken);

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
