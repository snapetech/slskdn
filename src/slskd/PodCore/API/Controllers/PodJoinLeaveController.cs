// <copyright file="PodJoinLeaveController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

/// <summary>
/// Pod join/leave operations API controller.
/// </summary>
[Route("api/v0/podcore/membership")]
[ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodJoinLeaveController : ControllerBase
{
    private readonly ILogger<PodJoinLeaveController> _logger;
    private readonly IPodJoinLeaveService _joinLeaveService;

    public PodJoinLeaveController(
        ILogger<PodJoinLeaveController> logger,
        IPodJoinLeaveService joinLeaveService)
    {
        _logger = logger;
        _joinLeaveService = joinLeaveService;
    }

    /// <summary>
    /// Submits a signed join request to a pod.
    /// </summary>
    /// <param name="joinRequest">The signed join request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The join result.</returns>
    [HttpPost("join")]
    public async Task<IActionResult> RequestJoin([FromBody] PodJoinRequest joinRequest, CancellationToken cancellationToken = default)
    {
        if (joinRequest == null || string.IsNullOrWhiteSpace(joinRequest.PodId) || string.IsNullOrWhiteSpace(joinRequest.PeerId))
        {
            return BadRequest(new { error = "Valid join request with PodId and PeerId is required" });
        }

        try
        {
            var result = await _joinLeaveService.RequestJoinAsync(joinRequest, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodJoinLeave] Join request submitted for {PeerId} to join {PodId}", result.PeerId, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodJoinLeave] Join request failed for {PeerId} to {PodId}: {Error}", result.PeerId, result.PodId, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing join request");
            return StatusCode(500, new { error = "Failed to process join request" });
        }
    }

    /// <summary>
    /// Accepts or rejects a pending join request.
    /// </summary>
    /// <param name="acceptance">The signed acceptance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acceptance result.</returns>
    [HttpPost("join/accept")]
    public async Task<IActionResult> AcceptJoin([FromBody] PodJoinAcceptance acceptance, CancellationToken cancellationToken = default)
    {
        if (acceptance == null || string.IsNullOrWhiteSpace(acceptance.PodId) || string.IsNullOrWhiteSpace(acceptance.PeerId))
        {
            return BadRequest(new { error = "Valid acceptance with PodId and PeerId is required" });
        }

        try
        {
            var result = await _joinLeaveService.ProcessJoinAcceptanceAsync(acceptance, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodJoinLeave] Join acceptance processed for {PeerId} in {PodId}", result.PeerId, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodJoinLeave] Join acceptance failed for {PeerId} in {PodId}: {Error}", result.PeerId, result.PodId, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing join acceptance");
            return StatusCode(500, new { error = "Failed to process join acceptance" });
        }
    }

    /// <summary>
    /// Submits a signed leave request from a pod.
    /// </summary>
    /// <param name="leaveRequest">The signed leave request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The leave result.</returns>
    [HttpPost("leave")]
    public async Task<IActionResult> RequestLeave([FromBody] PodLeaveRequest leaveRequest, CancellationToken cancellationToken = default)
    {
        if (leaveRequest == null || string.IsNullOrWhiteSpace(leaveRequest.PodId) || string.IsNullOrWhiteSpace(leaveRequest.PeerId))
        {
            return BadRequest(new { error = "Valid leave request with PodId and PeerId is required" });
        }

        try
        {
            var result = await _joinLeaveService.RequestLeaveAsync(leaveRequest, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodJoinLeave] Leave request submitted for {PeerId} from {PodId}", result.PeerId, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodJoinLeave] Leave request failed for {PeerId} from {PodId}: {Error}", result.PeerId, result.PodId, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing leave request");
            return StatusCode(500, new { error = "Failed to process leave request" });
        }
    }

    /// <summary>
    /// Accepts a pending leave request.
    /// </summary>
    /// <param name="acceptance">The signed acceptance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acceptance result.</returns>
    [HttpPost("leave/accept")]
    public async Task<IActionResult> AcceptLeave([FromBody] PodLeaveAcceptance acceptance, CancellationToken cancellationToken = default)
    {
        if (acceptance == null || string.IsNullOrWhiteSpace(acceptance.PodId) || string.IsNullOrWhiteSpace(acceptance.PeerId))
        {
            return BadRequest(new { error = "Valid acceptance with PodId and PeerId is required" });
        }

        try
        {
            var result = await _joinLeaveService.ProcessLeaveAcceptanceAsync(acceptance, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodJoinLeave] Leave acceptance processed for {PeerId} from {PodId}", result.PeerId, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodJoinLeave] Leave acceptance failed for {PeerId} from {PodId}: {Error}", result.PeerId, result.PodId, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error processing leave acceptance");
            return StatusCode(500, new { error = "Failed to process leave acceptance" });
        }
    }

    /// <summary>
    /// Gets pending join requests for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending join requests.</returns>
    [HttpGet("join/pending/{podId}")]
    public async Task<IActionResult> GetPendingJoinRequests(string podId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "PodId is required" });
        }

        try
        {
            var requests = await _joinLeaveService.GetPendingJoinRequestsAsync(podId, cancellationToken);
            return Ok(new { podId, pendingJoinRequests = requests });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error getting pending join requests for {PodId}", podId);
            return StatusCode(500, new { error = "Failed to get pending join requests" });
        }
    }

    /// <summary>
    /// Gets pending leave requests for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending leave requests.</returns>
    [HttpGet("leave/pending/{podId}")]
    public async Task<IActionResult> GetPendingLeaveRequests(string podId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "PodId is required" });
        }

        try
        {
            var requests = await _joinLeaveService.GetPendingLeaveRequestsAsync(podId, cancellationToken);
            return Ok(new { podId, pendingLeaveRequests = requests });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error getting pending leave requests for {PodId}", podId);
            return StatusCode(500, new { error = "Failed to get pending leave requests" });
        }
    }

    /// <summary>
    /// Cancels a pending join request.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancellation result.</returns>
    [HttpDelete("join/{podId}/{peerId}")]
    public async Task<IActionResult> CancelJoinRequest(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var cancelled = await _joinLeaveService.CancelJoinRequestAsync(podId, peerId, cancellationToken);

            if (cancelled)
            {
                _logger.LogInformation("[PodJoinLeave] Cancelled join request for {PeerId} from {PodId}", peerId, podId);
                return Ok(new { cancelled = true });
            }
            else
            {
                return NotFound(new { error = "No pending join request found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error cancelling join request for {PeerId} from {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to cancel join request" });
        }
    }

    /// <summary>
    /// Cancels a pending leave request.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancellation result.</returns>
    [HttpDelete("leave/{podId}/{peerId}")]
    public async Task<IActionResult> CancelLeaveRequest(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var cancelled = await _joinLeaveService.CancelLeaveRequestAsync(podId, peerId, cancellationToken);

            if (cancelled)
            {
                _logger.LogInformation("[PodJoinLeave] Cancelled leave request for {PeerId} from {PodId}", peerId, podId);
                return Ok(new { cancelled = true });
            }
            else
            {
                return NotFound(new { error = "No pending leave request found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodJoinLeave] Error cancelling leave request for {PeerId} from {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to cancel leave request" });
        }
    }
}

