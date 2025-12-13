// <copyright file="PodMembershipController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

/// <summary>
/// Pod membership management API controller.
/// </summary>
[Route("api/v0/podcore/membership")]
[ApiController]
public class PodMembershipController : ControllerBase
{
    private readonly ILogger<PodMembershipController> _logger;
    private readonly IPodMembershipService _membershipService;

    public PodMembershipController(
        ILogger<PodMembershipController> logger,
        IPodMembershipService membershipService)
    {
        _logger = logger;
        _membershipService = membershipService;
    }

    /// <summary>
    /// Publishes a membership record to DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="member">The pod member.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish result.</returns>
    [HttpPost("{podId}/members")]
    public async Task<IActionResult> PublishMembership(string podId, [FromBody] PodMember member, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || member == null || string.IsNullOrWhiteSpace(member.PeerId))
        {
            return BadRequest(new { error = "Valid podId and member with PeerId are required" });
        }

        try
        {
            var result = await _membershipService.PublishMembershipAsync(podId, member, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodMembership] Published membership for {PeerId} in pod {PodId}", result.PeerId, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodMembership] Failed to publish membership for {PeerId} in {PodId}: {Error}", result.PeerId, result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error publishing membership");
            return StatusCode(500, new { error = "Failed to publish membership" });
        }
    }

    /// <summary>
    /// Updates a membership record in DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="member">The updated pod member.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    [HttpPut("{podId}/members/{peerId}")]
    public async Task<IActionResult> UpdateMembership(string podId, string peerId, [FromBody] PodMember member, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId) || member == null)
        {
            return BadRequest(new { error = "Valid podId, peerId, and member are required" });
        }

        // Ensure the member has the correct peer ID
        member.PeerId = peerId;

        try
        {
            var result = await _membershipService.UpdateMembershipAsync(podId, member, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodMembership] Updated membership for {PeerId} in pod {PodId}", result.PeerId, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodMembership] Failed to update membership for {PeerId} in {PodId}: {Error}", result.PeerId, result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error updating membership");
            return StatusCode(500, new { error = "Failed to update membership" });
        }
    }

    /// <summary>
    /// Removes a membership record from DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The removal result.</returns>
    [HttpDelete("{podId}/{peerId}")]
    public async Task<IActionResult> RemoveMembership(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var result = await _membershipService.RemoveMembershipAsync(podId, peerId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodMembership] Removed membership for {PeerId} from pod {PodId}", peerId, podId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodMembership] Failed to remove membership for {PeerId} from {PodId}: {Error}", peerId, podId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error removing membership for {PeerId} from {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to remove membership" });
        }
    }

    /// <summary>
    /// Gets a membership record from DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The membership record.</returns>
    [HttpGet("{podId}/{peerId}")]
    public async Task<IActionResult> GetMembership(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var result = await _membershipService.GetMembershipAsync(podId, peerId, cancellationToken);

            if (result.Found)
            {
                return Ok(result);
            }
            else
            {
                return NotFound(new { podId, peerId, found = false, error = result.ErrorMessage ?? "Membership not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error retrieving membership for {PeerId} in {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to retrieve membership" });
        }
    }

    /// <summary>
    /// Verifies membership in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    [HttpGet("{podId}/{peerId}/verify")]
    public async Task<IActionResult> VerifyMembership(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var result = await _membershipService.VerifyMembershipAsync(podId, peerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error verifying membership for {PeerId} in {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to verify membership" });
        }
    }

    /// <summary>
    /// Bans a member from a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="request">The ban request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ban result.</returns>
    [HttpPost("{podId}/{peerId}/ban")]
    public async Task<IActionResult> BanMember(string podId, string peerId, [FromBody] BanRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var result = await _membershipService.BanMemberAsync(podId, peerId, request?.Reason, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodMembership] Banned member {PeerId} from pod {PodId}", peerId, podId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodMembership] Failed to ban member {PeerId} from {PodId}: {Error}", peerId, podId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error banning member {PeerId} from {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to ban member" });
        }
    }

    /// <summary>
    /// Unbans a member from a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unban result.</returns>
    [HttpPost("{podId}/{peerId}/unban")]
    public async Task<IActionResult> UnbanMember(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var result = await _membershipService.UnbanMemberAsync(podId, peerId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodMembership] Unbanned member {PeerId} from pod {PodId}", peerId, podId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodMembership] Failed to unban member {PeerId} from {PodId}: {Error}", peerId, podId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error unbanning member {PeerId} from {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to unban member" });
        }
    }

    /// <summary>
    /// Changes a member's role in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="request">The role change request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role change result.</returns>
    [HttpPost("{podId}/{peerId}/role")]
    public async Task<IActionResult> ChangeRole(string podId, string peerId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId) || string.IsNullOrWhiteSpace(request?.NewRole))
        {
            return BadRequest(new { error = "PodId, PeerId, and NewRole are required" });
        }

        try
        {
            var result = await _membershipService.ChangeRoleAsync(podId, peerId, request.NewRole, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodMembership] Changed role for {PeerId} in pod {PodId} to {Role}", peerId, podId, request.NewRole);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodMembership] Failed to change role for {PeerId} in {PodId}: {Error}", peerId, podId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error changing role for {PeerId} in {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to change role" });
        }
    }

    /// <summary>
    /// Gets membership statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Membership statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetMembershipStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _membershipService.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error getting membership stats");
            return StatusCode(500, new { error = "Failed to get membership statistics" });
        }
    }

    /// <summary>
    /// Cleans up expired membership records.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupExpiredMemberships(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _membershipService.CleanupExpiredAsync(cancellationToken);
            _logger.LogInformation("[PodMembership] Cleaned up {Count} expired memberships", result.RecordsCleaned);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMembership] Error cleaning up expired memberships");
            return StatusCode(500, new { error = "Failed to cleanup expired memberships" });
        }
    }
}

/// <summary>
/// Request to ban a member.
/// </summary>
public record BanRequest(string? Reason);

/// <summary>
/// Request to change a member's role.
/// </summary>
public record ChangeRoleRequest(string NewRole);
