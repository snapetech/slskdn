// <copyright file="MeshOverlayConnector.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.DhtRendezvous.Security;

/// <summary>
/// Makes outbound overlay connections to mesh peers discovered via DHT.
/// </summary>
public sealed class MeshOverlayConnector : IMeshOverlayConnector
{
    private readonly ILogger<MeshOverlayConnector> _logger;
    private readonly IOptionsMonitor<slskd.Options> _optionsMonitor;
    private readonly CertificateManager _certificateManager;
    private readonly CertificatePinStore _pinStore;
    private readonly OverlayRateLimiter _rateLimiter;
    private readonly OverlayBlocklist _blocklist;
    private readonly MeshNeighborRegistry _registry;
    
    private int _pendingConnections;
    private long _successfulConnections;
    private long _failedConnections;
    
    /// <summary>
    /// Maximum concurrent connection attempts.
    /// </summary>
    public const int MaxConcurrentAttempts = 3;
    
    private string LocalUsername => _optionsMonitor.CurrentValue?.Soulseek?.Username ?? "unknown";
    
    public MeshOverlayConnector(
        ILogger<MeshOverlayConnector> logger,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        CertificateManager certificateManager,
        CertificatePinStore pinStore,
        OverlayRateLimiter rateLimiter,
        OverlayBlocklist blocklist,
        MeshNeighborRegistry registry)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _certificateManager = certificateManager;
        _pinStore = pinStore;
        _rateLimiter = rateLimiter;
        _blocklist = blocklist;
        _registry = registry;
    }
    
    public int PendingConnections => _pendingConnections;
    public long SuccessfulConnections => _successfulConnections;
    public long FailedConnections => _failedConnections;
    
    public async Task<int> ConnectToCandidatesAsync(
        IEnumerable<IPEndPoint> candidates,
        CancellationToken cancellationToken = default)
    {
        var successCount = 0;
        var shuffled = candidates.ToList();
        
        // SECURITY: Use cryptographic RNG for peer selection to prevent prediction attacks
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        
        foreach (var endpoint in shuffled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            
            // Stop if we have enough neighbors
            if (_registry.Count >= MeshNeighborRegistry.MaxNeighbors)
            {
                _logger.LogDebug("Registry at max capacity, stopping connection attempts");
                break;
            }
            
            // Skip if already connected
            if (_registry.IsConnectedTo(endpoint))
            {
                continue;
            }
            
            // Skip blocked endpoints
            if (_blocklist.IsBlocked(endpoint.Address))
            {
                continue;
            }
            
            var connection = await ConnectToEndpointAsync(endpoint, cancellationToken);
            if (connection is not null)
            {
                successCount++;
            }
        }
        
        return successCount;
    }
    
    public async Task<MeshOverlayConnection?> ConnectToEndpointAsync(
        IPEndPoint endpoint,
        CancellationToken cancellationToken = default)
    {
        // Check if already connected
        if (_registry.IsConnectedTo(endpoint))
        {
            _logger.LogDebug("Already connected to {Endpoint}", endpoint);
            return null;
        }
        
        // Check blocklist
        if (_blocklist.IsBlocked(endpoint.Address))
        {
            _logger.LogDebug("Endpoint {Endpoint} is blocked", endpoint);
            return null;
        }
        
        // Limit concurrent attempts
        if (_pendingConnections >= MaxConcurrentAttempts)
        {
            _logger.LogDebug("Too many pending connections, skipping {Endpoint}", endpoint);
            return null;
        }
        
        Interlocked.Increment(ref _pendingConnections);
        
        try
        {
            _logger.LogDebug("Connecting to mesh peer at {Endpoint}", endpoint);
            
            // Get our certificate
            var clientCert = _certificateManager.GetOrCreateServerCertificate();
            
            // Connect with TLS
            var connection = await MeshOverlayConnection.ConnectAsync(endpoint, clientCert, cancellationToken);
            
            try
            {
                // Perform handshake
                var ack = await connection.PerformClientHandshakeAsync(LocalUsername, cancellationToken: cancellationToken);
                
                // Check if username is blocked
                if (_blocklist.IsBlocked(ack.Username))
                {
                    _logger.LogWarning("Connected to blocked user {Username}, disconnecting", ack.Username);
                    await connection.DisconnectAsync("Blocked");
                    Interlocked.Increment(ref _failedConnections);
                    return null;
                }
                
                // Check certificate pin (TOFU)
                if (connection.CertificateThumbprint is not null)
                {
                    var pinResult = _pinStore.CheckPin(ack.Username, connection.CertificateThumbprint);
                    
                    switch (pinResult)
                    {
                        case PinCheckResult.NotPinned:
                            // SECURITY: Log at INFO level for TOFU visibility
                            _logger.LogInformation(
                                "TOFU: First connection to {Username}, pinning certificate {Thumbprint}",
                                ack.Username,
                                connection.CertificateThumbprint?[..16] + "...");
                            _pinStore.SetPin(ack.Username, connection.CertificateThumbprint);
                            break;
                        
                        case PinCheckResult.Valid:
                            _pinStore.TouchPin(ack.Username);
                            break;
                        
                        case PinCheckResult.Mismatch:
                            _logger.LogError(
                                "Certificate pin mismatch for {Username}! Possible MITM attack.",
                                ack.Username);
                            _blocklist.BlockUsername(ack.Username, "Certificate pin mismatch", TimeSpan.FromHours(1));
                            _rateLimiter.RecordViolation(endpoint.Address);
                            await connection.DisconnectAsync("Certificate mismatch");
                            Interlocked.Increment(ref _failedConnections);
                            return null;
                    }
                }
                
                // Register the connection
                if (!await _registry.RegisterAsync(connection))
                {
                    _logger.LogDebug("Failed to register connection to {Username}", ack.Username);
                    await connection.DisconnectAsync("Registration failed");
                    Interlocked.Increment(ref _failedConnections);
                    return null;
                }
                
                Interlocked.Increment(ref _successfulConnections);
                
                _logger.LogInformation(
                    "Connected to mesh peer {Username}@{Endpoint} (features: {Features})",
                    ack.Username,
                    endpoint,
                    string.Join(", ", (IEnumerable<string>?)ack.Features ?? Array.Empty<string>()));
                
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Handshake failed with {Endpoint}", endpoint);
                _rateLimiter.RecordViolation(endpoint.Address);
                await connection.DisposeAsync();
                Interlocked.Increment(ref _failedConnections);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to connect to {Endpoint}", endpoint);
            Interlocked.Increment(ref _failedConnections);
            return null;
        }
        finally
        {
            Interlocked.Decrement(ref _pendingConnections);
        }
    }
    
    public MeshOverlayConnectorStats GetStats()
    {
        return new MeshOverlayConnectorStats
        {
            PendingConnections = _pendingConnections,
            SuccessfulConnections = _successfulConnections,
            FailedConnections = _failedConnections,
        };
    }
}

