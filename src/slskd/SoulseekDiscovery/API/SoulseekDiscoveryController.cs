// <copyright file="SoulseekDiscoveryController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SoulseekDiscovery.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Common.Security;
using slskd.Core.Security;
using Soulseek;

[ApiController]
[Route("api/v{version:apiVersion}/soulseek")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class SoulseekDiscoveryController : ControllerBase
{
    public SoulseekDiscoveryController(
        ISoulseekDiscoveryService discoveryService,
        ISoulseekSafetyLimiter safetyLimiter,
        ILogger<SoulseekDiscoveryController> logger)
    {
        DiscoveryService = discoveryService;
        SafetyLimiter = safetyLimiter;
        Logger = logger;
    }

    private ISoulseekDiscoveryService DiscoveryService { get; }
    private ILogger<SoulseekDiscoveryController> Logger { get; }
    private ISoulseekSafetyLimiter SafetyLimiter { get; }

    [HttpPost("interests")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> AddInterest([FromBody] SoulseekInterestRequest? request, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var item = NormalizeItem(request?.Item);
        if (item == null)
        {
            return BadRequest("item is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-interest"))
        {
            return StatusCode(429, "Soulseek interest operation rate limit exceeded.");
        }

        await DiscoveryService.AddInterestAsync(item, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("interests/{item}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> RemoveInterest([FromRoute] string item, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var normalizedItem = NormalizeItem(item);
        if (normalizedItem == null)
        {
            return BadRequest("item is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-interest"))
        {
            return StatusCode(429, "Soulseek interest operation rate limit exceeded.");
        }

        await DiscoveryService.RemoveInterestAsync(normalizedItem, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("hated-interests")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> AddHatedInterest([FromBody] SoulseekInterestRequest? request, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var item = NormalizeItem(request?.Item);
        if (item == null)
        {
            return BadRequest("item is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-interest"))
        {
            return StatusCode(429, "Soulseek interest operation rate limit exceeded.");
        }

        await DiscoveryService.AddHatedInterestAsync(item, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("hated-interests/{item}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> RemoveHatedInterest([FromRoute] string item, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var normalizedItem = NormalizeItem(item);
        if (normalizedItem == null)
        {
            return BadRequest("item is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-interest"))
        {
            return StatusCode(429, "Soulseek interest operation rate limit exceeded.");
        }

        await DiscoveryService.RemoveHatedInterestAsync(normalizedItem, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("recommendations")]
    [ProducesResponseType(typeof(RecommendationList), 200)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetRecommendations(CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-recommendations"))
        {
            return StatusCode(429, "Soulseek recommendation rate limit exceeded.");
        }

        return Ok(await DiscoveryService.GetRecommendationsAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("recommendations/global")]
    [ProducesResponseType(typeof(RecommendationList), 200)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetGlobalRecommendations(CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-recommendations"))
        {
            return StatusCode(429, "Soulseek recommendation rate limit exceeded.");
        }

        return Ok(await DiscoveryService.GetGlobalRecommendationsAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("users/{username}/interests")]
    [ProducesResponseType(typeof(UserInterests), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetUserInterests([FromRoute] string username, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var normalizedUsername = NormalizeUsername(username);
        if (normalizedUsername == null)
        {
            return BadRequest("username is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-user-interests"))
        {
            return StatusCode(429, "Soulseek user-interest rate limit exceeded.");
        }

        return Ok(await DiscoveryService.GetUserInterestsAsync(normalizedUsername, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("users/similar")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SimilarUser>), 200)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetSimilarUsers(CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-similar-users"))
        {
            return StatusCode(429, "Soulseek similar-user rate limit exceeded.");
        }

        return Ok(await DiscoveryService.GetSimilarUsersAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("items/{item}/recommendations")]
    [ProducesResponseType(typeof(ItemRecommendations), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetItemRecommendations([FromRoute] string item, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var normalizedItem = NormalizeItem(item);
        if (normalizedItem == null)
        {
            return BadRequest("item is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-item-recommendations"))
        {
            return StatusCode(429, "Soulseek item-recommendation rate limit exceeded.");
        }

        return Ok(await DiscoveryService.GetItemRecommendationsAsync(normalizedItem, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("items/{item}/similar-users")]
    [ProducesResponseType(typeof(ItemSimilarUsers), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetItemSimilarUsers([FromRoute] string item, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var normalizedItem = NormalizeItem(item);
        if (normalizedItem == null)
        {
            return BadRequest("item is required");
        }

        if (!SafetyLimiter.TryConsumeSearch("soulseek-item-similar-users"))
        {
            return StatusCode(429, "Soulseek item-similar-user rate limit exceeded.");
        }

        return Ok(await DiscoveryService.GetItemSimilarUsersAsync(normalizedItem, cancellationToken).ConfigureAwait(false));
    }

    private static string? NormalizeItem(string? item)
    {
        item = item?.Trim();
        return string.IsNullOrWhiteSpace(item) ? null : item;
    }

    private static string? NormalizeUsername(string? username)
    {
        username = username?.Trim();
        return string.IsNullOrWhiteSpace(username) ? null : username;
    }
}

public sealed class SoulseekInterestRequest
{
    public string? Item { get; set; }
}
