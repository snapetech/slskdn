using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Mesh service for introspection and stats about the mesh itself.
/// Returns safe, privacy-respecting information about the local node.
/// </summary>
public class MeshIntrospectionService : IMeshService
{
    private readonly ILogger<MeshIntrospectionService> _logger;
    private readonly MeshServiceRouter _router;
    private readonly IMeshServiceDirectory _serviceDirectory;

    public MeshIntrospectionService(
        ILogger<MeshIntrospectionService> logger,
        MeshServiceRouter router,
        IMeshServiceDirectory serviceDirectory)
    {
        _logger = logger;
        _router = router;
        _serviceDirectory = serviceDirectory;
    }

    public string ServiceName => "mesh-introspect";

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "[MeshIntrospect] Handling call: {Method} from {PeerId}",
                call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "GetStatus" => HandleGetStatus(call, context),
                "GetCapabilities" => HandleGetCapabilities(call, context),
                "GetServices" => await HandleGetServicesAsync(call, context, cancellationToken),
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
            _logger.LogError(ex, "[MeshIntrospect] Error handling call: {Method}", call.Method);
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
        throw new NotSupportedException("Streaming not implemented for mesh-introspect service");
    }

    private ServiceReply HandleGetStatus(ServiceCall call, MeshServiceContext context)
    {
        var stats = _router.GetStats();
        
        // Get discovery metrics if directory supports it
        DiscoveryMetrics? discoveryMetrics = null;
        if (_serviceDirectory is DhtMeshServiceDirectory dhtDirectory)
        {
            discoveryMetrics = dhtDirectory.GetDiscoveryMetrics();
        }
        
        var status = new
        {
            Status = "healthy",
            UptimeSeconds = (int)(DateTimeOffset.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
            
            // Service stats
            RegisteredServices = stats.RegisteredServiceCount,
            TrackedPeers = stats.TrackedPeerCount,
            ActivePeersLastMinute = stats.ActivePeersLastMinute,
            
            // Circuit breaker stats
            CircuitBreakers = new
            {
                TotalTracked = stats.CircuitBreakers.Count,
                OpenCircuits = stats.OpenCircuitCount,
                Services = stats.CircuitBreakers
                    .Where(cb => cb.IsOpen || cb.ConsecutiveFailures > 0)
                    .Select(cb => new
                    {
                        cb.ServiceName,
                        cb.IsOpen,
                        cb.ConsecutiveFailures,
                        OpenedAt = cb.OpenedAt?.ToString("o")
                    })
                    .ToList()
            },
            
            // Work budget stats
            WorkBudget = new
            {
                Enabled = stats.WorkBudgetEnabled,
                ActivePeers = stats.WorkBudgetMetrics?.ActivePeersLastMinute ?? 0,
                TotalWorkConsumed = stats.WorkBudgetMetrics?.TotalWorkUnitsConsumedLastMinute ?? 0,
                PeersNearQuota = stats.WorkBudgetMetrics?.PeersNearQuota?.Count ?? 0
            },
            
            // Discovery abuse stats (if available)
            Discovery = discoveryMetrics != null ? new
            {
                ActivePeers = discoveryMetrics.ActivePeersLastMinute,
                TotalQueries = discoveryMetrics.TotalQueriesLastMinute,
                SuspiciousPeers = discoveryMetrics.SuspiciousPeers.Count
            } : null,
            
            // DO NOT expose: hostname, OS username, filesystem paths, public IP
        };

        var response = JsonSerializer.Serialize(status);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private ServiceReply HandleGetCapabilities(ServiceCall call, MeshServiceContext context)
    {
        var capabilities = new
        {
            Services = new[] { "pods", "mesh-introspect" },
            Protocols = new[] { "service-call", "service-reply" },
            Features = new[] { "service-fabric", "dht-discovery" }
        };

        var response = JsonSerializer.Serialize(capabilities);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandleGetServicesAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        // Return list of services available on this node
        // This is safe to expose as it only reveals service names, not content
        
        var serviceNames = new[] { "pods", "mesh-introspect" }; // TODO: Get from router
        
        var services = new System.Collections.Generic.List<object>();
        foreach (var serviceName in serviceNames)
        {
            var descriptors = await _serviceDirectory.FindByNameAsync(serviceName, cancellationToken);
            services.Add(new
            {
                ServiceName = serviceName,
                ProviderCount = descriptors.Count
            });
        }

        var response = JsonSerializer.Serialize(services);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }
}
