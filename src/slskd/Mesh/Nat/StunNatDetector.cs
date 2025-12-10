using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh;

/// <summary>
/// STUN-based NAT detector (best-effort).
/// </summary>
public class StunNatDetector : INatDetector
{
    private const ushort StunBindingRequest = 0x0001;
    private const uint StunMagicCookie = 0x2112A442;
    private readonly ILogger<StunNatDetector> logger;
    private readonly MeshOptions options;

    public StunNatDetector(ILogger<StunNatDetector> logger, IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task<NatType> DetectAsync(CancellationToken ct = default)
    {
        if (!options.EnableStun || options.StunServers.Count == 0)
        {
            return NatType.Unknown;
        }

        foreach (var server in options.StunServers)
        {
            try
            {
                var nat = await ProbeServer(server, ct);
                if (nat != NatType.Unknown)
                {
                    return nat;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[NAT] STUN probe failed for {Server}", server);
            }
        }

        return NatType.Unknown;
    }

    private async Task<NatType> ProbeServer(string server, CancellationToken ct)
    {
        var parts = server.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        {
            return NatType.Unknown;
        }

        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 2000;
        udp.Client.SendTimeout = 2000;

        var endpoint = new IPEndPoint(Dns.GetHostAddresses(parts[0])[0], port);
        var txn = RandomNumberGenerator.GetBytes(12);
        var request = BuildBindingRequest(txn);

        await udp.SendAsync(request, request.Length, endpoint, ct);

        var receiveTask = udp.ReceiveAsync(ct);
        if (await Task.WhenAny(receiveTask, Task.Delay(2000, ct)) != receiveTask)
        {
            return NatType.Unknown;
        }

        var response = receiveTask.Result;
        var mapped = ParseMappedAddress(response.Buffer, txn);
        if (mapped == null)
        {
            return NatType.Unknown;
        }

        // Best-effort classification:
        // If mapped address differs from local endpoint, assume NAT (Restricted)
        // Otherwise Direct.
        var localEp = (IPEndPoint)udp.Client.LocalEndPoint!;
        if (!mapped.Address.Equals(localEp.Address) || mapped.Port != localEp.Port)
        {
            return NatType.Restricted;
        }

        return NatType.Direct;
    }

    private static byte[] BuildBindingRequest(byte[] txn)
    {
        var buf = new byte[20]; // type(2) + len(2) + cookie(4) + txn(12)
        WriteUInt16(buf, 0, StunBindingRequest);
        WriteUInt16(buf, 2, 0); // length
        WriteUInt32(buf, 4, StunMagicCookie);
        Array.Copy(txn, 0, buf, 8, 12);
        return buf;
    }

    private static IPEndPoint? ParseMappedAddress(byte[] buf, byte[] txn)
    {
        if (buf.Length < 20) return null;
        // Skip header
        int offset = 20;
        while (offset + 4 <= buf.Length)
        {
            var attrType = ReadUInt16(buf, offset);
            var attrLen = ReadUInt16(buf, offset + 2);
            offset += 4;
            if (offset + attrLen > buf.Length) break;

            if (attrType == 0x0020) // XOR-MAPPED-ADDRESS
            {
                // Family
                var family = buf[offset + 1];
                if (family == 0x01 && attrLen >= 8)
                {
                    ushort xport = (ushort)(ReadUInt16(buf, offset + 2) ^ (StunMagicCookie >> 16));
                    uint xaddr = ReadUInt32(buf, offset + 4) ^ StunMagicCookie;
                    var addrBytes = BitConverter.GetBytes(xaddr);
                    if (BitConverter.IsLittleEndian) Array.Reverse(addrBytes);
                    return new IPEndPoint(new IPAddress(addrBytes), xport);
                }
            }

            offset += attrLen;
        }

        return null;
    }

    private static void WriteUInt16(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)(value >> 8);
        buf[offset + 1] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)((value >> 24) & 0xFF);
        buf[offset + 1] = (byte)((value >> 16) & 0xFF);
        buf[offset + 2] = (byte)((value >> 8) & 0xFF);
        buf[offset + 3] = (byte)(value & 0xFF);
    }

    private static ushort ReadUInt16(byte[] buf, int offset) =>
        (ushort)((buf[offset] << 8) | buf[offset + 1]);

    private static uint ReadUInt32(byte[] buf, int offset) =>
        (uint)((buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3]);
}
