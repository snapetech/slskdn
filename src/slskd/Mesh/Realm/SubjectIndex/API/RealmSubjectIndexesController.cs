// <copyright file="RealmSubjectIndexesController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Realm.SubjectIndex.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;

[ApiController]
[Route("api/v{version:apiVersion}/realm-subject-indexes")]
[ApiVersion("0")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class RealmSubjectIndexesController : ControllerBase
{
    private readonly IRealmSubjectIndexService _subjectIndexService;

    public RealmSubjectIndexesController(IRealmSubjectIndexService subjectIndexService)
    {
        _subjectIndexService = subjectIndexService;
    }

    [HttpGet("{realmId}")]
    [ProducesResponseType(typeof(IReadOnlyList<RealmSubjectIndex>), 200)]
    public async Task<IActionResult> GetIndexes(string realmId, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(realmId))
        {
            return BadRequest("realm id is required");
        }

        var indexes = await _subjectIndexService.GetIndexesForRealmAsync(realmId.Trim(), cancellationToken).ConfigureAwait(false);
        return Ok(indexes);
    }

    [HttpGet("{realmId}/conflicts")]
    [ProducesResponseType(typeof(RealmSubjectIndexConflictReport), 200)]
    public async Task<IActionResult> GetConflicts(string realmId, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(realmId))
        {
            return BadRequest("realm id is required");
        }

        var report = await _subjectIndexService.GetConflictReportAsync(realmId.Trim(), cancellationToken).ConfigureAwait(false);
        return Ok(report);
    }

    [HttpGet("{realmId}/authority-decisions")]
    [ProducesResponseType(typeof(IReadOnlyList<RealmSubjectIndexAuthorityDecision>), 200)]
    public async Task<IActionResult> GetAuthorityDecisions(string realmId, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(realmId))
        {
            return BadRequest("realm id is required");
        }

        var decisions = await _subjectIndexService
            .GetAuthorityDecisionsForRealmAsync(realmId.Trim(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(decisions);
    }

    [HttpPost("{realmId}/{indexId}/authority-decision")]
    [ProducesResponseType(typeof(RealmSubjectIndexAuthorityDecision), 200)]
    public async Task<IActionResult> SetAuthorityDecision(
        string realmId,
        string indexId,
        [FromBody] RealmSubjectIndexAuthorityDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(realmId))
        {
            return BadRequest("realm id is required");
        }

        if (string.IsNullOrWhiteSpace(indexId))
        {
            return BadRequest("index id is required");
        }

        var decision = await _subjectIndexService
            .SetAuthorityEnabledAsync(
                realmId.Trim(),
                indexId.Trim(),
                request ?? new RealmSubjectIndexAuthorityDecisionRequest(),
                cancellationToken)
            .ConfigureAwait(false);

        return decision.IsAccepted ? Ok(decision) : BadRequest(decision);
    }

    [HttpGet("recordings/{recordingId}/resolutions")]
    [ProducesResponseType(typeof(IReadOnlyList<RealmSubjectIndexResolution>), 200)]
    public async Task<IActionResult> ResolveRecording(string recordingId, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(recordingId))
        {
            return BadRequest("recording id is required");
        }

        var resolutions = await _subjectIndexService
            .ResolveByRecordingIdAsync(recordingId.Trim(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(resolutions);
    }
}
