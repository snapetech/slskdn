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
        foreach (var epStr in peerEndpoints.Where(e => e.StartsWith("udp://", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryParseUdp(epStr, out var remoteEp))
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
        if (options.EnableMirrored && peerEndpoints.Any(e => e.StartsWith("relay://", StringComparison.OrdinalIgnoreCase)))
        {
            var relayEpStr = peerEndpoints.First(e => e.StartsWith("relay://", StringComparison.OrdinalIgnoreCase));
            if (TryParseRelay(relayEpStr, out var relayEp))
            {
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

    private static bool TryParseUdp(string endpoint, out IPEndPoint ep)
    {
        ep = new IPEndPoint(IPAddress.Any, 0);
        if (!endpoint.StartsWith("udp://", StringComparison.OrdinalIgnoreCase)) return false;
        var rest = endpoint.Substring("udp://".Length);
        var parts = rest.Split(':', 2);
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var ip)) return false;
        if (!int.TryParse(parts[1], out var port)) return false;
        ep = new IPEndPoint(ip, port);
        return true;
    }

    private static bool TryParseRelay(string endpoint, out IPEndPoint ep)
    {
        ep = new IPEndPoint(IPAddress.Any, 0);
        if (!endpoint.StartsWith("relay://", StringComparison.OrdinalIgnoreCase)) return false;
        var rest = endpoint.Substring("relay://".Length);
        var parts = rest.Split(':', 2);
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var ip)) return false;
        if (!int.TryParse(parts[1], out var port)) return false;
        ep = new IPEndPoint(ip, port);
        return true;
    }
}















