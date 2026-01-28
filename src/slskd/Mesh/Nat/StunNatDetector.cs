// <copyright file="StunNatDetector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
    private static readonly TimeSpan DnsResolveTimeout = TimeSpan.FromSeconds(1);
    private readonly ILogger<StunNatDetector> logger;
    private readonly MeshOptions options;
    private NatType lastDetectedType = NatType.Unknown;

    public StunNatDetector(ILogger<StunNatDetector> logger, IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    /// <summary>
    /// Gets the last detected NAT type (for metrics).
    /// </summary>
    public NatType LastDetectedType => lastDetectedType;

    public async Task<NatType> DetectAsync(CancellationToken ct = default)
    {
        if (!options.EnableStun || options.StunServers.Count == 0)
        {
            logger.LogDebug("[NAT] STUN disabled or no servers configured");
            lastDetectedType = NatType.Unknown;
            return NatType.Unknown;
        }

        logger.LogDebug("[NAT] Starting STUN detection with servers: {Servers}", string.Join(", ", options.StunServers));

        // Strategy:
        // 1) Probe primary STUN server to get mapping1
        // 2) Probe same server with a DIFFERENT local port to get mapping2
        // 3) Probe a second server (if available) with a new port to get mapping3
        // Classification (given enum constraints: Direct, Restricted, Symmetric):
        //  - Direct: mapping == local endpoint
        //  - Symmetric: mapping changes when destination changes or when local port changes
        //  - Restricted (covers full/port-restricted cone): mapping stable across probes but differs from local

        var servers = options.StunServers.Take(2).ToArray();
        if (servers.Length == 0) return NatType.Unknown;

        try
        {
            logger.LogDebug("[NAT] Probing primary STUN server: {Server}", servers[0]);
            var mapping1 = await ProbeServer(servers[0], ct);
            if (mapping1 == null)
            {
                logger.LogDebug("[NAT] Primary server probe failed");
                lastDetectedType = NatType.Unknown;
                return NatType.Unknown;
            }

            logger.LogDebug("[NAT] Primary probe result: local={Local}, mapped={Mapped}, direct={Direct}",
                mapping1.LocalEndPoint, mapping1.MappedEndPoint, mapping1.IsDirect);

            if (mapping1.IsDirect)
            {
                logger.LogDebug("[NAT] Detected direct connection (no NAT)");
                lastDetectedType = NatType.Direct;
                return NatType.Direct;
            }

            // Second probe to same server with a new local socket
            logger.LogDebug("[NAT] Probing same server with new local port");
            var mapping2 = await ProbeServer(servers[0], ct, forceNewLocal: true);
            if (mapping2 == null)
            {
                logger.LogDebug("[NAT] Secondary probe failed");
                lastDetectedType = NatType.Unknown;
                return NatType.Unknown;
            }

            logger.LogDebug("[NAT] Secondary probe result: local={Local}, mapped={Mapped}",
                mapping2.LocalEndPoint, mapping2.MappedEndPoint);

            if (mapping1.MappedEndPoint.Address.ToString() != mapping2.MappedEndPoint.Address.ToString() ||
                mapping1.MappedEndPoint.Port != mapping2.MappedEndPoint.Port)
            {
                logger.LogDebug("[NAT] Detected symmetric NAT (mapping changes with local port)");
                lastDetectedType = NatType.Symmetric;
                return NatType.Symmetric;
            }

            // Optional third probe to a different server to detect destination-dependent mapping
            if (servers.Length > 1)
            {
                logger.LogDebug("[NAT] Probing secondary STUN server: {Server}", servers[1]);
                var mapping3 = await ProbeServer(servers[1], ct, forceNewLocal: true);
                if (mapping3 == null)
                {
                    logger.LogDebug("[NAT] Tertiary probe failed, falling back to restricted");
                    lastDetectedType = NatType.Restricted;
                    return NatType.Restricted; // fall back to restricted
                }

                logger.LogDebug("[NAT] Tertiary probe result: local={Local}, mapped={Mapped}",
                    mapping3.LocalEndPoint, mapping3.MappedEndPoint);

                if (mapping1.MappedEndPoint.Address.ToString() != mapping3.MappedEndPoint.Address.ToString() ||
                    mapping1.MappedEndPoint.Port != mapping3.MappedEndPoint.Port)
                {
                    logger.LogDebug("[NAT] Detected symmetric NAT (mapping changes with destination)");
                    lastDetectedType = NatType.Symmetric;
                    return NatType.Symmetric;
                }
            }

            // Mapping is stable but differs from local -> Restricted (covers full/port-restricted cone)
            logger.LogDebug("[NAT] Detected restricted NAT (stable mapping, differs from local)");
            lastDetectedType = NatType.Restricted;
            return NatType.Restricted;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[NAT] STUN detection failed");
            lastDetectedType = NatType.Unknown;
            return NatType.Unknown;
        }
    }

    private async Task<MappingResult?> ProbeServer(string server, CancellationToken ct, bool forceNewLocal = false)
    {
        var parts = server.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        {
            return null;
        }

        using var udp = forceNewLocal ? new UdpClient(0) : new UdpClient(); // allocate a new local port when requested
        udp.Client.ReceiveTimeout = 2000;
        udp.Client.SendTimeout = 2000;

        var resolvedAddress = await ResolveHostAsync(parts[0], ct);
        if (resolvedAddress == null)
        {
            logger.LogDebug("[NAT] DNS resolve timed out for {Host}", parts[0]);
            return null;
        }

        var endpoint = new IPEndPoint(resolvedAddress, port);
        var txn = RandomNumberGenerator.GetBytes(12);
        var request = BuildBindingRequest(txn);

        await udp.SendAsync(request, request.Length, endpoint);

        var receiveTask = udp.ReceiveAsync();
        if (await Task.WhenAny(receiveTask, Task.Delay(2000, ct)) != receiveTask)
        {
            return null;
        }

        var response = receiveTask.Result;
        var mapped = ParseMappedAddress(response.Buffer, txn);
        if (mapped == null)
        {
            return null;
        }

        var localEp = (IPEndPoint)udp.Client.LocalEndPoint!;
        var isDirect = mapped.Address.Equals(localEp.Address) && mapped.Port == localEp.Port;

        return new MappingResult(mapped, localEp, isDirect);
    }

    private async Task<IPAddress?> ResolveHostAsync(string host, CancellationToken ct)
    {
        try
        {
            var resolveTask = Dns.GetHostAddressesAsync(host);
            var completed = await Task.WhenAny(resolveTask, Task.Delay(DnsResolveTimeout, ct));
            if (completed != resolveTask)
            {
                return null;
            }

            var addresses = await resolveTask;
            return addresses.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[NAT] DNS resolve failed for {Host}", host);
            return null;
        }
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

    private sealed record MappingResult(IPEndPoint MappedEndPoint, IPEndPoint LocalEndPoint, bool IsDirect);
}
