// <copyright file="QuarantineJuryController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.QuarantineJury.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;

[ApiController]
[Route("api/v{version:apiVersion}/quarantine-jury")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class QuarantineJuryController : ControllerBase
{
    private readonly IQuarantineJuryService _juryService;

    public QuarantineJuryController(IQuarantineJuryService juryService)
    {
        _juryService = juryService;
    }

    [HttpPost("requests")]
    [ProducesResponseType(typeof(QuarantineJuryValidationResult), 200)]
    public async Task<IActionResult> CreateRequest(
        [FromBody] QuarantineJuryRequest request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest("request is required");
        }

        var result = await _juryService.CreateRequestAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsValid ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests")]
    [ProducesResponseType(typeof(IReadOnlyList<QuarantineJuryRequest>), 200)]
    public async Task<IActionResult> GetRequests(CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var requests = await _juryService.GetRequestsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(requests);
    }

    [HttpGet("requests/{requestId}")]
    [ProducesResponseType(typeof(QuarantineJuryRequest), 200)]
    public async Task<IActionResult> GetRequest(string requestId, CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var request = await _juryService.GetRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        return request == null ? NotFound() : Ok(request);
    }

    [HttpPost("verdicts")]
    [ProducesResponseType(typeof(QuarantineJuryValidationResult), 200)]
    public async Task<IActionResult> SubmitVerdict(
        [FromBody] QuarantineJuryVerdictRecord verdict,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (verdict == null)
        {
            return BadRequest("verdict is required");
        }

        var result = await _juryService.SubmitVerdictAsync(verdict, cancellationToken).ConfigureAwait(false);
        return result.IsValid ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests/{requestId}/aggregate")]
    [ProducesResponseType(typeof(QuarantineJuryAggregate), 200)]
    public async Task<IActionResult> GetAggregate(string requestId, CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var aggregate = await _juryService.GetAggregateAsync(requestId, cancellationToken).ConfigureAwait(false);
        return aggregate.Reason == "Request not found." ? NotFound(aggregate) : Ok(aggregate);
    }

    [HttpGet("requests/{requestId}/review")]
    [ProducesResponseType(typeof(QuarantineJuryReview), 200)]
    public async Task<IActionResult> GetReview(string requestId, CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var review = await _juryService.GetReviewAsync(requestId, cancellationToken).ConfigureAwait(false);
        return review == null ? NotFound() : Ok(review);
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(QuarantineJuryAuditReport), 200)]
    public async Task<IActionResult> GetAuditReport(
        [FromQuery] int staleAfterHours = 72,
        CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var report = await _juryService.GetAuditReportAsync(staleAfterHours, cancellationToken).ConfigureAwait(false);
        return Ok(report);
    }

    [HttpPost("requests/{requestId}/accept-release-candidate")]
    [ProducesResponseType(typeof(QuarantineJuryAcceptanceResult), 200)]
    public async Task<IActionResult> AcceptReleaseCandidate(
        string requestId,
        [FromBody] QuarantineJuryAcceptanceRequest acceptanceRequest,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var result = await _juryService.AcceptReleaseCandidateAsync(
            requestId,
            acceptanceRequest ?? new QuarantineJuryAcceptanceRequest(),
            cancellationToken).ConfigureAwait(false);
        if (result.Errors.Contains("Request not found."))
        {
            return NotFound(result);
        }

        return result.IsAccepted ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests/{requestId}/release-package")]
    [ProducesResponseType(typeof(QuarantineJuryReleasePackageResult), 200)]
    public async Task<IActionResult> GetReleasePackage(string requestId, CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var result = await _juryService.GetReleasePackageAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (result.Errors.Contains("Request not found."))
        {
            return NotFound(result);
        }

        return result.IsReady ? Ok(result) : BadRequest(result);
    }

    [HttpPost("requests/{requestId}/routes")]
    [ProducesResponseType(typeof(QuarantineJuryRouteAttempt), 200)]
    public async Task<IActionResult> RouteRequest(
        string requestId,
        [FromBody] QuarantineJuryRouteRequest routeRequest,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var attempt = await _juryService.RouteRequestAsync(
            requestId,
            routeRequest ?? new QuarantineJuryRouteRequest(),
            cancellationToken).ConfigureAwait(false);
        if (attempt.ErrorMessage == "Request not found.")
        {
            return NotFound(attempt);
        }

        return attempt.Success ? Ok(attempt) : BadRequest(attempt);
    }

    [HttpGet("requests/{requestId}/routes")]
    [ProducesResponseType(typeof(IReadOnlyList<QuarantineJuryRouteAttempt>), 200)]
    public async Task<IActionResult> GetRouteAttempts(string requestId, CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var attempts = await _juryService.GetRouteAttemptsAsync(requestId, cancellationToken).ConfigureAwait(false);
        return Ok(attempts);
    }
}
