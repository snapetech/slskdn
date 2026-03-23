// <copyright file="PeerResolutionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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

        var normalizedPeerId = peerId.Trim();

        try
        {
            // Check in-memory mapping first
            lock (mappingsLock)
            {
                if (peerMappings.TryGetValue(normalizedPeerId, out var mapping) && !string.IsNullOrWhiteSpace(mapping.Username))
                {
                    logger.LogDebug("[PeerResolution] Found username mapping for peer {PeerId}: {Username}", normalizedPeerId, mapping.Username);
                    return mapping.Username.Trim();
                }
            }

            // Query DHT for peer metadata
            var dhtKey = $"{PeerMetadataPrefix}{normalizedPeerId}";
            var metadata = await dht.GetAsync<PeerMetadata>(dhtKey, ct);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Username))
            {
                var normalizedUsername = metadata.Username.Trim();
                var metadataPeerId = string.IsNullOrWhiteSpace(metadata.PeerId) ? normalizedPeerId : metadata.PeerId.Trim();
                var parsedEndpoint = metadata.Endpoint != null ? ParseEndpoint(metadata.Endpoint) : null;

                // Cache the mapping
                lock (mappingsLock)
                {
                    var mapping = new PeerMapping
                    {
                        PeerId = metadataPeerId,
                        Username = normalizedUsername,
                        Endpoint = parsedEndpoint
                    };
                    peerMappings[normalizedPeerId] = mapping;
                    peerMappings[metadataPeerId] = mapping;
                    peerMappings[normalizedUsername] = mapping;
                }

                logger.LogDebug("[PeerResolution] Resolved peer {PeerId} to username {Username} via DHT", normalizedPeerId, normalizedUsername);
                return normalizedUsername;
            }

            // Fallback: assume peer ID might be a username (for backward compatibility)
            logger.LogDebug("[PeerResolution] No mapping found for peer {PeerId}, using peer ID as username", normalizedPeerId);
            return normalizedPeerId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PeerResolution] Error resolving peer {PeerId} to username", normalizedPeerId);

            // Fallback to peer ID
            return normalizedPeerId;
        }
    }

    public async Task<IPEndPoint?> ResolvePeerIdToEndpointAsync(string peerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        var normalizedPeerId = peerId.Trim();

        try
        {
            // Check in-memory mapping first
            lock (mappingsLock)
            {
                if (peerMappings.TryGetValue(normalizedPeerId, out var mapping) && mapping.Endpoint != null)
                {
                    logger.LogDebug("[PeerResolution] Found endpoint mapping for peer {PeerId}: {Endpoint}", normalizedPeerId, mapping.Endpoint);
                    return mapping.Endpoint;
                }

                var aliasMapping = peerMappings.Values.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.Username) &&
                    string.Equals(m.Username, normalizedPeerId, StringComparison.OrdinalIgnoreCase) &&
                    m.Endpoint != null);
                if (aliasMapping?.Endpoint != null)
                {
                    logger.LogDebug("[PeerResolution] Resolved endpoint for alias {PeerId}: {Endpoint}", normalizedPeerId, aliasMapping.Endpoint);
                    return aliasMapping.Endpoint;
                }
            }

            // Query DHT for peer metadata
            var dhtKey = $"{PeerMetadataPrefix}{normalizedPeerId}";
            var metadata = await dht.GetAsync<PeerMetadata>(dhtKey, ct);

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Endpoint))
            {
                var endpoint = ParseEndpoint(metadata.Endpoint);
                if (endpoint != null)
                {
                    var metadataPeerId = string.IsNullOrWhiteSpace(metadata.PeerId) ? normalizedPeerId : metadata.PeerId.Trim();
                    var normalizedUsername = metadata.Username?.Trim();

                    // Cache the mapping
                    lock (mappingsLock)
                    {
                        if (!peerMappings.TryGetValue(normalizedPeerId, out var existing))
                        {
                            existing = new PeerMapping { PeerId = metadataPeerId };
                        }

                        existing.PeerId = metadataPeerId;
                        existing.Username = normalizedUsername;
                        existing.Endpoint = endpoint;
                        peerMappings[normalizedPeerId] = existing;
                        peerMappings[metadataPeerId] = existing;
                        if (!string.IsNullOrWhiteSpace(normalizedUsername))
                        {
                            peerMappings[normalizedUsername] = existing;
                        }
                    }

                    logger.LogDebug("[PeerResolution] Resolved peer {PeerId} to endpoint {Endpoint} via DHT", normalizedPeerId, endpoint);
                    return endpoint;
                }
            }

            logger.LogDebug("[PeerResolution] No endpoint found for peer {PeerId}", normalizedPeerId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PeerResolution] Error resolving peer {PeerId} to endpoint", normalizedPeerId);
            return null;
        }
    }

    public void RegisterPeerMapping(string peerId, string username, IPEndPoint? endpoint = null)
    {
        if (string.IsNullOrWhiteSpace(peerId) || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var normalizedPeerId = peerId.Trim();
        var normalizedUsername = username.Trim();

        lock (mappingsLock)
        {
            var mapping = new PeerMapping
            {
                PeerId = normalizedPeerId,
                Username = normalizedUsername,
                Endpoint = endpoint
            };
            peerMappings[normalizedPeerId] = mapping;
            peerMappings[normalizedUsername] = mapping;
        }

        logger.LogDebug("[PeerResolution] Registered mapping: peer {PeerId} -> username {Username}, endpoint {Endpoint}",
            normalizedPeerId, normalizedUsername, endpoint?.ToString() ?? "none");
    }

    private static IPEndPoint? ParseEndpoint(string endpointString)
    {
        if (string.IsNullOrWhiteSpace(endpointString))
        {
            return null;
        }

        try
        {
            // Support formats: "ip:port", "[ipv6]:port", "udp://ip:port", "tcp://ip:port"
            var normalized = endpointString.Trim();
            if (normalized.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["udp://".Length..];
            }
            else if (normalized.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["tcp://".Length..];
            }

            string hostPart;
            string portPart;
            if (normalized.StartsWith("[", StringComparison.Ordinal))
            {
                var closingBracketIndex = normalized.IndexOf(']');
                if (closingBracketIndex <= 1 ||
                    closingBracketIndex + 2 >= normalized.Length ||
                    normalized[closingBracketIndex + 1] != ':')
                {
                    return null;
                }

                hostPart = normalized[1..closingBracketIndex];
                portPart = normalized[(closingBracketIndex + 2)..];
            }
            else
            {
                var separatorIndex = normalized.LastIndexOf(':');
                if (separatorIndex <= 0 || separatorIndex == normalized.Length - 1)
                {
                    return null;
                }

                hostPart = normalized[..separatorIndex];
                portPart = normalized[(separatorIndex + 1)..];
            }

            if (IPAddress.TryParse(hostPart, out var ip) &&
                int.TryParse(portPart, out var port) &&
                port is > 0 and <= ushort.MaxValue)
            {
                return new IPEndPoint(ip, port);
            }

            if (int.TryParse(portPart, out port) && port is > 0 and <= ushort.MaxValue)
            {
                var resolved = Dns.GetHostAddresses(hostPart.Trim())
                    .FirstOrDefault(address => address.AddressFamily is System.Net.Sockets.AddressFamily.InterNetwork or System.Net.Sockets.AddressFamily.InterNetworkV6);
                if (resolved != null)
                {
                    return new IPEndPoint(resolved, port);
                }
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
