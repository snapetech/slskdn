using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Overlay;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Client for making service calls over the mesh overlay.
/// Handles request/response correlation and timeouts.
/// </summary>
public class MeshServiceClient : IMeshServiceClient
{
    private readonly ILogger<MeshServiceClient> _logger;
    private readonly IMeshServiceDirectory _serviceDirectory;
    private readonly IControlSigner _signer;
    
    // TODO: Inject actual overlay sender when available
    // private readonly IOverlaySender _overlaySender;
    
    // Pending calls: correlationId -> TaskCompletionSource
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ServiceReply>> _pendingCalls = new();
    
    // Configuration
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private readonly int _maxConcurrentCallsPerPeer = 10;
    
    public MeshServiceClient(
        ILogger<MeshServiceClient> logger,
        IMeshServiceDirectory serviceDirectory,
        IControlSigner signer)
    {
        _logger = logger;
        _serviceDirectory = serviceDirectory;
        _signer = signer;
    }

    public async Task<ServiceReply> CallAsync(
        string targetPeerId,
        ServiceCall call,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetPeerId))
            throw new ArgumentException("Target peer ID cannot be empty", nameof(targetPeerId));
        
        if (call == null)
            throw new ArgumentNullException(nameof(call));

        // Create task completion source for this call
        var tcs = new TaskCompletionSource<ServiceReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        if (!_pendingCalls.TryAdd(call.CorrelationId, tcs))
        {
            _logger.LogWarning(
                "[ServiceClient] Duplicate correlation ID: {CorrelationId}",
                call.CorrelationId);
            throw new InvalidOperationException("Duplicate correlation ID");
        }

        try
        {
            // Serialize the call
            var callBytes = MessagePackSerializer.Serialize(call);
            
            // Create control envelope
            var envelope = new ControlEnvelope
            {
                Type = OverlayControlTypes.ServiceCall, // NEW control type
                Payload = callBytes,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            // Sign envelope
            _signer.Sign(envelope);
            
            // TODO: Send envelope to target peer via overlay
            // await _overlaySender.SendAsync(targetPeerId, envelope, cancellationToken);
            
            _logger.LogDebug(
                "[ServiceClient] Sent call to {PeerId}: {Service}.{Method} (id: {CorrelationId})",
                targetPeerId, call.ServiceName, call.Method, call.CorrelationId);
            
            // Wait for reply with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_defaultTimeout);
            
            var reply = await tcs.Task.WaitAsync(cts.Token);
            
            _logger.LogDebug(
                "[ServiceClient] Received reply from {PeerId}: status={Status} (id: {CorrelationId})",
                targetPeerId, reply.StatusCode, call.CorrelationId);
            
            return reply;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "[ServiceClient] Call cancelled: {Service}.{Method}",
                call.ServiceName, call.Method);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[ServiceClient] Call timed out: {Service}.{Method} to {PeerId}",
                call.ServiceName, call.Method, targetPeerId);
            
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.Timeout,
                ErrorMessage = "Call timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ServiceClient] Error calling service: {Service}.{Method} to {PeerId}",
                call.ServiceName, call.Method, targetPeerId);
            
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = "Client error"
            };
        }
        finally
        {
            // Clean up pending call
            _pendingCalls.TryRemove(call.CorrelationId, out _);
        }
    }

    public async Task<ServiceReply> CallServiceAsync(
        string serviceName,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        // 1. Discover service
        var descriptors = await _serviceDirectory.FindByNameAsync(serviceName, cancellationToken);
        
        if (descriptors.Count == 0)
        {
            _logger.LogWarning(
                "[ServiceClient] No providers found for service: {ServiceName}",
                serviceName);
            
            return new ServiceReply
            {
                CorrelationId = Guid.NewGuid().ToString(),
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = $"No providers for '{serviceName}'"
            };
        }

        // 2. Pick first descriptor (TODO: Reputation-based selection)
        var descriptor = descriptors.First();
        
        _logger.LogDebug(
            "[ServiceClient] Selected provider for {ServiceName}: {PeerId}",
            serviceName, descriptor.OwnerPeerId);

        // 3. Create call
        var call = new ServiceCall
        {
            ServiceName = serviceName,
            Method = method,
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = payload.ToArray()
        };

        // 4. Make call
        return await CallAsync(descriptor.OwnerPeerId, call, cancellationToken);
    }

    /// <summary>
    /// Handle an incoming service reply (called by overlay dispatcher).
    /// </summary>
    public void HandleReply(ServiceReply reply)
    {
        if (reply == null)
        {
            _logger.LogWarning("[ServiceClient] Received null reply");
            return;
        }

        if (_pendingCalls.TryRemove(reply.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(reply);
        }
        else
        {
            _logger.LogDebug(
                "[ServiceClient] Received reply for unknown correlation ID: {CorrelationId}",
                reply.CorrelationId);
        }
    }
}
