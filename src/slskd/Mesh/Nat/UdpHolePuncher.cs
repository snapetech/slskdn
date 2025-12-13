using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Nat;

/// <summary>
/// Best-effort UDP hole punching helper.
/// </summary>
public interface IUdpHolePuncher
{
    Task<UdpHolePunchResult> TryPunchAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        CancellationToken ct = default);
}

public record UdpHolePunchResult(bool Success, IPEndPoint? LocalEndpoint);

public class UdpHolePuncher : IUdpHolePuncher
{
    private readonly ILogger<UdpHolePuncher> logger;

    public UdpHolePuncher(ILogger<UdpHolePuncher> logger)
    {
        this.logger = logger;
    }

    public async Task<UdpHolePunchResult> TryPunchAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        CancellationToken ct = default)
    {
        using var udp = new UdpClient(localEp);
        udp.Client.ReceiveTimeout = 2000;
        udp.Client.SendTimeout = 2000;

        var payload = new byte[] { 0x50, 0x55, 0x4e, 0x43 }; // "PUNC"

        try
        {
            // Send a few packets to open NAT mappings
            for (int i = 0; i < 3; i++)
            {
                await udp.SendAsync(payload, payload.Length, remoteEp);
            }

            // Listen briefly for a response
            var receiveTask = udp.ReceiveAsync();
            if (await Task.WhenAny(receiveTask, Task.Delay(2000, ct)) == receiveTask)
            {
                var res = receiveTask.Result;
                logger.LogDebug("[HolePunch] Received response from {Remote}", res.RemoteEndPoint);
            }

            var bound = (IPEndPoint)udp.Client.LocalEndPoint!;
            return new UdpHolePunchResult(true, bound);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[HolePunch] Failed for {Remote}", remoteEp);
            return new UdpHolePunchResult(false, null);
        }
    }
}















