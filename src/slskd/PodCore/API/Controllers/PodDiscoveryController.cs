// <copyright file="PodDiscoveryController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

/// <summary>
/// Pod discovery API controller.
/// </summary>
[Route("api/v0/podcore/discovery")]
[ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodDiscoveryController : ControllerBase
{
    private readonly ILogger<PodDiscoveryController> _logger;
    private readonly IPodDiscoveryService _discoveryService;

    public PodDiscoveryController(
        ILogger<PodDiscoveryController> logger,
        IPodDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Registers a pod for discovery.
    /// </summary>
    /// <param name="pod">The pod to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration result.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterPod([FromBody] Pod pod, CancellationToken cancellationToken = default)
    {
        if (pod == null || string.IsNullOrWhiteSpace(pod.PodId))
        {
            return BadRequest(new { error = "Valid pod with PodId is required" });
        }

        try
        {
            var result = await _discoveryService.RegisterPodAsync(pod, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDiscovery] Registered pod {PodId} for discovery with {Count} keys", result.PodId, result.DiscoveryKeys.Count);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDiscovery] Failed to register pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error registering pod");
            return StatusCode(500, new { error = "Failed to register pod" });
        }
    }

    /// <summary>
    /// Unregisters a pod from discovery.
    /// </summary>
    /// <param name="podId">The pod ID to unregister.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unregistration result.</returns>
    [HttpDelete("unregister/{podId}")]
    public async Task<IActionResult> UnregisterPod(string podId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "PodId is required" });
        }

        try
        {
            var result = await _discoveryService.UnregisterPodAsync(podId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDiscovery] Unregistered pod {PodId} from discovery", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDiscovery] Failed to unregister pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error unregistering pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to unregister pod" });
        }
    }

    /// <summary>
    /// Updates pod discovery information.
    /// </summary>
    /// <param name="pod">The updated pod.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    [HttpPost("update")]
    public async Task<IActionResult> UpdatePod([FromBody] Pod pod, CancellationToken cancellationToken = default)
    {
        if (pod == null || string.IsNullOrWhiteSpace(pod.PodId))
        {
            return BadRequest(new { error = "Valid pod with PodId is required" });
        }

        try
        {
            var result = await _discoveryService.UpdatePodAsync(pod, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDiscovery] Updated pod {PodId} discovery information", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDiscovery] Failed to update pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error updating pod discovery");
            return StatusCode(500, new { error = "Failed to update pod discovery" });
        }
    }

    /// <summary>
    /// Discovers pods by name.
    /// </summary>
    /// <param name="name">The pod name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching pods.</returns>
    [HttpGet("name/{name}")]
    public async Task<IActionResult> DiscoverPodsByName(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Name is required" });
        }

        try
        {
            var result = await _discoveryService.DiscoverPodsByNameAsync(name, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by name: {Name}", name);
            return StatusCode(500, new { error = "Failed to discover pods by name" });
        }
    }

    /// <summary>
    /// Discovers pods by tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pods with the specified tag.</returns>
    [HttpGet("tag/{tag}")]
    public async Task<IActionResult> DiscoverPodsByTag(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return BadRequest(new { error = "Tag is required" });
        }

        try
        {
            var result = await _discoveryService.DiscoverPodsByTagAsync(tag, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by tag: {Tag}", tag);
            return StatusCode(500, new { error = "Failed to discover pods by tag" });
        }
    }

    /// <summary>
    /// Discovers pods by multiple tags.
    /// </summary>
    /// <param name="tags">Comma-separated list of tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pods matching all tags.</returns>
    [HttpGet("tags/{tags}")]
    public async Task<IActionResult> DiscoverPodsByTags(string tags, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return BadRequest(new { error = "Tags are required" });
        }

        try
        {
            var tagList = tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
            var result = await _discoveryService.DiscoverPodsByTagsAsync(tagList, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by tags: {Tags}", tags);
            return StatusCode(500, new { error = "Failed to discover pods by tags" });
        }
    }

    /// <summary>
    /// Gets a general list of discoverable pods.
    /// </summary>
    /// <param name="limit">Maximum number of pods to return (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A sample of discoverable pods.</returns>
    [HttpGet("all")]
    public async Task<IActionResult> DiscoverAllPods([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 1000)
        {
            return BadRequest(new { error = "Limit must be between 1 and 1000" });
        }

        try
        {
            var result = await _discoveryService.DiscoverAllPodsAsync(limit, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering all pods");
            return StatusCode(500, new { error = "Failed to discover pods" });
        }
    }

    /// <summary>
    /// Discovers pods by content ID.
    /// </summary>
    /// <param name="contentId">The content ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pods associated with the content.</returns>
    [HttpGet("content/{*contentId}")]
    public async Task<IActionResult> DiscoverPodsByContent(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest(new { error = "ContentId is required" });
        }

        try
        {
            var result = await _discoveryService.DiscoverPodsByContentAsync(contentId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error discovering pods by content: {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to discover pods by content" });
        }
    }

    /// <summary>
    /// Gets pod discovery statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetDiscoveryStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _discoveryService.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error getting discovery stats");
            return StatusCode(500, new { error = "Failed to get discovery statistics" });
        }
    }

    /// <summary>
    /// Refreshes pod discovery entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshDiscovery(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _discoveryService.RefreshDiscoveryAsync(cancellationToken);
            _logger.LogInformation("[PodDiscovery] Refresh discovery completed: success={Success}, republished={Republished}",
                result.Success, result.WasRepublished);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDiscovery] Error refreshing discovery");
            return StatusCode(500, new { error = "Failed to refresh discovery" });
        }
    }
}
