// <copyright file="PrivateGatewayMeshService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using slskd.PodCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Mesh service for private VPN gateway functionality.
/// Provides secure TCP tunneling to pod-approved destinations.
/// </summary>
public class PrivateGatewayMeshService : IMeshService
{
    private readonly ILogger<PrivateGatewayMeshService> _logger;
    private readonly IPodService _podService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DnsSecurityService _dnsSecurity;

    // Active tunnels: tunnelId -> TunnelSession
    private readonly ConcurrentDictionary<string, TunnelSession> _activeTunnels = new();

    // Tunnel streams: tunnelId -> NetworkStream (for TCP connections)
    private readonly ConcurrentDictionary<string, NetworkStream> _tunnelStreams = new();

    // Incoming data buffers: tunnelId -> Queue<byte[]> (for TCP → client data)
    private readonly ConcurrentDictionary<string, ConcurrentQueue<byte[]>> _incomingDataBuffers = new();

    // DNS cache: hostname -> (resolved IPs, expiry time) for rebinding protection
    private readonly ConcurrentDictionary<string, (List<string> IPs, DateTimeOffset Expires)> _dnsCache = new();

    // Request nonce cache: (peerId, nonce) -> expiry time for replay protection
    private readonly ConcurrentDictionary<(string PeerId, string Nonce), DateTimeOffset> _nonceCache = new();

    public PrivateGatewayMeshService(
        ILogger<PrivateGatewayMeshService> logger,
        IPodService podService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _podService = podService;
        _serviceProvider = serviceProvider;
        _dnsSecurity = serviceProvider.GetRequiredService<DnsSecurityService>();

        // Start cleanup task
        _ = Task.Run(CleanupExpiredTunnelsAsync);
    }

    public string ServiceName => "private-gateway";

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "[PrivateGateway] Handling call: {Method} from {PeerId}",
                call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "OpenTunnel" => await HandleOpenTunnelAsync(call, context, cancellationToken),
                "TunnelData" => await HandleTunnelDataAsync(call, context, cancellationToken),
                "GetTunnelData" => await HandleGetTunnelDataAsync(call, context, cancellationToken),
                "CloseTunnel" => await HandleCloseTunnelAsync(call, context, cancellationToken),
                _ => new ServiceReply
                {
                    CorrelationId = call.CorrelationId,
                    StatusCode = ServiceStatusCodes.MethodNotFound,
                    ErrorMessage = $"Unknown method: {call.Method}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PrivateGateway] Error handling call: {Method}", call.Method);
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = "Internal error"
            };
        }
    }

    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        // For MVP, we'll use framed messages over calls rather than raw streaming
        // This can be upgraded to true streaming later for better performance
        throw new NotSupportedException("Streaming not yet implemented for private-gateway service. Use TunnelData calls instead.");
    }

    private async Task<ServiceReply> HandleOpenTunnelAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        // Input validation and sanitization
        var request = JsonSerializer.Deserialize<OpenTunnelRequest>(call.Payload);
        if (request == null)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid request payload"
            };
        }

        // Strict input validation
        var validationResult = ValidateOpenTunnelRequest(request);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "[PrivateGateway] Invalid OpenTunnel request from {PeerId}: {Error}",
                context.RemotePeerId, validationResult.Error);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = validationResult.Error
            };
        }

        // Request binding validation (replay protection)
        var bindingValidation = ValidateRequestBinding(request, context.RemotePeerId);
        if (!bindingValidation.IsValid)
        {
            _logger.LogWarning(
                "[PrivateGateway] Request binding validation failed for {PeerId}: {Error}",
                context.RemotePeerId, bindingValidation.Error);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = bindingValidation.Error
            };
        }

        // 1) Identity check (hard gate) - authenticated overlay session bound to clientPeerId
        // The MeshServiceContext already provides authenticated peer identity
        if (string.IsNullOrWhiteSpace(context.RemotePeerId) ||
            !PodValidation.IsValidPeerId(context.RemotePeerId))
        {
            _logger.LogWarning(
                "[PrivateGateway] Invalid peer identity in OpenTunnel request: {PeerId}",
                context.RemotePeerId ?? "null");

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = "Invalid peer identity"
            };
        }

        // 2) Pod membership gate - verify clientPeerId is member
        var pod = await _podService.GetPodAsync(request.PodId, cancellationToken);
        if (pod == null)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = "Pod not found"
            };
        }

        var members = await _podService.GetMembersAsync(request.PodId, cancellationToken);
        if (!members.Any(m => string.Equals(m.PeerId, context.RemotePeerId, StringComparison.Ordinal)))
        {
            _logger.LogWarning(
                "[PrivateGateway] Non-member {PeerId} attempted to open tunnel for pod {PodId}",
                context.RemotePeerId, request.PodId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = "Only pod members can open tunnels"
            };
        }

        // 3) Gateway peer verification - verify this node IS the gateway for this pod
        if (pod.PrivateServicePolicy == null ||
            string.IsNullOrWhiteSpace(pod.PrivateServicePolicy.GatewayPeerId))
        {
            _logger.LogWarning(
                "[PrivateGateway] Pod {PodId} has invalid gateway configuration",
                request.PodId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Pod gateway configuration is invalid"
            };
        }

        // For MVP, we'll assume this service runs on the gateway peer
        // In production, this would need to verify the local peer ID matches the gateway
        var localPeerId = "peer:mesh:self"; // TODO: Get from service context
        if (!string.Equals(pod.PrivateServicePolicy.GatewayPeerId, localPeerId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "[PrivateGateway] Tunnel request for pod {PodId} received on non-gateway peer {LocalPeerId} (gateway is {GatewayPeerId})",
                request.PodId, localPeerId, pod.PrivateServicePolicy.GatewayPeerId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Request must be sent to the designated gateway peer"
            };
        }

        // 4) Pod capability and member count verification
        if (!pod.Capabilities.Contains(PodCapability.PrivateServiceGateway) ||
            !pod.PrivateServicePolicy.Enabled)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Private gateway not enabled for this pod"
            };
        }

        // Verify member count is within limits
        var policy = pod.PrivateServicePolicy;
        if (members.Count() > policy.MaxMembers)
        {
            _logger.LogWarning(
                "[PrivateGateway] Pod {PodId} has {MemberCount} members but policy allows max {MaxMembers}",
                request.PodId, members.Count(), policy.MaxMembers);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Pod exceeds maximum member limit for private gateway"
            };
        }

        // 5) Destination allowlist gate - with registered service support
        AllowedDestination? allowedDestination = null;
        RegisteredService? registeredService = null;

        // First try registered services (preferred approach)
        if (!string.IsNullOrWhiteSpace(request.ServiceName))
        {
            registeredService = policy.RegisteredServices.FirstOrDefault(s =>
                s.Name.Equals(request.ServiceName, StringComparison.OrdinalIgnoreCase) &&
                s.Host.Equals(request.DestinationHost, StringComparison.OrdinalIgnoreCase) &&
                s.Port == request.DestinationPort);

            if (registeredService != null)
            {
                // Convert registered service to allowed destination for validation
                allowedDestination = new AllowedDestination
                {
                    HostPattern = registeredService.Host,
                    Port = registeredService.Port,
                    Protocol = registeredService.Protocol,
                    Kind = registeredService.Kind
                };
            }
        }

        // Fall back to legacy allowed destinations
        if (allowedDestination == null)
        {
            allowedDestination = policy.AllowedDestinations.FirstOrDefault(d =>
                MatchesDestination(d, request.DestinationHost, request.DestinationPort));
        }

        if (allowedDestination == null)
        {
            _logger.LogWarning(
                "[PrivateGateway] AUDIT: Tunnel rejected - Reason:DestinationNotAllowed, PeerId:{PeerId}, PodId:{PodId}, Host:{Host}, Port:{Port}",
                context.RemotePeerId, request.PodId, request.DestinationHost, request.DestinationPort);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = "Destination not allowed by pod policy"
            };
        }

        // DNS resolution and validation with rebinding protection
        var dnsResult = await _dnsSecurity.ResolveAndValidateAsync(
            request.DestinationHost,
            policy.AllowPrivateRanges,
            policy.AllowPublicDestinations,
            cancellationToken);

        if (!dnsResult.IsSuccess)
        {
            _logger.LogWarning(
                "[PrivateGateway] AUDIT: Tunnel rejected - Reason:DnsFailure, PeerId:{PeerId}, PodId:{PodId}, Host:{Host}, Error:{Error}",
                context.RemotePeerId, request.PodId, request.DestinationHost, dnsResult.ErrorMessage);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = dnsResult.ErrorMessage
            };
        }

        var resolvedIPs = dnsResult.AllowedIPs;

        // 7) Quotas/rate limits/timeouts validation
        var activeTunnelsForPeer = _activeTunnels.Values.Count(t =>
            t.ClientPeerId == context.RemotePeerId && t.IsActive);
        var activeTunnelsForPod = _activeTunnels.Values.Count(t =>
            t.PodId == request.PodId && t.IsActive);

        if (activeTunnelsForPeer >= policy.MaxConcurrentTunnelsPerPeer)
        {
            _logger.LogWarning(
                "[PrivateGateway] Peer {PeerId} exceeded concurrent tunnel limit ({Current}/{Max}) for pod {PodId}",
                context.RemotePeerId, activeTunnelsForPeer, policy.MaxConcurrentTunnelsPerPeer, request.PodId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = $"Too many active tunnels per peer (max {policy.MaxConcurrentTunnelsPerPeer})"
            };
        }

        if (activeTunnelsForPod >= policy.MaxConcurrentTunnelsPod)
        {
            _logger.LogWarning(
                "[PrivateGateway] Pod {PodId} exceeded concurrent tunnel limit ({Current}/{Max})",
                request.PodId, activeTunnelsForPod, policy.MaxConcurrentTunnelsPod);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = $"Too many active tunnels for pod (max {policy.MaxConcurrentTunnelsPod})"
            };
        }

        // Rate limiting: check recent tunnel creation attempts
        var recentTunnelsForPeer = _activeTunnels.Values.Count(t =>
            t.ClientPeerId == context.RemotePeerId &&
            t.CreatedAt > DateTimeOffset.UtcNow.AddSeconds(-60)); // Last minute

        if (recentTunnelsForPeer >= policy.MaxNewTunnelsPerMinutePerPeer)
        {
            _logger.LogWarning(
                "[PrivateGateway] Peer {PeerId} exceeded tunnel creation rate limit ({Current}/{Max} per minute) for pod {PodId}",
                context.RemotePeerId, recentTunnelsForPeer, policy.MaxNewTunnelsPerMinutePerPeer, request.PodId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = $"Tunnel creation rate limit exceeded (max {policy.MaxNewTunnelsPerMinutePerPeer} per minute)"
            };
        }

        // Generate tunnel ID and create session
        var tunnelId = Guid.NewGuid().ToString("N");
        var session = new TunnelSession
        {
            TunnelId = tunnelId,
            PodId = request.PodId,
            ClientPeerId = context.RemotePeerId,
            DestinationHost = request.DestinationHost,
            DestinationPort = request.DestinationPort,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivity = DateTimeOffset.UtcNow,
            IsActive = true
        };

        // Attempt to establish TCP connection
        try
        {
            var tcpClient = new TcpClient();
            var connectTimeout = policy.DialTimeout;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(connectTimeout);

            await tcpClient.ConnectAsync(request.DestinationHost, request.DestinationPort, cts.Token);

            var stream = tcpClient.GetStream();

            // Pin the resolved IPs for this tunnel (DNS rebinding protection)
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint as System.Net.IPEndPoint;
            var connectedIP = remoteEndPoint?.Address.ToString();
            if (connectedIP != null)
            {
                // Ensure the connected IP is in our allowed list
                if (!resolvedIPs.Contains(connectedIP) && !_dnsSecurity.ValidateTunnelIP(tunnelId, connectedIP))
                {
                    _logger.LogWarning(
                        "[PrivateGateway] AUDIT: Tunnel rejected - Reason:DnsRebinding, PeerId:{PeerId}, PodId:{PodId}, Host:{Host}, ConnectedIP:{ConnectedIP}, AllowedIPs:{AllowedIPs}",
                        context.RemotePeerId, request.PodId, request.DestinationHost, connectedIP, string.Join(", ", resolvedIPs));

                    tcpClient.Close();
                    return new ServiceReply
                    {
                        CorrelationId = call.CorrelationId,
                        StatusCode = ServiceStatusCodes.Forbidden,
                        ErrorMessage = "DNS rebinding detected - connected IP not in allowed list"
                    };
                }

                // Pin the resolved IPs for this hostname to prevent rebinding
                _dnsSecurity.PinTunnelIPs(tunnelId, request.DestinationHost, resolvedIPs);
            }

            // Store session and stream
            _activeTunnels[tunnelId] = session;
            _tunnelStreams[tunnelId] = stream;

            // Start forwarding task (bidirectional)
            _ = Task.Run(() => ForwardTunnelDataAsync(tunnelId, stream, cancellationToken), cancellationToken);

            _logger.LogInformation(
                "[PrivateGateway] AUDIT: Tunnel opened - TunnelId:{TunnelId}, PeerId:{PeerId}, PodId:{PodId}, Host:{Host}, Port:{Port}, Service:{ServiceName}",
                tunnelId, context.RemotePeerId, request.PodId, request.DestinationHost, request.DestinationPort,
                request.ServiceName ?? "direct");

            var response = JsonSerializer.Serialize(new OpenTunnelResponse
            {
                TunnelId = tunnelId,
                Accepted = true
            });

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Encoding.UTF8.GetBytes(response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PrivateGateway] Failed to connect to {Host}:{Port} for tunnel request",
                request.DestinationHost, request.DestinationPort);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = $"Failed to connect to destination: {ex.Message}"
            };
        }
    }

    private async Task<ServiceReply> HandleTunnelDataAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<TunnelDataRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.TunnelId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "TunnelId and data are required"
            };
        }

        // Validate tunnel exists and ownership
        if (!_activeTunnels.TryGetValue(request.TunnelId, out var session))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = "Tunnel not found"
            };
        }

        // Only client can send data
        if (session.ClientPeerId != context.RemotePeerId)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = "Only tunnel client can send data"
            };
        }

        // Check tunnel is still active
        if (!session.IsActive || !_tunnelStreams.TryGetValue(request.TunnelId, out var tcpStream))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Tunnel is not active"
            };
        }

        try
        {
            // Send data to TCP destination
            await tcpStream.WriteAsync(request.Data, cancellationToken);
            await tcpStream.FlushAsync(cancellationToken);

            // Update session stats
            session.BytesIn += request.Data.Length;
            session.LastActivity = DateTimeOffset.UtcNow;

            var response = JsonSerializer.Serialize(new { Sent = request.Data.Length });
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Encoding.UTF8.GetBytes(response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PrivateGateway] Error sending data for tunnel {TunnelId}", request.TunnelId);

            // Close tunnel on error
            await CloseTunnelAsync(request.TunnelId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = $"Tunnel error: {ex.Message}"
            };
        }
    }

    private async Task<ServiceReply> HandleGetTunnelDataAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<GetTunnelDataRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.TunnelId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "TunnelId is required"
            };
        }

        // Validate tunnel exists and ownership
        if (!_activeTunnels.TryGetValue(request.TunnelId, out var session))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = "Tunnel not found"
            };
        }

        // Only client can receive data
        if (session.ClientPeerId != context.RemotePeerId)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = "Only tunnel client can receive data"
            };
        }

        // Check tunnel is still active
        if (!session.IsActive || !_tunnelStreams.TryGetValue(request.TunnelId, out var tcpStream))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Tunnel is not active"
            };
        }

        try
        {
            // Check if we have buffered data for this tunnel
            if (_incomingDataBuffers.TryGetValue(request.TunnelId, out var dataBuffer))
            {
                if (dataBuffer.TryDequeue(out var data))
                {
                    // Update session stats
                    session.BytesOut += data.Length;
                    session.LastActivity = DateTimeOffset.UtcNow;

                    var response = JsonSerializer.Serialize(new TunnelDataResponse
                    {
                        Data = data,
                        BytesReceived = data.Length
                    });

                    return new ServiceReply
                    {
                        CorrelationId = call.CorrelationId,
                        StatusCode = ServiceStatusCodes.OK,
                        Payload = Encoding.UTF8.GetBytes(response)
                    };
                }
            }

            // No data available
            var noDataResponse = JsonSerializer.Serialize(new TunnelDataResponse
            {
                Data = Array.Empty<byte>(),
                BytesReceived = 0
            });

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.OK,
                Payload = Encoding.UTF8.GetBytes(noDataResponse)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PrivateGateway] Error reading data for tunnel {TunnelId}", request.TunnelId);

            // Close tunnel on error
            await CloseTunnelAsync(request.TunnelId);

            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = $"Tunnel error: {ex.Message}"
            };
        }
    }

    private async Task<ServiceReply> HandleCloseTunnelAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<CloseTunnelRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.TunnelId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "TunnelId is required"
            };
        }

        // Find and validate tunnel ownership
        if (!_activeTunnels.TryGetValue(request.TunnelId, out var session))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = "Tunnel not found"
            };
        }

        // Only client or gateway peer can close tunnel
        var pod = await _podService.GetPodAsync(session.PodId, cancellationToken);
        var isClient = session.ClientPeerId == context.RemotePeerId;
        var isGateway = pod?.PrivateServicePolicy?.GatewayPeerId == context.RemotePeerId;

        if (!isClient && !isGateway)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Forbidden,
                ErrorMessage = "Only tunnel client or gateway can close tunnel"
            };
        }

        // Close tunnel
        await CloseTunnelAsync(request.TunnelId);

        var response = JsonSerializer.Serialize(new { Closed = true });
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task ForwardTunnelDataAsync(string tunnelId, NetworkStream tcpStream, CancellationToken cancellationToken)
    {
        try
        {
            // Initialize data buffer for this tunnel
            var dataBuffer = new ConcurrentQueue<byte[]>();
            _incomingDataBuffers[tunnelId] = dataBuffer;

            var buffer = new byte[8192];

            while (!cancellationToken.IsCancellationRequested)
            {
                // Check if tunnel still exists
                if (!_activeTunnels.TryGetValue(tunnelId, out var session) || !session.IsActive)
                    break;

                try
                {
                    // Try to read data from TCP stream (with timeout)
                    if (tcpStream.DataAvailable)
                    {
                        var bytesRead = await tcpStream.ReadAsync(buffer, cancellationToken);
                        if (bytesRead > 0)
                        {
                            var data = new byte[bytesRead];
                            Array.Copy(buffer, 0, data, 0, bytesRead);

                            // Buffer data for client polling
                            dataBuffer.Enqueue(data);

                            // Update session stats
                            session.BytesOut += bytesRead;
                            session.LastActivity = DateTimeOffset.UtcNow;

                            _logger.LogDebug(
                                "[PrivateGateway] Buffered {Bytes} bytes for tunnel {TunnelId}",
                                bytesRead, tunnelId);
                        }
                    }
                    else
                    {
                        // Check for client-side timeout
                        var pod = await _podService.GetPodAsync(session.PodId);
                        var idleTimeout = pod?.PrivateServicePolicy?.IdleTimeout ?? TimeSpan.FromSeconds(120);

                        if (DateTimeOffset.UtcNow - session.LastActivity > idleTimeout)
                        {
                            _logger.LogInformation(
                                "[PrivateGateway] Closing idle tunnel {TunnelId} (no activity for {IdleTime})",
                                tunnelId, DateTimeOffset.UtcNow - session.LastActivity);
                            break;
                        }
                    }

                    // Small delay to prevent tight loop
                    await Task.Delay(50, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "[PrivateGateway] TCP read error for tunnel {TunnelId}", tunnelId);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PrivateGateway] Error in tunnel forwarding for {TunnelId}", tunnelId);
        }
        finally
        {
            await CloseTunnelAsync(tunnelId);
        }
    }

    private async Task CloseTunnelAsync(string tunnelId)
    {
        if (_tunnelStreams.TryRemove(tunnelId, out var stream))
        {
            try
            {
                stream.Close();
                await stream.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PrivateGateway] Error closing TCP stream for tunnel {TunnelId}", tunnelId);
            }
        }

        // Clean up data buffer
        _incomingDataBuffers.TryRemove(tunnelId, out _);

        if (_activeTunnels.TryRemove(tunnelId, out var session))
        {
            session.IsActive = false;
            _logger.LogInformation(
                "[PrivateGateway] AUDIT: Tunnel closed - TunnelId:{TunnelId}, PeerId:{PeerId}, PodId:{PodId}, Duration:{Duration}s, BytesIn:{BytesIn}, BytesOut:{BytesOut}",
                tunnelId, session.ClientPeerId, session.PodId,
                (DateTimeOffset.UtcNow - session.CreatedAt).TotalSeconds,
                session.BytesIn, session.BytesOut);
        }

        // Release IP pinning for this tunnel
        _dnsSecurity.ReleaseTunnelPin(tunnelId);
    }

    private async Task CleanupExpiredTunnelsAsync()
    {
        while (true)
        {
            try
            {
                var expiredTunnels = new List<string>();

                foreach (var (tunnelId, session) in _activeTunnels)
                {
                    // Get pod policy for timeout values
                    var pod = await _podService.GetPodAsync(session.PodId);
                    var maxLifetime = pod?.PrivateServicePolicy?.MaxLifetime ?? TimeSpan.FromMinutes(60);
                    var idleTimeout = pod?.PrivateServicePolicy?.IdleTimeout ?? TimeSpan.FromSeconds(120);

                    var now = DateTimeOffset.UtcNow;

                    if (now - session.CreatedAt > maxLifetime ||
                        now - session.LastActivity > idleTimeout)
                    {
                        expiredTunnels.Add(tunnelId);
                    }
                }

                foreach (var tunnelId in expiredTunnels)
                {
                    _logger.LogInformation("[PrivateGateway] Cleaning up expired tunnel {TunnelId}", tunnelId);
                    await CloseTunnelAsync(tunnelId);
                }

                // Clean up expired cache entries
                var expiredNonces = _nonceCache.Where(kvp => kvp.Value < DateTimeOffset.UtcNow)
                                               .Select(kvp => kvp.Key)
                                               .ToList();
                foreach (var nonceKey in expiredNonces)
                {
                    _nonceCache.TryRemove(nonceKey, out _);
                }

                var expiredDnsEntries = _dnsCache.Where(kvp => kvp.Value.Expires < DateTimeOffset.UtcNow)
                                                 .Select(kvp => kvp.Key)
                                                 .ToList();
                foreach (var hostname in expiredDnsEntries)
                {
                    _dnsCache.TryRemove(hostname, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrivateGateway] Error in cleanup task");
            }

            await Task.Delay(TimeSpan.FromMinutes(5)); // Cleanup every 5 minutes
        }
    }

    private bool MatchesDestination(AllowedDestination allowed, string host, int port)
    {
        // Check port match
        if (allowed.Port != port)
            return false;

        // Check host pattern
        if (allowed.HostPattern.Contains('*'))
        {
            // Simple wildcard matching
            var pattern = "^" + Regex.Escape(allowed.HostPattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(host, pattern, RegexOptions.IgnoreCase);
        }
        else
        {
            // Exact match or IP match
            return string.Equals(allowed.HostPattern, host, StringComparison.OrdinalIgnoreCase);
        }
    }

    private (bool IsValid, string Error) ValidateOpenTunnelRequest(OpenTunnelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PodId))
            return (false, "PodId is required");

        if (!PodValidation.IsValidPodId(request.PodId))
            return (false, "Invalid PodId format");

        if (string.IsNullOrWhiteSpace(request.DestinationHost))
            return (false, "DestinationHost is required");

        // Strict hostname validation
        if (request.DestinationHost.Length > 253) // Max FQDN length
            return (false, "DestinationHost is too long");

        if (!IsValidHostname(request.DestinationHost))
            return (false, "Invalid DestinationHost format");

        if (request.DestinationPort < 1 || request.DestinationPort > 65535)
            return (false, "DestinationPort must be between 1 and 65535");

        // Check for dangerous hostnames (localhost, reserved names)
        if (IsDangerousHostname(request.DestinationHost))
            return (false, "Destination hostname is not allowed");

        return (true, string.Empty);
    }

    private bool IsValidHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return false;

        // Allow IP addresses
        if (IPAddress.TryParse(hostname, out _))
            return true;

        // Hostname validation (simplified)
        var hostnamePattern = @"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$";
        return Regex.IsMatch(hostname, hostnamePattern) && hostname.Length <= 253;
    }

    private bool IsDangerousHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return false;

        var lowerHost = hostname.ToLowerInvariant();

        // Block localhost and loopback
        if (lowerHost == "localhost" || lowerHost.StartsWith("localhost."))
            return true;

        // Block reserved names that could be dangerous
        var dangerousNames = new[] { "broadcasthost", "local" };
        if (dangerousNames.Contains(lowerHost))
            return true;

        return false;
    }

    private (bool IsValid, string Error) ValidateRequestBinding(OpenTunnelRequest request, string peerId)
    {
        // Validate nonce format (should be cryptographically random)
        if (string.IsNullOrWhiteSpace(request.RequestNonce) || request.RequestNonce.Length < 16)
            return (false, "RequestNonce is required and must be at least 16 characters");

        // Check for replay attacks
        var nonceKey = (peerId, request.RequestNonce);
        if (_nonceCache.ContainsKey(nonceKey))
            return (false, "Request nonce has already been used");

        // Validate timestamp (within reasonable window, e.g., 5 minutes)
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestampAge = now - request.RequestTimestamp;
        const int MaxAgeSeconds = 300; // 5 minutes

        if (Math.Abs(timestampAge) > MaxAgeSeconds)
            return (false, "Request timestamp is too old or from the future");

        // Cache the nonce to prevent replay (expires after 10 minutes)
        _nonceCache[nonceKey] = DateTimeOffset.UtcNow.AddMinutes(10);

        return (true, string.Empty);
    }



    private bool IsBlockedAddress(string ipString)
    {
        try
        {
            if (!IPAddress.TryParse(ipString, out var ip))
                return false;

            // Always block cloud metadata services
            if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
            {
                var bytes = ip.GetAddressBytes();
                // AWS: 169.254.169.254
                if (bytes[0] == 169 && bytes[1] == 254 && bytes[2] == 169 && bytes[3] == 254)
                    return true;

                // Azure: 169.254.169.254 (same as AWS)
                // GCP: metadata.google.internal (but we check IPs, so block 169.254.169.254)
                // DigitalOcean: same
            }

            // Block link-local addresses that shouldn't be reachable externally
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal)
                return true;

            // Block multicast addresses
            if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
            {
                var bytes = ip.GetAddressBytes();
                if (bytes[0] >= 224 && bytes[0] <= 239) // 224.0.0.0/4
                    return true;
            }
        }
        catch
        {
            // If parsing fails, assume not blocked
        }

        return false;
    }
}

// Request/Response DTOs
public record OpenTunnelRequest
{
    public string PodId { get; init; } = string.Empty;
    public string DestinationHost { get; init; } = string.Empty;
    public int DestinationPort { get; init; }
    public string? ServiceName { get; init; } // For registered service lookup
    public string RequestNonce { get; init; } = string.Empty; // For replay protection
    public long RequestTimestamp { get; init; } // Unix timestamp for freshness
}

public record OpenTunnelResponse
{
    public string TunnelId { get; init; } = string.Empty;
    public bool Accepted { get; init; }
}

public record CloseTunnelRequest
{
    public string TunnelId { get; init; } = string.Empty;
}

public record TunnelDataRequest
{
    public string TunnelId { get; init; } = string.Empty;
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

public record GetTunnelDataRequest
{
    public string TunnelId { get; init; } = string.Empty;
}

public record TunnelDataResponse
{
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public int BytesReceived { get; init; }
}

// Note: This MVP implementation uses polling for data transfer.
// Full bidirectional streaming can be added later for better performance.
