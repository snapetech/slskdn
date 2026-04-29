// <copyright file="ContentDirectory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using slskd.MediaCore;
using System.Linq;

namespace slskd.Mesh.Dht;

/// <summary>
/// DHT-backed content lookup.
/// </summary>
public class ContentDirectory : IMeshDirectory
{
    private readonly ILogger<ContentDirectory> logger;
    private readonly IMeshDhtClient dht;
    private readonly IDescriptorValidator validator;

    public ContentDirectory(
        ILogger<ContentDirectory> logger,
        IMeshDhtClient dht,
        IDescriptorValidator validator)
    {
        this.logger = logger;
        this.dht = dht;
        this.validator = validator;
    }

    public async Task<slskd.Mesh.MeshPeerDescriptor?> FindPeerByIdAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer:{peerId}";
        var dhtDescriptor = await dht.GetAsync<MeshPeerDescriptor>(key, ct);
        if (dhtDescriptor == null) return null;

        // Convert DHT descriptor to interface descriptor
        var endpoint = dhtDescriptor.Endpoints?.FirstOrDefault();
        var (address, port) = ParseEndpoint(endpoint);
        return new slskd.Mesh.MeshPeerDescriptor(
            string.IsNullOrWhiteSpace(dhtDescriptor.PeerId) ? peerId : dhtDescriptor.PeerId,
            address,
            port);
    }

    public async Task<IReadOnlyList<slskd.Mesh.MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default)
    {
        var key = $"mesh:content-peers:{contentId}";
        var hints = await dht.GetAsync<ContentPeerHints>(key, ct);
        if (hints?.Peers == null || hints.Peers.Count == 0) return Array.Empty<slskd.Mesh.MeshPeerDescriptor>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return hints.Peers
            .Where(p => !string.IsNullOrWhiteSpace(p.PeerId) && (p.TimestampUnixMs <= 0 || now - p.TimestampUnixMs < 3600_000))
            .GroupBy(p => p.PeerId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(peer => peer.TimestampUnixMs).First())
            .Select(p =>
            {
                var endpoint = p.Endpoints?.FirstOrDefault();
                var (address, port) = ParseEndpoint(endpoint);
                return new slskd.Mesh.MeshPeerDescriptor(p.PeerId, address, port);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<slskd.Mesh.MeshContentDescriptor>> FindContentByPeerAsync(string peerId, CancellationToken ct = default)
    {
        var key = $"mesh:peer-content:{peerId}";
        var contentList = await dht.GetAsync<List<string>>(key, ct);
        if (contentList == null || contentList.Count == 0) return Array.Empty<slskd.Mesh.MeshContentDescriptor>();

        var results = new List<slskd.Mesh.MeshContentDescriptor>();
        foreach (var cid in contentList.Where(cid => !string.IsNullOrWhiteSpace(cid)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var descriptor = await GetContentDescriptorAsync(cid, ct);
            if (descriptor != null)
            {
                results.Add(new slskd.Mesh.MeshContentDescriptor(cid, descriptor.Hashes?.FirstOrDefault()?.Hex, descriptor.SizeBytes ?? 0, descriptor.Codec));
            }
        }

        return results;
    }

    /// <summary>
    /// Get a content descriptor by ID (single value).
    /// </summary>
    public async Task<ContentDescriptor?> GetContentDescriptorAsync(string contentId, CancellationToken ct = default)
    {
        var key = $"mesh:content:{contentId}";
        var descriptor = await dht.GetAsync<ContentDescriptor>(key, ct);
        if (descriptor == null)
        {
            return null;
        }

        if (!validator.Validate(descriptor, out var reason))
        {
            logger.LogWarning("[MeshContent] Descriptor invalid for {ContentId}: {Reason}", contentId, reason);
            return null;
        }

        return descriptor;
    }

    /// <summary>
    /// Parse an endpoint string (e.g., "host:port") into address and port.
    /// </summary>
    private (string? Address, int? Port) ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint))
        {
            return (null, null);
        }

        var normalized = endpoint;
        var schemeSeparatorIndex = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex >= 0)
        {
            normalized = normalized[(schemeSeparatorIndex + 3)..];
        }

        if (!TryParseHostAndPort(normalized, out var address, out var port))
        {
            return (normalized, null);
        }

        return (address, port);
    }

    private static bool TryParseHostAndPort(string endpoint, out string address, out int port)
    {
        address = endpoint;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        string portPart;
        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = endpoint.IndexOf(']');
            if (closingBracketIndex <= 1 || closingBracketIndex >= endpoint.Length - 2 || endpoint[closingBracketIndex + 1] != ':')
            {
                return false;
            }

            address = endpoint[1..closingBracketIndex];
            portPart = endpoint[(closingBracketIndex + 2)..];
        }
        else
        {
            var separatorIndex = endpoint.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == endpoint.Length - 1)
            {
                return false;
            }

            address = endpoint[..separatorIndex];
            portPart = endpoint[(separatorIndex + 1)..];
        }

        return int.TryParse(portPart, out port) && port is > 0 and <= ushort.MaxValue;
    }
}
