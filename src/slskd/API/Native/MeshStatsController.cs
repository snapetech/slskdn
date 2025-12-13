namespace slskd.API.Native;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Mesh;

/// <summary>
/// Provides mesh transport statistics API endpoints.
/// </summary>
[ApiController]
[Route("api/v0/mesh")]
[Produces("application/json")]
[Authorize]
public class MeshStatsController : ControllerBase
{
    private readonly IMeshAdvanced meshAdvanced;

    public MeshStatsController(IMeshAdvanced meshAdvanced)
    {
        this.meshAdvanced = meshAdvanced;
    }

    /// <summary>
    /// Gets current mesh transport statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        try
        {
            var stats = await meshAdvanced.GetTransportStatsAsync(ct);
            return Ok(new
            {
                dht = stats.ActiveDhtSessions,
                overlay = stats.ActiveOverlaySessions,
                mirrored = stats.ActiveMirroredSessions,
                natType = stats.DetectedNatType.ToString().ToLowerInvariant()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve mesh stats", message = ex.Message });
        }
    }
}















