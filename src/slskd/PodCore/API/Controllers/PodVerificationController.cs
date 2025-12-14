// <copyright file="PodVerificationController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

/// <summary>
/// Pod membership verification API controller.
/// </summary>
[Route("api/v0/podcore/verification")]
[ApiController]
public class PodVerificationController : ControllerBase
{
    private readonly ILogger<PodVerificationController> _logger;
    private readonly IPodMembershipVerifier _membershipVerifier;

    public PodVerificationController(
        ILogger<PodVerificationController> logger,
        IPodMembershipVerifier membershipVerifier)
    {
        _logger = logger;
        _membershipVerifier = membershipVerifier;
    }

    /// <summary>
    /// Verifies membership in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The membership verification result.</returns>
    [HttpGet("membership/{podId}/{peerId}")]
    public async Task<IActionResult> VerifyMembership(string podId, string peerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId))
        {
            return BadRequest(new { error = "PodId and PeerId are required" });
        }

        try
        {
            var result = await _membershipVerifier.VerifyMembershipAsync(podId, peerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodVerification] Error verifying membership for {PeerId} in {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to verify membership" });
        }
    }

    /// <summary>
    /// Verifies a pod message authenticity.
    /// </summary>
    /// <param name="message">The pod message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message verification result.</returns>
    [HttpPost("message")]
    public async Task<IActionResult> VerifyMessage([FromBody] PodMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            return BadRequest(new { error = "Message is required" });
        }

        try
        {
            var result = await _membershipVerifier.VerifyMessageAsync(message, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodVerification] Error verifying message {MessageId}", message?.MessageId);
            return StatusCode(500, new { error = "Failed to verify message" });
        }
    }

    /// <summary>
    /// Checks if a peer has the required role in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="requiredRole">The required role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role check result.</returns>
    [HttpGet("role/{podId}/{peerId}/{requiredRole}")]
    public async Task<IActionResult> CheckRole(string podId, string peerId, string requiredRole, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(peerId) || string.IsNullOrWhiteSpace(requiredRole))
        {
            return BadRequest(new { error = "PodId, PeerId, and RequiredRole are required" });
        }

        try
        {
            var hasRole = await _membershipVerifier.HasRoleAsync(podId, peerId, requiredRole, cancellationToken);
            return Ok(new { hasRole });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodVerification] Error checking role for {PeerId} in {PodId}", peerId, podId);
            return StatusCode(500, new { error = "Failed to check role" });
        }
    }

    /// <summary>
    /// Gets verification statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetVerificationStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _membershipVerifier.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodVerification] Error getting verification stats");
            return StatusCode(500, new { error = "Failed to get verification statistics" });
        }
    }
}

