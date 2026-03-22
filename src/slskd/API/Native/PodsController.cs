// <copyright file="PodsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Messaging;
using slskd.PodCore;

/// <summary>
/// Provides Pod management API endpoints.
/// </summary>
[ApiController]
[Route("api/v0/pods")]
[Produces("application/json")]
[Authorize]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodsController : ControllerBase
{
    private const string SoulseekDmBindingPrefix = "soulseek-dm:";

    private readonly IPodService podService;
    private readonly IPodMessaging podMessaging;
    private readonly ISoulseekChatBridge chatBridge;
    private readonly ILogger<PodsController> logger;
    private readonly IConversationService? conversationService;
    private readonly IOptionsMonitor<MeshOptions>? meshOptions;

    public PodsController(
        IPodService podService,
        IPodMessaging podMessaging,
        ISoulseekChatBridge chatBridge,
        ILogger<PodsController> logger,
        IConversationService? conversationService = null,
        IOptionsMonitor<MeshOptions>? meshOptions = null)
    {
        this.podService = podService;
        this.podMessaging = podMessaging;
        this.chatBridge = chatBridge;
        this.logger = logger;
        this.conversationService = conversationService;
        this.meshOptions = meshOptions;
    }

    /// <summary>
    /// Lists all pods.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListPods(CancellationToken ct = default)
    {
        try
        {
            var pods = await podService.ListAsync(ct);
            return Ok(pods);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list pods");
            return StatusCode(500, new { error = "Failed to list pods" });
        }
    }

    /// <summary>
    /// Gets a specific pod by ID.
    /// </summary>
    [HttpGet("{podId}")]
    public async Task<IActionResult> GetPod(string podId, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(podId))
            {
                return BadRequest(new { error = "PodId is required" });
            }

            var pod = await podService.GetPodAsync(podId, ct);
            if (pod == null)
            {
                return NotFound(new { error = "Pod not found" });
            }

            return Ok(pod);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to get pod" });
        }
    }

    /// <summary>
    /// Deletes a pod and its members, membership history, and messages.
    /// </summary>
    [HttpDelete("{podId}")]
    public async Task<IActionResult> DeletePod(string podId, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(podId))
                return BadRequest(new { error = "PodId is required" });

            var deleted = await podService.DeletePodAsync(podId, ct);
            if (!deleted)
                return NotFound(new { error = "Pod not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to delete pod" });
        }
    }

    /// <summary>
    /// Creates a new pod.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePod([FromBody] CreatePodRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request == null || request.Pod == null)
            {
                return BadRequest(new { error = "Pod data is required" });
            }

            request = request with
            {
                RequestingPeerId = request.RequestingPeerId?.Trim() ?? string.Empty,
                Pod = NormalizePod(request.Pod)
            };

            if (string.IsNullOrWhiteSpace(request.RequestingPeerId))
            {
                return BadRequest(new { error = "RequestingPeerId is required" });
            }

            // Validate that the requesting peer will be the first member (and owner)
            if (request.Pod.Capabilities?.Contains(PodCapability.PrivateServiceGateway) == true)
            {
                if (request.Pod.PrivateServicePolicy?.GatewayPeerId != request.RequestingPeerId)
                {
                    return BadRequest(new { error = "When creating a VPN pod, RequestingPeerId must match GatewayPeerId" });
                }
            }

            var created = await podService.CreateAsync(request.Pod, ct);

            // Add the creator as the first member (owner)
            var firstMember = new PodMember
            {
                PeerId = request.RequestingPeerId,
                Role = "owner",
                IsBanned = false
            };
            await podService.JoinAsync(created.PodId, firstMember, ct);

            return CreatedAtAction(nameof(GetPod), new { podId = created.PodId }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid pod create request");
            return BadRequest(new { error = "Invalid pod request" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create pod");
            return StatusCode(500, new { error = "Failed to create pod" });
        }
    }

    /// <summary>
    /// Updates an existing pod.
    /// </summary>
    [HttpPut("{podId}")]
    public async Task<IActionResult> UpdatePod(
        string podId,
        [FromBody] UpdatePodRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request == null || request.Pod == null)
            {
                return BadRequest(new { error = "Pod data is required" });
            }

            podId = podId?.Trim() ?? string.Empty;
            request = request with
            {
                RequestingPeerId = request.RequestingPeerId?.Trim() ?? string.Empty,
                Pod = NormalizePod(request.Pod)
            };

            if (request.Pod.PodId != podId)
            {
                return BadRequest(new { error = "PodId in URL must match PodId in body" });
            }

            if (string.IsNullOrWhiteSpace(request.RequestingPeerId))
            {
                return BadRequest(new { error = "RequestingPeerId is required" });
            }

            // Get existing pod to check authorization
            var existingPod = await podService.GetPodAsync(podId, ct);
            if (existingPod == null)
            {
                return NotFound(new { error = "Pod not found" });
            }

            // Get current members for authorization check
            var members = await podService.GetMembersAsync(podId, ct);

            // Check if user is authorized to modify private service policy
            if (request.Pod.Capabilities?.Contains(PodCapability.PrivateServiceGateway) == true ||
                (existingPod.Capabilities?.Contains(PodCapability.PrivateServiceGateway) == true &&
                 request.Pod.PrivateServicePolicy != null))
            {
                // Only gateway peer can enable VPN capability or modify policy
                var gatewayPeerId = request.Pod.PrivateServicePolicy?.GatewayPeerId ??
                                   existingPod.PrivateServicePolicy?.GatewayPeerId;

                if (!string.IsNullOrEmpty(gatewayPeerId))
                {
                    var isGatewayPeer = string.Equals(request.RequestingPeerId, gatewayPeerId, StringComparison.Ordinal);
                    var isPodMember = members.Any(m => string.Equals(m.PeerId, request.RequestingPeerId, StringComparison.Ordinal));

                    if (!isPodMember)
                    {
                        return StatusCode(403, new { error = "Only pod members can update pods" });
                    }

                    if (!isGatewayPeer)
                    {
                        return StatusCode(403, new { error = "Only the designated gateway peer can modify private service policy" });
                    }
                }
            }

            var updated = await podService.UpdateAsync(request.Pod, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Pod not found" });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid pod update request for {PodId}", podId);
            return BadRequest(new { error = "Invalid pod request" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to update pod" });
        }
    }

    /// <summary>
    /// Gets members of a pod.
    /// </summary>
    [HttpGet("{podId}/members")]
    public async Task<IActionResult> GetMembers(string podId, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(podId))
            {
                return BadRequest(new { error = "PodId is required" });
            }

            var members = await podService.GetMembersAsync(podId, ct);
            return Ok(members);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pod members for {PodId}", podId);
            return StatusCode(500, new { error = "Failed to get pod members" });
        }
    }

    /// <summary>
    /// Joins a pod.
    /// </summary>
    [HttpPost("{podId}/join")]
    public async Task<IActionResult> JoinPod(
        string podId,
        [FromBody] JoinPodRequest request,
        CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        try
        {
            var peerId = request?.PeerId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(podId))
            {
                return BadRequest(new { error = "PodId is required" });
            }

            if (string.IsNullOrWhiteSpace(peerId))
            {
                return BadRequest(new { error = "PeerId is required" });
            }

            var member = new PodMember
            {
                PeerId = peerId,
                Role = "member",
                IsBanned = false,
            };

            var joined = await podService.JoinAsync(podId, member, ct);
            if (!joined)
            {
                return BadRequest(new { error = "Failed to join pod (may already be a member)" });
            }

            return Ok(new { podId, peerId, joined = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to join pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to join pod" });
        }
    }

    /// <summary>
    /// Leaves a pod.
    /// </summary>
    [HttpPost("{podId}/leave")]
    public async Task<IActionResult> LeavePod(
        string podId,
        [FromBody] LeavePodRequest request,
        CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        try
        {
            var peerId = request?.PeerId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(podId))
            {
                return BadRequest(new { error = "PodId is required" });
            }

            if (string.IsNullOrWhiteSpace(peerId))
            {
                return BadRequest(new { error = "PeerId is required" });
            }

            var left = await podService.LeaveAsync(podId, peerId, ct);
            if (!left)
            {
                return NotFound(new { error = "Pod or member not found" });
            }

            return Ok(new { podId, peerId, left = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to leave pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to leave pod" });
        }
    }

    /// <summary>
    /// Bans a member from a pod.
    /// </summary>
    [HttpPost("{podId}/ban")]
    public async Task<IActionResult> BanMember(
        string podId,
        [FromBody] BanMemberRequest request,
        CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;

        try
        {
            var peerId = request?.PeerId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(podId))
            {
                return BadRequest(new { error = "PodId is required" });
            }

            if (string.IsNullOrWhiteSpace(peerId))
            {
                return BadRequest(new { error = "PeerId is required" });
            }

            var banned = await podService.BanAsync(podId, peerId, ct);
            if (!banned)
            {
                return NotFound(new { error = "Pod or member not found" });
            }

            return Ok(new { podId, peerId, banned = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ban member from pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to ban member" });
        }
    }

    /// <summary>
    /// Gets messages from a pod channel.
    /// </summary>
    [HttpGet("{podId}/channels/{channelId}/messages")]
    public async Task<IActionResult> GetMessages(
        string podId,
        string channelId,
        [FromQuery] long? since = null,
        CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        channelId = channelId?.Trim() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(channelId))
            {
                return BadRequest(new { error = "PodId and ChannelId are required" });
            }

            var soulseekUsername = await TryGetSoulseekDmUsernameAsync(podId, channelId, ct);
            if (soulseekUsername != null && conversationService != null)
            {
                var conv = await conversationService.FindAsync(soulseekUsername, includeInactive: true, includeMessages: true);
                var selfPeerId = meshOptions?.CurrentValue?.SelfPeerId ?? "bridge:self";
                var podMessages = (conv?.Messages ?? Array.Empty<PrivateMessage>())
                    .OrderBy(m => m.Timestamp)
                    .Select(pm => new PodMessage
                    {
                        MessageId = pm.Id.ToString(),
                        PodId = podId,
                        ChannelId = channelId,
                        SenderPeerId = pm.Direction == MessageDirection.In ? "bridge:" + pm.Username : selfPeerId,
                        Body = pm.Message ?? string.Empty,
                        TimestampUnixMs = new DateTimeOffset(pm.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                        Signature = string.Empty,
                        SigVersion = 1,
                    })
                    .Where(m => !since.HasValue || m.TimestampUnixMs > since.Value)
                    .ToList();
                return Ok(podMessages);
            }

            var messages = await podMessaging.GetMessagesAsync(podId, channelId, since, ct);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get messages for pod {PodId} channel {ChannelId}", podId, channelId);
            return StatusCode(500, new { error = "Failed to get messages" });
        }
    }

    /// <summary>
    /// Sends a message to a pod channel.
    /// </summary>
    [HttpPost("{podId}/channels/{channelId}/messages")]
    public async Task<IActionResult> SendMessage(
        string podId,
        string channelId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        channelId = channelId?.Trim() ?? string.Empty;

        try
        {
            var body = request?.Body?.Trim() ?? string.Empty;
            var senderPeerId = request?.SenderPeerId?.Trim() ?? string.Empty;
            var signature = request?.Signature?.Trim();

            if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(channelId))
            {
                return BadRequest(new { error = "PodId and ChannelId are required" });
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest(new { error = "Message body is required" });
            }

            if (string.IsNullOrWhiteSpace(senderPeerId))
            {
                return BadRequest(new { error = "SenderPeerId is required" });
            }

            var soulseekUsername = await TryGetSoulseekDmUsernameAsync(podId, channelId, ct);
            if (soulseekUsername != null && conversationService != null)
            {
                await conversationService.SendMessageAsync(soulseekUsername, body);
                return Ok(new { messageId = Guid.NewGuid().ToString("N"), sent = true });
            }

            var message = new PodMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                PodId = podId,
                ChannelId = channelId,
                SenderPeerId = senderPeerId,
                Body = body,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Signature = signature ?? string.Empty,
            };

            var sent = await podMessaging.SendAsync(message, ct);
            if (!sent)
            {
                return BadRequest(new { error = "Failed to send message" });
            }

            return Ok(new { messageId = message.MessageId, sent = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message to pod {PodId} channel {ChannelId}", podId, channelId);
            return StatusCode(500, new { error = "Failed to send message" });
        }
    }

    /// <summary>Gets Soulseek username from channel BindingInfo when it is soulseek-dm:username; returns null otherwise.</summary>
    private async Task<string?> TryGetSoulseekDmUsernameAsync(string podId, string channelId, CancellationToken ct)
    {
        var pod = await podService.GetPodAsync(podId, ct);
        var ch = pod?.Channels?.FirstOrDefault(c => string.Equals(c.ChannelId, channelId, StringComparison.Ordinal));
        if (string.IsNullOrEmpty(ch?.BindingInfo) || !ch.BindingInfo.StartsWith(SoulseekDmBindingPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        return ch.BindingInfo.Substring(SoulseekDmBindingPrefix.Length).Trim();
    }

    /// <summary>
    /// Binds a pod channel to a Soulseek room.
    /// </summary>
    [HttpPost("{podId}/channels/{channelId}/bind")]
    public async Task<IActionResult> BindRoom(
        string podId,
        string channelId,
        [FromBody] BindRoomRequest request,
        CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        channelId = channelId?.Trim() ?? string.Empty;

        try
        {
            var roomName = request?.RoomName?.Trim() ?? string.Empty;
            var mode = string.IsNullOrWhiteSpace(request?.Mode) ? "readonly" : request.Mode.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(channelId))
            {
                return BadRequest(new { error = "PodId and ChannelId are required" });
            }

            if (string.IsNullOrWhiteSpace(roomName))
            {
                return BadRequest(new { error = "RoomName is required" });
            }

            if (mode != "readonly" && mode != "mirror")
            {
                return BadRequest(new { error = "Mode must be 'readonly' or 'mirror'" });
            }

            var bound = await chatBridge.BindRoomAsync(podId, channelId, roomName, mode, ct);
            if (!bound)
            {
                return BadRequest(new { error = "Failed to bind channel to room" });
            }

            return Ok(new { podId, channelId, roomName, mode, bound = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bind pod {PodId} channel {ChannelId} to room", podId, channelId);
            return StatusCode(500, new { error = "Failed to bind room" });
        }
    }

    /// <summary>
    /// Unbinds a pod channel from a Soulseek room.
    /// </summary>
    [HttpPost("{podId}/channels/{channelId}/unbind")]
    public async Task<IActionResult> UnbindRoom(
            string podId,
            string channelId,
            CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        channelId = channelId?.Trim() ?? string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(channelId))
            {
                return BadRequest(new { error = "PodId and ChannelId are required" });
            }

            var unbound = await chatBridge.UnbindRoomAsync(podId, channelId, ct);
            if (!unbound)
            {
                return NotFound(new { error = "Channel binding not found" });
            }

            return Ok(new { podId, channelId, unbound = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unbind pod {PodId} channel {ChannelId}", podId, channelId);
            return StatusCode(500, new { error = "Failed to unbind room" });
        }
    }

    private static Pod NormalizePod(Pod pod)
    {
        ArgumentNullException.ThrowIfNull(pod);

        return new Pod
        {
            PodId = pod.PodId?.Trim() ?? string.Empty,
            Name = pod.Name?.Trim() ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(pod.Description) ? null : pod.Description.Trim(),
            Visibility = pod.Visibility,
            IsPublic = pod.IsPublic,
            MaxMembers = pod.MaxMembers,
            AllowGuests = pod.AllowGuests,
            RequireApproval = pod.RequireApproval,
            UpdatedAt = pod.UpdatedAt,
            FocusContentId = string.IsNullOrWhiteSpace(pod.FocusContentId) ? null : pod.FocusContentId.Trim(),
            Tags = pod.Tags?
                .Select(tag => tag?.Trim() ?? string.Empty)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .ToList()
                ?? new List<string>(),
            Channels = pod.Channels?
                .Select(channel => new PodChannel
                {
                    ChannelId = channel.ChannelId?.Trim() ?? string.Empty,
                    Kind = channel.Kind,
                    Name = channel.Name?.Trim() ?? string.Empty,
                    BindingInfo = string.IsNullOrWhiteSpace(channel.BindingInfo) ? null : channel.BindingInfo.Trim(),
                    Description = string.IsNullOrWhiteSpace(channel.Description) ? null : channel.Description.Trim(),
                })
                .ToList()
                ?? new List<PodChannel>(),
            Members = pod.Members?
                .Select(member => new PodMember
                {
                    PeerId = member.PeerId?.Trim() ?? string.Empty,
                    Role = member.Role?.Trim() ?? string.Empty,
                    IsBanned = member.IsBanned,
                    PublicKey = string.IsNullOrWhiteSpace(member.PublicKey) ? null : member.PublicKey.Trim(),
                    JoinedAt = member.JoinedAt,
                    LastSeen = member.LastSeen,
                })
                .ToList(),
            ExternalBindings = pod.ExternalBindings?
                .Select(binding => new ExternalBinding
                {
                    Kind = binding.Kind?.Trim() ?? string.Empty,
                    Mode = binding.Mode?.Trim() ?? string.Empty,
                    Identifier = binding.Identifier?.Trim() ?? string.Empty,
                })
                .ToList()
                ?? new List<ExternalBinding>(),
            Capabilities = pod.Capabilities,
            PrivateServicePolicy = pod.PrivateServicePolicy == null ? null : new PodPrivateServicePolicy
            {
                Enabled = pod.PrivateServicePolicy.Enabled,
                MaxMembers = pod.PrivateServicePolicy.MaxMembers,
                GatewayPeerId = pod.PrivateServicePolicy.GatewayPeerId?.Trim() ?? string.Empty,
                RegisteredServices = pod.PrivateServicePolicy.RegisteredServices?
                    .Select(service => new RegisteredService
                    {
                        Name = service.Name?.Trim() ?? string.Empty,
                        Description = service.Description?.Trim() ?? string.Empty,
                        Host = service.Host?.Trim() ?? string.Empty,
                        Port = service.Port,
                        Protocol = service.Protocol?.Trim() ?? string.Empty,
                        Kind = service.Kind,
                    })
                    .ToList()
                    ?? new List<RegisteredService>(),
                AllowedDestinations = pod.PrivateServicePolicy.AllowedDestinations?
                    .Select(destination => new AllowedDestination
                    {
                        HostPattern = destination.HostPattern?.Trim() ?? string.Empty,
                        Port = destination.Port,
                        Protocol = destination.Protocol?.Trim() ?? string.Empty,
                        AllowPublic = destination.AllowPublic,
                        Kind = destination.Kind,
                    })
                    .ToList()
                    ?? new List<AllowedDestination>(),
                AllowPrivateRanges = pod.PrivateServicePolicy.AllowPrivateRanges,
                AllowPublicDestinations = pod.PrivateServicePolicy.AllowPublicDestinations,
                MaxConcurrentTunnelsPerPeer = pod.PrivateServicePolicy.MaxConcurrentTunnelsPerPeer,
                MaxConcurrentTunnelsPod = pod.PrivateServicePolicy.MaxConcurrentTunnelsPod,
                MaxNewTunnelsPerMinutePerPeer = pod.PrivateServicePolicy.MaxNewTunnelsPerMinutePerPeer,
                MaxBytesPerDayPerPeer = pod.PrivateServicePolicy.MaxBytesPerDayPerPeer,
                IdleTimeout = pod.PrivateServicePolicy.IdleTimeout,
                MaxLifetime = pod.PrivateServicePolicy.MaxLifetime,
                DialTimeout = pod.PrivateServicePolicy.DialTimeout,
                MaxBufferedBytesPerTunnel = pod.PrivateServicePolicy.MaxBufferedBytesPerTunnel,
                MaxFrameSize = pod.PrivateServicePolicy.MaxFrameSize,
            },
        };
    }
}

public record CreatePodRequest(Pod Pod, string RequestingPeerId);
public record JoinPodRequest(string PeerId);
public record LeavePodRequest(string PeerId);
public record BanMemberRequest(string PeerId);
public record SendMessageRequest(string Body, string SenderPeerId, string? Signature = null);
public record BindRoomRequest(string RoomName, string? Mode = null);
public record UpdatePodRequest(Pod Pod, string RequestingPeerId);
