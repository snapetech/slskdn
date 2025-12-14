// <copyright file="RelayClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Nat;

/// <summary>
/// Minimal relay client to fall back when hole punching fails (symmetric NAT).
/// </summary>
public interface IRelayClient
{
    Task<bool> RelayAsync(byte[] payload, IPEndPoint relayEp, CancellationToken ct = default);
}

public class RelayClient : IRelayClient
{
    private readonly ILogger<RelayClient> logger;

    public RelayClient(ILogger<RelayClient> logger)
    {
        this.logger = logger;
    }

    public async Task<bool> RelayAsync(byte[] payload, IPEndPoint relayEp, CancellationToken ct = default)
    {
        try
        {
            using var udp = new UdpClient();
            await udp.SendAsync(payload, payload.Length, relayEp);
            logger.LogDebug("[Relay] Sent payload size={Size} via relay {Relay}", payload.Length, relayEp);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Relay] Failed to send via relay {Relay}", relayEp);
            return false;
        }
    }
}
