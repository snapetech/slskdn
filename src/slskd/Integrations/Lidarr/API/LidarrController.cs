// <copyright file="LidarrController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.Lidarr.API;

using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Core.Security;

[Route("api/v{version:apiVersion}/integrations/lidarr")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly]
public sealed class LidarrController : ControllerBase
{
    public LidarrController(
        ILidarrClient lidarrClient,
        ILidarrSyncService lidarrSyncService,
        ILidarrImportService lidarrImportService)
    {
        LidarrClient = lidarrClient;
        LidarrSyncService = lidarrSyncService;
        LidarrImportService = lidarrImportService;
    }

    private ILidarrClient LidarrClient { get; }

    private ILidarrSyncService LidarrSyncService { get; }

    private ILidarrImportService LidarrImportService { get; }

    [HttpGet("status")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        var status = await LidarrClient.GetSystemStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    [HttpGet("wanted/missing")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> GetWantedMissing([FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var wanted = await LidarrClient.GetWantedMissingAsync(pageSize, cancellationToken).ConfigureAwait(false);
        return Ok(wanted);
    }

    [HttpPost("wanted/sync")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> SyncWanted(CancellationToken cancellationToken = default)
    {
        var result = await LidarrSyncService.SyncWantedToWishlistAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("manualimport")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> ImportCompletedDirectory([FromBody] LidarrImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Directory))
        {
            return BadRequest("Directory is required");
        }

        var result = await LidarrImportService.ImportCompletedDirectoryAsync(request.Directory, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}

public sealed record LidarrImportRequest(string Directory);
