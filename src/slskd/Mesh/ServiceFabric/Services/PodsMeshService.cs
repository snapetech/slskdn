using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.ServiceFabric;
using slskd.PodCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Mesh service adapter for Pod/Chat functionality.
/// Wraps existing IPodService and IPodMessaging.
/// </summary>
public class PodsMeshService : IMeshService
{
    private readonly ILogger<PodsMeshService> _logger;
    private readonly IPodService _podService;
    private readonly IPodMessaging _podMessaging;

    public PodsMeshService(
        ILogger<PodsMeshService> logger,
        IPodService podService,
        IPodMessaging podMessaging)
    {
        _logger = logger;
        _podService = podService;
        _podMessaging = podMessaging;
    }

    public string ServiceName => "pods";

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "[PodsMeshService] Handling call: {Method} from {PeerId}",
                call.Method, context.RemotePeerId);

            return call.Method switch
            {
                "List" => await HandleListAsync(call, context, cancellationToken),
                "Get" => await HandleGetAsync(call, context, cancellationToken),
                "Join" => await HandleJoinAsync(call, context, cancellationToken),
                "Leave" => await HandleLeaveAsync(call, context, cancellationToken),
                "PostMessage" => await HandlePostMessageAsync(call, context, cancellationToken),
                "GetMessages" => await HandleGetMessagesAsync(call, context, cancellationToken),
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
            _logger.LogError(ex, "[PodsMeshService] Error handling call: {Method}", call.Method);
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
        throw new NotSupportedException("Streaming not yet implemented for pods service");
    }

    private async Task<ServiceReply> HandleListAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var pods = await _podService.ListAsync(cancellationToken);
        
        // Only return public/listed pods to external callers
        var publicPods = pods.Where(p => p.Visibility == PodVisibility.Listed).ToArray();
        
        var response = JsonSerializer.Serialize(publicPods);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandleGetAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<GetPodRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.PodId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "PodId is required"
            };
        }

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

        var response = JsonSerializer.Serialize(pod);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandleJoinAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<JoinPodRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.PodId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "PodId is required"
            };
        }

        var member = new PodMember
        {
            PeerId = context.RemotePeerId,
            PublicKey = context.RemotePublicKey ?? string.Empty,
            Role = request.Role ?? "member"
        };

        var success = await _podService.JoinAsync(request.PodId, member, cancellationToken);
        
        var response = JsonSerializer.Serialize(new { Success = success });
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = success ? ServiceStatusCodes.OK : ServiceStatusCodes.UnknownError,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandleLeaveAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<LeavePodRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.PodId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "PodId is required"
            };
        }

        var success = await _podService.LeaveAsync(request.PodId, context.RemotePeerId, cancellationToken);
        
        var response = JsonSerializer.Serialize(new { Success = success });
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = success ? ServiceStatusCodes.OK : ServiceStatusCodes.UnknownError,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandlePostMessageAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<PostMessageRequest>(call.Payload);
        if (request == null)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid request"
            };
        }

        // TODO: Validate message size
        if (request.Body?.Length > 4096)
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.PayloadTooLarge,
                ErrorMessage = "Message too large (max 4KB)"
            };
        }

        var message = new PodMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ChannelId = request.ChannelId,
            SenderPeerId = context.RemotePeerId,
            Body = request.Body,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = request.Signature ?? string.Empty
        };

        var success = await _podMessaging.SendAsync(message, cancellationToken);
        
        var response = JsonSerializer.Serialize(new { Success = success, MessageId = message.MessageId });
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = success ? ServiceStatusCodes.OK : ServiceStatusCodes.UnknownError,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }

    private async Task<ServiceReply> HandleGetMessagesAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<GetMessagesRequest>(call.Payload);
        if (request == null || string.IsNullOrWhiteSpace(request.PodId) || string.IsNullOrWhiteSpace(request.ChannelId))
        {
            return new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "PodId and ChannelId are required"
            };
        }

        var messages = await _podMessaging.GetMessagesAsync(
            request.PodId,
            request.ChannelId,
            request.SinceTimestamp,
            cancellationToken);
        
        var response = JsonSerializer.Serialize(messages);
        
        return new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = ServiceStatusCodes.OK,
            Payload = System.Text.Encoding.UTF8.GetBytes(response)
        };
    }
}

// Request/Response DTOs
public record GetPodRequest
{
    public string PodId { get; init; } = string.Empty;
}

public record JoinPodRequest
{
    public string PodId { get; init; } = string.Empty;
    public string? Role { get; init; }
}

public record LeavePodRequest
{
    public string PodId { get; init; } = string.Empty;
}

public record PostMessageRequest
{
    public string ChannelId { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? Signature { get; init; }
}

public record GetMessagesRequest
{
    public string PodId { get; init; } = string.Empty;
    public string ChannelId { get; init; } = string.Empty;
    public long? SinceTimestamp { get; init; }
}
