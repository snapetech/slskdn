namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Search;

/// <summary>
/// Provides slskd-compatible search API.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class SearchCompatibilityController : ControllerBase
{
    private readonly ISearchService searchService;
    private readonly ILogger<SearchCompatibilityController> logger;

    public SearchCompatibilityController(
        ISearchService searchService,
        ILogger<SearchCompatibilityController> logger)
    {
        this.searchService = searchService;
        this.logger = logger;
    }

    /// <summary>
    /// Perform a Soulseek search (slskd compatibility).
    /// </summary>
    [HttpPost("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Compatibility search: {Query}", request.Query);

        var scope = request.Type?.ToLowerInvariant() == "room"
            ? SearchScope.Room
            : SearchScope.Network;

        var results = await searchService.SearchAsync(
            request.Query,
            scope: scope,
            filterResponses: true,
            minimumResponseFileCount: 1,
            minimumPeerFreeUploadSlots: 0,
            maximumPeerQueueLength: int.MaxValue,
            timeout: 30000,
            responseLimit: request.Limit ?? 200,
            fileLimit: int.MaxValue,
            cancellationToken: cancellationToken);

        return Ok(new
        {
            search_id = results.Id.ToString(),
            query = request.Query,
            results = results.Responses.Select(r => new
            {
                user = r.Username,
                speed_kbps = r.UploadSpeed / 1024,
                files = r.Files.Select(f => new
                {
                    path = f.Filename,
                    size_bytes = f.Size,
                    bitrate = f.BitRate,
                    length_ms = f.Length.HasValue ? f.Length.Value * 1000 : (int?)null,
                    ext = System.IO.Path.GetExtension(f.Filename).TrimStart('.')
                })
            })
        });
    }
}

public record SearchRequest(
    string Query,
    string? Type,
    int? Limit);
