namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Transfers;
using Soulseek;

/// <summary>
/// Provides slskd-compatible downloads API.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class DownloadsCompatibilityController : ControllerBase
{
    private readonly ITransferService transferService;
    private readonly ILogger<DownloadsCompatibilityController> logger;

    public DownloadsCompatibilityController(
        ITransferService transferService,
        ILogger<DownloadsCompatibilityController> logger)
    {
        this.transferService = transferService;
        this.logger = logger;
    }

    /// <summary>
    /// Create downloads (slskd compatibility).
    /// </summary>
    [HttpPost("downloads")]
    [Authorize]
    public async Task<IActionResult> CreateDownloads(
        [FromBody] DownloadRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Compatibility download: {Count} items", request.Items.Count);

        var downloadIds = new List<string>();

        foreach (var item in request.Items)
        {
            try
            {
                var transfer = await transferService.EnqueueDownloadAsync(
                    item.User,
                    item.RemotePath,
                    item.TargetDir,
                    cancellationToken);

                downloadIds.Add(transfer.Id.ToString());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue download for {User}/{Path}",
                    item.User, item.RemotePath);
                // Continue with other downloads
            }
        }

        return Ok(new { download_ids = downloadIds });
    }

    /// <summary>
    /// Get all downloads (slskd compatibility).
    /// </summary>
    [HttpGet("downloads")]
    [Authorize]
    public async Task<IActionResult> GetDownloads(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var transfers = await transferService.GetAllDownloadsAsync(cancellationToken);

        if (!string.IsNullOrEmpty(status))
        {
            var filterStatus = ParseStatus(status);
            transfers = transfers.Where(t => MapStatus(t.State) == filterStatus).ToList();
        }

        return Ok(new
        {
            downloads = transfers.Select(t => new
            {
                id = t.Id.ToString(),
                user = t.Username,
                remote_path = t.Filename,
                local_path = System.IO.Path.Combine(t.Data.LocalFilename ?? string.Empty),
                status = MapStatus(t.State),
                progress = t.PercentComplete / 100.0,
                bytes_total = t.Size,
                bytes_transferred = t.BytesTransferred,
                error = t.Exception?.Message
            })
        });
    }

    /// <summary>
    /// Get a single download (slskd compatibility).
    /// </summary>
    [HttpGet("downloads/{id}")]
    [Authorize]
    public async Task<IActionResult> GetDownload(
        Guid id,
        CancellationToken cancellationToken)
    {
        var transfer = await transferService.GetDownloadAsync(id, cancellationToken);

        if (transfer == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = transfer.Id.ToString(),
            user = transfer.Username,
            remote_path = transfer.Filename,
            local_path = System.IO.Path.Combine(transfer.Data.LocalFilename ?? string.Empty),
            status = MapStatus(transfer.State),
            progress = transfer.PercentComplete / 100.0,
            bytes_total = transfer.Size,
            bytes_transferred = transfer.BytesTransferred,
            error = transfer.Exception?.Message
        });
    }

    private static string MapStatus(TransferStates state)
    {
        return state switch
        {
            TransferStates.Queued => "queued",
            TransferStates.Initializing => "queued",
            TransferStates.Requested => "running",
            TransferStates.InProgress => "running",
            TransferStates.Completed => "completed",
            TransferStates.Cancelled => "cancelled",
            TransferStates.TimedOut => "failed",
            _ => "failed"
        };
    }

    private static string ParseStatus(string status)
    {
        return status.ToLowerInvariant();
    }
}

public record DownloadRequest(List<DownloadItem> Items);

public record DownloadItem(
    string User,
    string RemotePath,
    string TargetDir,
    string? TargetFilename = null);
