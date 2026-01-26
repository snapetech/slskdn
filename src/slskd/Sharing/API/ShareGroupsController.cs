// <copyright file="ShareGroupsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing.API;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.Identity;
using slskd.Sharing;

/// <summary>Share group CRUD and members. Requires Feature.CollectionsSharing.</summary>
[ApiController]
[Route("api/v0/sharegroups")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
[Produces("application/json")]
[Consumes("application/json")]
public class ShareGroupsController : ControllerBase
{
    private readonly ISharingService _sharing;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly IServiceProvider _serviceProvider;

    public ShareGroupsController(ISharingService sharing, IOptionsMonitor<slskd.Options> options, IServiceProvider serviceProvider)
    {
        _sharing = sharing;
        _options = options;
        _serviceProvider = serviceProvider;
    }

    private async Task<string> GetCurrentUserIdAsync(CancellationToken ct = default)
    {
        // Prefer Soulseek username if available
        var soulseekUsername = _options.CurrentValue.Soulseek.Username;
        if (!string.IsNullOrWhiteSpace(soulseekUsername))
            return soulseekUsername;

        // Fall back to Identity & Friends PeerId
        var profileService = _serviceProvider.GetService<IProfileService>();
        if (profileService != null)
        {
            try
            {
                var profile = await profileService.GetMyProfileAsync(ct);
                if (!string.IsNullOrWhiteSpace(profile.PeerId))
                    return profile.PeerId;
            }
            catch
            {
                // If profile service fails, continue with empty string
            }
        }

        return string.Empty;
    }
    private bool Enabled => _options.CurrentValue.Feature.CollectionsSharing;

    [HttpGet]
    [ProducesResponseType(typeof(List<ShareGroup>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        // If we can't determine user identity, return empty list instead of error
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Ok(new List<ShareGroup>());
        var list = await _sharing.GetShareGroupsByOwnerAsync(currentUserId, ct);
        return Ok(list);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ShareGroup), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGroupAsync(id, ct);
        if (g == null) return NotFound();
        if (g.OwnerUserId != currentUserId) return NotFound();
        return Ok(g);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ShareGroup), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateShareGroupRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = new ShareGroup { Name = req.Name.Trim(), OwnerUserId = currentUserId };
        var created = await _sharing.CreateShareGroupAsync(g, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateShareGroupRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGroupAsync(id, ct);
        if (g == null || g.OwnerUserId != currentUserId) return NotFound();
        g.Name = req.Name?.Trim() ?? g.Name;
        await _sharing.UpdateShareGroupAsync(g, ct);
        return Ok(g);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGroupAsync(id, ct);
        if (g == null || g.OwnerUserId != currentUserId) return NotFound();
        await _sharing.DeleteShareGroupAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id}/members")]
    [ProducesResponseType(typeof(List<string>), 200)]
    [ProducesResponseType(typeof(List<ShareGroupMemberInfo>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMembers([FromRoute] Guid id, [FromQuery] bool detailed = false, CancellationToken ct = default)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGroupAsync(id, ct);
        if (g == null || g.OwnerUserId != currentUserId) return NotFound();

        if (detailed)
        {
            var members = await _sharing.GetShareGroupMemberInfosAsync(id, ct);
            return Ok(members);
        }
        else
        {
            var members = await _sharing.GetShareGroupMembersAsync(id, ct);
            return Ok(members);
        }
    }

    [HttpPost("{id}/members")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddMember([FromRoute] Guid id, [FromBody] AddMemberRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGroupAsync(id, ct);
        if (g == null || g.OwnerUserId != currentUserId) return NotFound();

        // Support both UserId (legacy) and PeerId/ContactId (Identity & Friends)
        if (!string.IsNullOrWhiteSpace(req.PeerId))
        {
            await _sharing.AddShareGroupMemberByPeerIdAsync(id, req.PeerId!.Trim(), ct);
        }
        else if (!string.IsNullOrWhiteSpace(req.UserId))
        {
            await _sharing.AddShareGroupMemberAsync(id, req.UserId!.Trim(), ct);
        }
        else
        {
            return BadRequest("UserId or PeerId is required.");
        }

        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveMember([FromRoute] Guid id, [FromRoute] string userId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGroupAsync(id, ct);
        if (g == null || g.OwnerUserId != currentUserId) return NotFound();
        await _sharing.RemoveShareGroupMemberAsync(id, userId, ct);
        return NoContent();
    }
}

public class CreateShareGroupRequest { [Required] public string? Name { get; set; } }
public class UpdateShareGroupRequest { public string? Name { get; set; } }
public class AddMemberRequest 
{ 
    /// <summary>Soulseek username (legacy).</summary>
    public string? UserId { get; set; }
    /// <summary>Contact PeerId (Identity & Friends). Takes precedence over UserId when set.</summary>
    public string? PeerId { get; set; }
}
