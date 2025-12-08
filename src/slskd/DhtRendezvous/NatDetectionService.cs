// <copyright file="NatDetectionService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mono.Nat;

/// <summary>
/// Service for detecting NAT type, public IP, and managing port mappings.
/// Uses UPnP/NAT-PMP via Mono.Nat and STUN for public IP detection.
/// </summary>
public sealed class NatDetectionService : IAsyncDisposable
{
    private readonly ILogger<NatDetectionService> _logger;
    private readonly DhtRendezvousOptions _options;
    private readonly ConcurrentBag<INatDevice> _discoveredDevices = new();
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);
    
    private IPAddress? _publicIp;
    private Mapping? _overlayPortMapping;
    private Mapping? _dhtPortMapping;
    private DateTimeOffset? _lastDiscoveryTime;
    private NatType _detectedNatType = NatType.Unknown;
    
    // STUN servers for public IP detection
    private static readonly string[] StunServers = new[]
    {
        "stun.l.google.com:19302",
        "stun1.l.google.com:19302",
        "stun.stunprotocol.org:3478",
        "stun.voip.blackberry.com:3478",
    };
    
    public NatDetectionService(
        ILogger<NatDetectionService> logger,
        DhtRendezvousOptions options)
    {
        _logger = logger;
        _options = options;
    }
    
    /// <summary>
    /// Detected NAT type.
    /// </summary>
    public NatType DetectedNatType => _detectedNatType;
    
    /// <summary>
    /// Detected public IP address.
    /// </summary>
    public IPAddress? PublicIp => _publicIp;
    
    /// <summary>
    /// Whether we have an active UPnP/NAT-PMP port mapping.
    /// </summary>
    public bool HasPortMapping => _overlayPortMapping is not null;
    
    /// <summary>
    /// Whether we appear to be beacon-capable (publicly reachable).
    /// </summary>
    public bool IsBeaconCapable => _detectedNatType == NatType.None || HasPortMapping;
    
    /// <summary>
    /// Number of discovered NAT devices.
    /// </summary>
    public int DiscoveredDeviceCount => _discoveredDevices.Count;
    
    /// <summary>
    /// Initialize NAT detection - discover devices and detect NAT type.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting NAT detection (UPnP={UpnpEnabled}, STUN={StunEnabled})...",
            _options.EnableUpnp, _options.EnableStun);
        
        var tasks = new List<Task>();
        
        // UPnP is opt-in due to security concerns
        if (_options.EnableUpnp)
        {
            _logger.LogWarning("UPnP is enabled. Note: UPnP has known security vulnerabilities. " +
                "Consider manual port forwarding or VPN for better security.");
            tasks.Add(DiscoverUpnpDevicesAsync(cancellationToken));
        }
        else
        {
            _logger.LogDebug("UPnP disabled (opt-in only for security reasons)");
        }
        
        // STUN is generally safe - just queries external servers for our public IP
        if (_options.EnableStun)
        {
            tasks.Add(DetectPublicIpViaStunAsync(cancellationToken));
        }
        
        // HTTP fallback for public IP (always safe)
        tasks.Add(DetectPublicIpViaHttpAsync(cancellationToken));
        
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
        
        // Determine NAT type based on results
        _detectedNatType = DetermineNatType();
        
        _logger.LogInformation(
            "NAT detection complete: Type={NatType}, PublicIP={PublicIp}, Devices={DeviceCount}, HasMapping={HasMapping}",
            _detectedNatType, _publicIp, _discoveredDevices.Count, HasPortMapping);
        
        _lastDiscoveryTime = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// Create port mappings for overlay and DHT ports.
    /// Requires EnableUpnp=true in options.
    /// </summary>
    public async Task<bool> CreatePortMappingsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableUpnp)
        {
            _logger.LogDebug("UPnP port mapping skipped (disabled in config)");
            return false;
        }
        
        if (_discoveredDevices.IsEmpty)
        {
            _logger.LogWarning("No NAT devices discovered, cannot create port mappings");
            return false;
        }
        
        var device = _discoveredDevices.First();
        
        try
        {
            // Create overlay port mapping
            var overlayMapping = new Mapping(Protocol.Tcp, _options.OverlayPort, _options.OverlayPort, 3600, "slskdn-overlay");
            _overlayPortMapping = await device.CreatePortMapAsync(overlayMapping);
            _logger.LogInformation("Created UPnP mapping for overlay port {Port}", _options.OverlayPort);
            
            // Create DHT port mapping if configured
            if (_options.DhtPort > 0)
            {
                var dhtMapping = new Mapping(Protocol.Udp, _options.DhtPort, _options.DhtPort, 3600, "slskdn-dht");
                _dhtPortMapping = await device.CreatePortMapAsync(dhtMapping);
                _logger.LogInformation("Created UPnP mapping for DHT port {Port}", _options.DhtPort);
            }
            
            // Update NAT type since we now have mappings
            _detectedNatType = NatType.UpnpMapped;
            
            return true;
        }
        catch (MappingException ex)
        {
            _logger.LogWarning(ex, "Failed to create UPnP port mapping: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating port mappings");
            return false;
        }
    }
    
    /// <summary>
    /// Remove port mappings on shutdown.
    /// </summary>
    public async Task RemovePortMappingsAsync(CancellationToken cancellationToken = default)
    {
        if (_discoveredDevices.IsEmpty)
        {
            return;
        }
        
        var device = _discoveredDevices.First();
        
        try
        {
            if (_overlayPortMapping is not null)
            {
                await device.DeletePortMapAsync(_overlayPortMapping);
                _logger.LogDebug("Removed UPnP mapping for overlay port");
                _overlayPortMapping = null;
            }
            
            if (_dhtPortMapping is not null)
            {
                await device.DeletePortMapAsync(_dhtPortMapping);
                _logger.LogDebug("Removed UPnP mapping for DHT port");
                _dhtPortMapping = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing port mappings");
        }
    }
    
    /// <summary>
    /// Get detection statistics.
    /// </summary>
    public NatDetectionStats GetStats()
    {
        return new NatDetectionStats
        {
            NatType = _detectedNatType,
            PublicIp = _publicIp?.ToString(),
            DiscoveredDeviceCount = _discoveredDevices.Count,
            HasOverlayMapping = _overlayPortMapping is not null,
            HasDhtMapping = _dhtPortMapping is not null,
            OverlayPort = _options.OverlayPort,
            DhtPort = _options.DhtPort,
            LastDiscoveryTime = _lastDiscoveryTime,
            IsBeaconCapable = IsBeaconCapable,
        };
    }
    
    private async Task DiscoverUpnpDevicesAsync(CancellationToken cancellationToken)
    {
        await _discoveryLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Starting UPnP/NAT-PMP device discovery...");
            
            var tcs = new TaskCompletionSource<bool>();
            var discoveryTimeout = TimeSpan.FromSeconds(5);
            
            void OnDeviceFound(object? sender, DeviceEventArgs e)
            {
                _logger.LogDebug("Discovered NAT device: {Protocol} at {Endpoint}",
                    e.Device.NatProtocol, e.Device.DeviceEndpoint);
                _discoveredDevices.Add(e.Device);
            }
            
            NatUtility.DeviceFound += OnDeviceFound;
            
            try
            {
                // Start discovery for both UPnP and NAT-PMP
                NatUtility.StartDiscovery(NatProtocol.Upnp, NatProtocol.Pmp);
                
                // Wait for discovery timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(discoveryTimeout);
                
                try
                {
                    await Task.Delay(discoveryTimeout, cts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Discovery timeout - expected
                }
            }
            finally
            {
                NatUtility.DeviceFound -= OnDeviceFound;
                NatUtility.StopDiscovery();
            }
            
            _logger.LogDebug("UPnP discovery complete, found {Count} devices", _discoveredDevices.Count);
            
            // Try to get public IP from first device
            if (!_discoveredDevices.IsEmpty)
            {
                try
                {
                    var device = _discoveredDevices.First();
                    var externalIp = await device.GetExternalIPAsync();
                    if (externalIp is not null && !IsPrivateIp(externalIp))
                    {
                        _publicIp = externalIp;
                        _logger.LogDebug("Got public IP from UPnP device: {IP}", externalIp);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not get external IP from NAT device");
                }
            }
        }
        finally
        {
            _discoveryLock.Release();
        }
    }
    
    private async Task DetectPublicIpViaStunAsync(CancellationToken cancellationToken)
    {
        foreach (var stunServer in StunServers)
        {
            try
            {
                var parts = stunServer.Split(':');
                var host = parts[0];
                var port = int.Parse(parts[1]);
                
                var ip = await StunQueryAsync(host, port, cancellationToken);
                if (ip is not null && !IsPrivateIp(ip))
                {
                    _publicIp ??= ip;
                    _logger.LogDebug("Got public IP from STUN ({Server}): {IP}", stunServer, ip);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "STUN query to {Server} failed", stunServer);
            }
        }
    }
    
    private async Task<IPAddress?> StunQueryAsync(string host, int port, CancellationToken cancellationToken)
    {
        // Simplified STUN binding request (RFC 5389)
        // This is a minimal implementation - production would use a proper STUN library
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 3000;
        
        // STUN binding request header (20 bytes)
        var request = new byte[20];
        request[0] = 0x00; // Binding Request
        request[1] = 0x01;
        // Message length = 0
        // Magic cookie
        request[4] = 0x21;
        request[5] = 0x12;
        request[6] = 0xA4;
        request[7] = 0x42;
        // Transaction ID (random)
        new Random().NextBytes(request.AsSpan(8, 12));
        
        var hostAddresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var endpoint = new IPEndPoint(hostAddresses.First(), port);
        
        await udp.SendAsync(request, request.Length, endpoint);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(3000);
        
        var result = await udp.ReceiveAsync(cts.Token);
        
        // Parse STUN response for XOR-MAPPED-ADDRESS attribute
        return ParseStunResponse(result.Buffer);
    }
    
    private IPAddress? ParseStunResponse(byte[] response)
    {
        if (response.Length < 20)
        {
            return null;
        }
        
        // Skip header, parse attributes
        var offset = 20;
        while (offset + 4 < response.Length)
        {
            var attrType = (response[offset] << 8) | response[offset + 1];
            var attrLen = (response[offset + 2] << 8) | response[offset + 3];
            offset += 4;
            
            // XOR-MAPPED-ADDRESS (0x0020) or MAPPED-ADDRESS (0x0001)
            if ((attrType == 0x0020 || attrType == 0x0001) && attrLen >= 8)
            {
                var family = response[offset + 1];
                if (family == 0x01) // IPv4
                {
                    var ipBytes = new byte[4];
                    Array.Copy(response, offset + 4, ipBytes, 0, 4);
                    
                    if (attrType == 0x0020) // XOR with magic cookie
                    {
                        ipBytes[0] ^= 0x21;
                        ipBytes[1] ^= 0x12;
                        ipBytes[2] ^= 0xA4;
                        ipBytes[3] ^= 0x42;
                    }
                    
                    return new IPAddress(ipBytes);
                }
            }
            
            offset += attrLen;
            // Pad to 4-byte boundary
            if (attrLen % 4 != 0)
            {
                offset += 4 - (attrLen % 4);
            }
        }
        
        return null;
    }
    
    private async Task DetectPublicIpViaHttpAsync(CancellationToken cancellationToken)
    {
        // Fallback: use HTTP API to get public IP
        var services = new[]
        {
            "https://api.ipify.org",
            "https://icanhazip.com",
            "https://checkip.amazonaws.com",
        };
        
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        
        foreach (var service in services)
        {
            try
            {
                var response = await http.GetStringAsync(service, cancellationToken);
                var ipStr = response.Trim();
                
                if (IPAddress.TryParse(ipStr, out var ip) && !IsPrivateIp(ip))
                {
                    _publicIp ??= ip;
                    _logger.LogDebug("Got public IP from HTTP ({Service}): {IP}", service, ip);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "HTTP IP detection via {Service} failed", service);
            }
        }
    }
    
    private NatType DetermineNatType()
    {
        // Check if we can bind directly (no NAT)
        if (CanBindToPublicIp())
        {
            return NatType.None;
        }
        
        // Check if we have UPnP mapping
        if (_overlayPortMapping is not null)
        {
            return NatType.UpnpMapped;
        }
        
        // Check if UPnP devices were found (can potentially map)
        if (!_discoveredDevices.IsEmpty)
        {
            return NatType.UpnpAvailable;
        }
        
        // We're behind NAT with no UPnP
        if (_publicIp is not null)
        {
            return NatType.Symmetric; // Assume worst case
        }
        
        return NatType.Unknown;
    }
    
    private bool CanBindToPublicIp()
    {
        if (_publicIp is null)
        {
            return false;
        }
        
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(_publicIp, 0));
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsPrivateIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false; // IPv6 - assume not private for simplicity
        }
        
        // 10.0.0.0/8
        if (bytes[0] == 10)
        {
            return true;
        }
        
        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }
        
        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }
        
        // 127.0.0.0/8
        if (bytes[0] == 127)
        {
            return true;
        }
        
        return false;
    }
    
    public async ValueTask DisposeAsync()
    {
        await RemovePortMappingsAsync();
        _discoveryLock.Dispose();
    }
}

/// <summary>
/// Detected NAT type.
/// </summary>
public enum NatType
{
    /// <summary>Unknown - detection not complete or failed.</summary>
    Unknown,
    
    /// <summary>No NAT - direct public IP.</summary>
    None,
    
    /// <summary>UPnP/NAT-PMP device found, mapping available.</summary>
    UpnpAvailable,
    
    /// <summary>UPnP port mapping active.</summary>
    UpnpMapped,
    
    /// <summary>NAT present but no UPnP - outbound only.</summary>
    Symmetric,
}

/// <summary>
/// NAT detection statistics.
/// </summary>
public sealed class NatDetectionStats
{
    public NatType NatType { get; init; }
    public string? PublicIp { get; init; }
    public int DiscoveredDeviceCount { get; init; }
    public bool HasOverlayMapping { get; init; }
    public bool HasDhtMapping { get; init; }
    public int OverlayPort { get; init; }
    public int DhtPort { get; init; }
    public DateTimeOffset? LastDiscoveryTime { get; init; }
    public bool IsBeaconCapable { get; init; }
}

