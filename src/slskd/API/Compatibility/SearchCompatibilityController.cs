namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using slskd.Search;
using Soulseek;

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

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required" });
        }

        // Generate a search ID
        var searchId = Guid.NewGuid();

        try
        {
            // Perform actual search using ISearchService
            var searchQuery = Soulseek.SearchQuery.FromText(request.Query);
            var searchScope = Soulseek.SearchScope.Network;
            var searchOptions = new Soulseek.SearchOptions(
                filterResponses: true,
                responseLimit: request.Limit ?? 50);

            var search = await searchService.StartAsync(searchId, searchQuery, searchScope, searchOptions);

            // Convert search results to compatibility format
            var results = new List<object>();
            if (search?.Responses != null)
            {
                foreach (var response in search.Responses)
                {
                    foreach (var file in response.Files)
                    {
                        results.Add(new
                        {
                            Username = response.Username,
                            Filename = file.Filename,
                            Size = file.Size,
                            Code = file.Code,
                            Extension = file.Extension
                        });
                    }
                }
            }

            return Ok(new
            {
                SearchId = searchId.ToString("N"),
                Query = request.Query,
                Results = results
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "Search failed", message = ex.Message });
        }
    }
}

public record SearchRequest(
    string Query,
    string? Type,
    int? Limit);
