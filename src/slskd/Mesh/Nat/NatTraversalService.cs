// <copyright file="NatTraversalService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Nat;

/// <summary>
/// Coordinates NAT traversal attempts (direct -> hole punching -> relay fallback).
/// </summary>
public interface INatTraversalService
{
    Task<NatTraversalResult> ConnectAsync(string peerId, List<string> peerEndpoints, CancellationToken ct = default);
}

public record NatTraversalResult(bool Success, string? ChosenEndpoint, bool UsedRelay, string Reason);

public class NatTraversalService : INatTraversalService
{
    private readonly ILogger<NatTraversalService> logger;
    private readonly IUdpHolePuncher holePuncher;
    private readonly IRelayClient relayClient;
    private readonly MeshOptions options;

    public NatTraversalService(
        ILogger<NatTraversalService> logger,
        IUdpHolePuncher holePuncher,
        IRelayClient relayClient,
        IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.holePuncher = holePuncher;
        this.relayClient = relayClient;
        this.options = options.Value;
    }

    public async Task<NatTraversalResult> ConnectAsync(string peerId, List<string> peerEndpoints, CancellationToken ct = default)
    {
        if (peerEndpoints == null || peerEndpoints.Count == 0)
        {
            return new NatTraversalResult(false, null, false, "no endpoints");
        }

        // Try direct UDP first
        foreach (var epStr in peerEndpoints
                     .Where(e => !string.IsNullOrWhiteSpace(e) && e.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var remoteEp = await ParseEndpointAsync(epStr, "udp://", ct);
            if (remoteEp != null)
            {
                var localEp = new IPEndPoint(IPAddress.Any, 0);
                var res = await holePuncher.TryPunchAsync(localEp, remoteEp, ct);
                if (res.Success)
                {
                    logger.LogDebug("[NAT] Hole punch success to {Peer} via {Endpoint}", peerId, epStr);
                    return new NatTraversalResult(true, epStr, false, "hole-punched");
                }
            }
        }

        // Relay fallback if allowed
        if (options.EnableMirrored)
        {
            foreach (var relayEpStr in peerEndpoints
                         .Where(e => !string.IsNullOrWhiteSpace(e) && e.StartsWith("relay://", StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var relayEp = await ParseEndpointAsync(relayEpStr, "relay://", ct);
                if (relayEp == null)
                {
                    continue;
                }

                // Send a small keepalive to open relay path
                var payload = new byte[] { 0x52, 0x45, 0x4c, 0x41, 0x59 }; // "RELAY"
                var ok = await relayClient.RelayAsync(payload, relayEp, ct);
                if (ok)
                {
                    logger.LogDebug("[NAT] Relay fallback to {Peer} via {Relay}", peerId, relayEpStr);
                    return new NatTraversalResult(true, relayEpStr, true, "relay");
                }
            }
        }

        return new NatTraversalResult(false, null, false, "all traversal attempts failed");
    }

    private static async Task<IPEndPoint?> ParseEndpointAsync(string endpoint, string schemePrefix, CancellationToken ct)
    {
        if (!endpoint.StartsWith(schemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = endpoint[schemePrefix.Length..];
        if (!TryParseHostAndPort(rest, out var host, out var port))
        {
            return null;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return new IPEndPoint(ip, port);
        }

        var resolved = await Dns.GetHostAddressesAsync(host, ct);
        var resolvedAddress = resolved
            .OrderBy(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 0 : 1)
            .FirstOrDefault();

        return resolvedAddress == null ? null : new IPEndPoint(resolvedAddress, port);
    }

    private static bool TryParseHostAndPort(string endpoint, out string host, out int port)
    {
        host = string.Empty;
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

            host = endpoint[1..closingBracketIndex];
            portPart = endpoint[(closingBracketIndex + 2)..];
        }
        else
        {
            var separatorIndex = endpoint.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == endpoint.Length - 1)
            {
                return false;
            }

            host = endpoint[..separatorIndex];
            portPart = endpoint[(separatorIndex + 1)..];
        }

        return int.TryParse(portPart, out port) && port is > 0 and <= ushort.MaxValue;
    }
}
