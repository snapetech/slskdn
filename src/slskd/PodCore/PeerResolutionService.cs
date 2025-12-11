namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// Resolves pod peer IDs to network endpoints (IPEndPoint) and usernames.
/// </summary>
public interface IPeerResolutionService
{
    /// <summary>
    /// Resolves a pod peer ID to a Soulseek username.
    /// </summary>
    Task<string?> ResolvePeerIdToUsernameAsync(string peerId, CancellationToken ct = default);

    /// <summary>
    /// Resolves a pod peer ID to an IPEndPoint for QUIC overlay routing.
    /// </summary>
    Task<IPEndPoint?> ResolvePeerIdToEndpointAsync(string peerId, CancellationToken ct = default);

    /// <summary>
    /// Registers a peer ID to username mapping.
    /// </summary>
    void RegisterPeerMapping(string peerId, string username, IPEndPoint? endpoint = null);
}

/// <summary>
/// Implements peer ID resolution using DHT and in-memory mappings.
/// </summary>
public class PeerResolutionService : IPeerResolutionService
{
    private readonly IMeshDhtClient dht;
    private readonly ILogger<PeerResolutionService> logger;
    private readonly Dictionary<string, PeerMapping> peerMappings = new(StringComparer.OrdinalIgnoreCase);
    private readonly object mappingsLock = new();
    private const string PeerMetadataPrefix = "peer:metadata:";

    public PeerResolutionService(
        IMeshDhtClient dht,
        ILogger<PeerResolutionService> logger)
    {
        this.dht = dht;
        this.logger = logger;
    }

    public async Task<string?> ResolvePeerIdToUsernameAsync(string peerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        try
        {
            // Check in-memory mapping first
            lock (mappingsLock)
            {
                if (peerMappings.TryGetValue(peerId, out var mapping) && !string.IsNullOrWhiteSpace(mapping.Username))
                {
                    logger.LogDebug("[PeerResolution] Found username mapping for peer {PeerId}: {Username}", peerId, mapping.Username);
                    return mapping.Username;
                }
            }

            // Query DHT for peer metadata
            var dhtKey = $"{PeerMetadataPrefix}{peerId}";
            var metadata = await dht.GetAsync<PeerMetadata>(dhtKey, ct);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Username))
            {
                // Cache the mapping
                lock (mappingsLock)
                {
                    peerMappings[peerId] = new PeerMapping
                    {
                        PeerId = peerId,
                        Username = metadata.Username,
                        Endpoint = metadata.Endpoint != null ? ParseEndpoint(metadata.Endpoint) : null
                    };
                }

                logger.LogDebug("[PeerResolution] Resolved peer {PeerId} to username {Username} via DHT", peerId, metadata.Username);
                return metadata.Username;
            }

            // Fallback: assume peer ID might be a username (for backward compatibility)
            logger.LogDebug("[PeerResolution] No mapping found for peer {PeerId}, using peer ID as username", peerId);
            return peerId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PeerResolution] Error resolving peer {PeerId} to username", peerId);
            // Fallback to peer ID
            return peerId;
        }
    }

    public async Task<IPEndPoint?> ResolvePeerIdToEndpointAsync(string peerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        try
        {
            // Check in-memory mapping first
            lock (mappingsLock)
            {
                if (peerMappings.TryGetValue(peerId, out var mapping) && mapping.Endpoint != null)
                {
                    logger.LogDebug("[PeerResolution] Found endpoint mapping for peer {PeerId}: {Endpoint}", peerId, mapping.Endpoint);
                    return mapping.Endpoint;
                }
            }

            // Query DHT for peer metadata
            var dhtKey = $"{PeerMetadataPrefix}{peerId}";
            var metadata = await dht.GetAsync<PeerMetadata>(dhtKey, ct);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Endpoint))
            {
                var endpoint = ParseEndpoint(metadata.Endpoint);
                if (endpoint != null)
                {
                    // Cache the mapping
                    lock (mappingsLock)
                    {
                        if (peerMappings.TryGetValue(peerId, out var existing))
                        {
                            existing.Endpoint = endpoint;
                        }
                        else
                        {
                            peerMappings[peerId] = new PeerMapping
                            {
                                PeerId = peerId,
                                Username = metadata.Username,
                                Endpoint = endpoint
                            };
                        }
                    }

                    logger.LogDebug("[PeerResolution] Resolved peer {PeerId} to endpoint {Endpoint} via DHT", peerId, endpoint);
                    return endpoint;
                }
            }

            logger.LogDebug("[PeerResolution] No endpoint found for peer {PeerId}", peerId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PeerResolution] Error resolving peer {PeerId} to endpoint", peerId);
            return null;
        }
    }

    public void RegisterPeerMapping(string peerId, string username, IPEndPoint? endpoint = null)
    {
        if (string.IsNullOrWhiteSpace(peerId) || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        lock (mappingsLock)
        {
            peerMappings[peerId] = new PeerMapping
            {
                PeerId = peerId,
                Username = username,
                Endpoint = endpoint
            };
        }

        logger.LogDebug("[PeerResolution] Registered mapping: peer {PeerId} -> username {Username}, endpoint {Endpoint}",
            peerId, username, endpoint?.ToString() ?? "none");
    }

    private static IPEndPoint? ParseEndpoint(string endpointString)
    {
        if (string.IsNullOrWhiteSpace(endpointString))
        {
            return null;
        }

        try
        {
            // Support formats: "ip:port", "udp://ip:port", "tcp://ip:port"
            var parts = endpointString.Replace("udp://", "").Replace("tcp://", "").Split(':');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var ip) && int.TryParse(parts[1], out var port))
            {
                return new IPEndPoint(ip, port);
            }
        }
        catch
        {
            // Invalid format
        }

        return null;
    }

    private class PeerMapping
    {
        public string PeerId { get; set; } = string.Empty;
        public string? Username { get; set; }
        public IPEndPoint? Endpoint { get; set; }
    }
}

/// <summary>
/// Peer metadata stored in DHT.
/// </summary>
public class PeerMetadata
{
    public string PeerId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Endpoint { get; set; } // Format: "ip:port" or "udp://ip:port"
    public long UpdatedAt { get; set; } // Unix timestamp in milliseconds
}

