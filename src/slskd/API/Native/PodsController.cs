// <copyright file="PodsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IPodService podService;
    private readonly IPodMessaging podMessaging;
    private readonly ISoulseekChatBridge chatBridge;
    private readonly ILogger<PodsController> logger;

    public PodsController(
        IPodService podService,
        IPodMessaging podMessaging,
        ISoulseekChatBridge chatBridge,
        ILogger<PodsController> logger)
    {
        this.podService = podService;
        this.podMessaging = podMessaging;
        this.chatBridge = chatBridge;
        this.logger = logger;
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
        try
        {
            var pod = await podService.GetPodAsync(podId, ct);
            if (pod == null)
            {
                return NotFound(new { error = $"Pod {podId} not found" });
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
            return BadRequest(new { error = ex.Message });
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
                return NotFound(new { error = $"Pod {podId} not found" });
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
            return NotFound(new { error = $"Pod {podId} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
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
        try
        {
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
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PeerId))
            {
                return BadRequest(new { error = "PeerId is required" });
            }

            var member = new PodMember
            {
                PeerId = request.PeerId,
                Role = "member",
                IsBanned = false,
            };

            var joined = await podService.JoinAsync(podId, member, ct);
            if (!joined)
            {
                return BadRequest(new { error = "Failed to join pod (may already be a member)" });
            }

            return Ok(new { podId, peerId = request.PeerId, joined = true });
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
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PeerId))
            {
                return BadRequest(new { error = "PeerId is required" });
            }

            var left = await podService.LeaveAsync(podId, request.PeerId, ct);
            if (!left)
            {
                return NotFound(new { error = "Pod or member not found" });
            }

            return Ok(new { podId, peerId = request.PeerId, left = true });
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
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PeerId))
            {
                return BadRequest(new { error = "PeerId is required" });
            }

            var banned = await podService.BanAsync(podId, request.PeerId, ct);
            if (!banned)
            {
                return NotFound(new { error = "Pod or member not found" });
            }

            return Ok(new { podId, peerId = request.PeerId, banned = true });
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
        try
        {
            // HARDENING: Use channelId directly without concatenation
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
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new { error = "Message body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.SenderPeerId))
            {
                return BadRequest(new { error = "SenderPeerId is required" });
            }

            // HARDENING: Use channelId directly without concatenation
            var message = new PodMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ChannelId = channelId,
                SenderPeerId = request.SenderPeerId,
                Body = request.Body,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Signature = request.Signature ?? string.Empty,
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
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RoomName))
            {
                return BadRequest(new { error = "RoomName is required" });
            }

            var mode = request.Mode ?? "readonly";
            if (mode != "readonly" && mode != "mirror")
            {
                return BadRequest(new { error = "Mode must be 'readonly' or 'mirror'" });
            }

            var bound = await chatBridge.BindRoomAsync(podId, channelId, request.RoomName, mode, ct);
            if (!bound)
            {
                return BadRequest(new { error = "Failed to bind channel to room" });
            }

            return Ok(new { podId, channelId, roomName = request.RoomName, mode, bound = true });
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
        try
        {
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
}

public record CreatePodRequest(Pod Pod, string RequestingPeerId);
public record JoinPodRequest(string PeerId);
public record LeavePodRequest(string PeerId);
public record BanMemberRequest(string PeerId);
public record SendMessageRequest(string Body, string SenderPeerId, string? Signature = null);
public record BindRoomRequest(string RoomName, string? Mode = null);
public record UpdatePodRequest(Pod Pod, string RequestingPeerId);
