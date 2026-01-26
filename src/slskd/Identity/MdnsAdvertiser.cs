// <copyright file="MdnsAdvertiser.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Raw mDNS advertiser using UDP sockets.</summary>
public sealed class MdnsAdvertiser : IDisposable
{
    private const int MdnsPort = 5353;
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.0.251");
    private static readonly IPEndPoint MulticastEndpoint = new(MulticastAddress, MdnsPort);

    private readonly ILogger<MdnsAdvertiser> _log;
    private UdpClient? _udpClient;
    private bool _disposed;
    private CancellationTokenSource? _announceCts;

    public MdnsAdvertiser(ILogger<MdnsAdvertiser> log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task StartAsync(string serviceName, string serviceType, ushort port, Dictionary<string, string> properties, CancellationToken ct = default)
    {
        if (_udpClient != null) return;

        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
            _udpClient.JoinMulticastGroup(MulticastAddress);

            var hostname = GetHostname();
            var instanceName = $"{serviceName}.{serviceType}.local";
            var hostnameLocal = $"{hostname}.local";

            _announceCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _announceCts.Token);

            // Send initial announcement
            await SendAnnouncementAsync(instanceName, serviceType, hostnameLocal, port, properties, linkedCts.Token).ConfigureAwait(false);

            // Send periodic announcements (mDNS requires this)
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), linkedCts.Token).ConfigureAwait(false);
                        await SendAnnouncementAsync(instanceName, serviceType, hostnameLocal, port, properties, linkedCts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[MdnsAdvertiser] Announcement loop error");
                }
            }, linkedCts.Token);

            _log.LogInformation("[MdnsAdvertiser] Started advertising {Instance} on {Hostname}:{Port}", instanceName, hostnameLocal, port);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[MdnsAdvertiser] Failed to start");
            Dispose();
            throw;
        }
    }

    public void Stop()
    {
        _announceCts?.Cancel();
        Dispose();
    }

    private async Task SendAnnouncementAsync(string instanceName, string serviceType, string hostname, ushort port, Dictionary<string, string> properties, CancellationToken ct)
    {
        if (_udpClient == null || _disposed) return;

        try
        {
            var packet = BuildDnsPacket(instanceName, serviceType, hostname, port, properties);
            await _udpClient.SendAsync(packet, packet.Length, MulticastEndpoint).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[MdnsAdvertiser] Failed to send announcement");
        }
    }

    private byte[] BuildDnsPacket(string instanceName, string serviceType, string hostname, ushort port, Dictionary<string, string> properties)
    {
        // DNS packet structure: Header (12 bytes) + Answer records
        var packet = new List<byte>();

        // DNS Header (all values in network byte order - big endian)
        var transactionId = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        packet.AddRange(ToNetworkBytes(transactionId)); // ID
        packet.AddRange(new byte[] { 0x84, 0x00 }); // Flags: QR=1 (response), AA=1 (authoritative)
        packet.AddRange(ToNetworkBytes((ushort)0)); // Questions = 0
        packet.AddRange(ToNetworkBytes((ushort)4)); // Answer RRs = 4
        packet.AddRange(ToNetworkBytes((ushort)0)); // Authority RRs = 0
        packet.AddRange(ToNetworkBytes((ushort)0)); // Additional RRs = 0

        // PTR record: _slskdn._tcp.local -> instance._slskdn._tcp.local
        var serviceTypeLocal = $"{serviceType}.local";
        EncodeName(serviceTypeLocal, packet);
        packet.AddRange(new byte[] { 0x00, 0x0C }); // Type = PTR (12)
        packet.AddRange(new byte[] { 0x00, 0x01 }); // Class = IN (1)
        packet.AddRange(ToNetworkBytes((uint)120)); // TTL = 120 seconds
        var ptrNameBytes = EncodeNameToBytes(instanceName);
        packet.AddRange(ToNetworkBytes((ushort)ptrNameBytes.Length)); // RDLength
        packet.AddRange(ptrNameBytes);

        // SRV record: instance._slskdn._tcp.local -> hostname.local:port
        EncodeName(instanceName, packet);
        packet.AddRange(new byte[] { 0x00, 0x21 }); // Type = SRV (33)
        packet.AddRange(new byte[] { 0x00, 0x01 }); // Class = IN
        packet.AddRange(ToNetworkBytes((uint)120)); // TTL
        var srvData = new List<byte>();
        srvData.AddRange(new byte[] { 0x00, 0x00 }); // Priority = 0
        srvData.AddRange(new byte[] { 0x00, 0x00 }); // Weight = 0
        srvData.AddRange(ToNetworkBytes(port)); // Port
        srvData.AddRange(EncodeNameToBytes(hostname));
        packet.AddRange(ToNetworkBytes((ushort)srvData.Count)); // RDLength
        packet.AddRange(srvData);

        // TXT record: instance._slskdn._tcp.local -> properties
        EncodeName(instanceName, packet);
        packet.AddRange(new byte[] { 0x00, 0x10 }); // Type = TXT (16)
        packet.AddRange(new byte[] { 0x00, 0x01 }); // Class = IN
        packet.AddRange(ToNetworkBytes((uint)120)); // TTL
        var txtData = new List<byte>();
        foreach (var kvp in properties)
        {
            var txtEntry = $"{kvp.Key}={kvp.Value}";
            var txtBytes = Encoding.UTF8.GetBytes(txtEntry);
            txtData.Add((byte)txtBytes.Length); // Length byte
            txtData.AddRange(txtBytes);
        }
        packet.AddRange(ToNetworkBytes((ushort)txtData.Count)); // RDLength
        packet.AddRange(txtData);

        // A record: hostname.local -> IP address
        EncodeName(hostname, packet);
        packet.AddRange(new byte[] { 0x00, 0x01 }); // Type = A (1)
        packet.AddRange(new byte[] { 0x00, 0x01 }); // Class = IN
        packet.AddRange(ToNetworkBytes((uint)120)); // TTL
        packet.AddRange(new byte[] { 0x00, 0x04 }); // RDLength = 4 (IPv4)
        var localIp = GetLocalIpAddress();
        packet.AddRange(localIp.GetAddressBytes());

        return packet.ToArray();
    }

    private static void EncodeName(string name, List<byte> packet)
    {
        var bytes = EncodeNameToBytes(name);
        packet.AddRange(bytes);
    }

    private static byte[] EncodeNameToBytes(string name)
    {
        var result = new List<byte>();
        var parts = name.Split('.');
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            var bytes = Encoding.UTF8.GetBytes(part);
            result.Add((byte)bytes.Length);
            result.AddRange(bytes);
        }
        result.Add(0); // Null terminator
        return result.ToArray();
    }

    private static byte[] ToNetworkBytes(ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    private static byte[] ToNetworkBytes(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    private static string GetHostname()
    {
        try
        {
            return Dns.GetHostName();
        }
        catch
        {
            return "slskd";
        }
    }

    private static IPAddress GetLocalIpAddress()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.GetIPProperties().UnicastAddresses.Any())
                .ToList();

            foreach (var ni in interfaces)
            {
                var addr = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address));
                if (addr != null) return addr.Address;
            }

            return IPAddress.Parse("127.0.0.1");
        }
        catch
        {
            return IPAddress.Parse("127.0.0.1");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _announceCts?.Cancel();
            _announceCts?.Dispose();
        }
        catch { }

        try
        {
            if (_udpClient != null)
            {
                try
                {
                    _udpClient.DropMulticastGroup(MulticastAddress);
                }
                catch { }
                _udpClient.Dispose();
            }
        }
        catch { }

        _udpClient = null;
    }
}
