// <copyright file="CollectionsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing.API;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.Identity;
using slskd.Sharing;

/// <summary>Collection CRUD and items. Requires Feature.CollectionsSharing.</summary>
[ApiController]
[Route("api/v0/collections")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
[Produces("application/json")]
[Consumes("application/json")]
public class CollectionsController : ControllerBase
{
    private readonly ISharingService _sharing;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly IServiceProvider _serviceProvider;

    public CollectionsController(ISharingService sharing, IOptionsMonitor<slskd.Options> options, IServiceProvider serviceProvider)
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
    [ProducesResponseType(typeof(List<Collection>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        // If we can't determine user identity, return empty list instead of error
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Ok(new List<Collection>());
        var list = await _sharing.GetCollectionsByOwnerAsync(currentUserId, ct);
        return Ok(list);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Collection), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        return Ok(c);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Collection), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var t = req.Type?.Trim() == CollectionType.Playlist ? CollectionType.Playlist : CollectionType.ShareList;
        var c = new Collection { Title = req.Title.Trim(), Description = req.Description?.Trim(), Type = t, OwnerUserId = currentUserId };
        var created = await _sharing.CreateCollectionAsync(c, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCollectionRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        if (req.Title != null) c.Title = req.Title.Trim();
        if (req.Description != null) c.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        if (req.Type != null) c.Type = req.Type.Trim() == CollectionType.Playlist ? CollectionType.Playlist : CollectionType.ShareList;
        await _sharing.UpdateCollectionAsync(c, ct);
        return Ok(c);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        await _sharing.DeleteCollectionAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id}/items")]
    [ProducesResponseType(typeof(List<CollectionItem>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetItems([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var items = await _sharing.GetCollectionItemsAsync(id, ct);
        return Ok(items);
    }

    [HttpPost("{id}/items")]
    [ProducesResponseType(typeof(CollectionItem), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddItem([FromRoute] Guid id, [FromBody] AddCollectionItemRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (string.IsNullOrWhiteSpace(req.ContentId)) return BadRequest("ContentId is required.");
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var item = new CollectionItem { CollectionId = id, ContentId = req.ContentId.Trim(), MediaKind = req.MediaKind?.Trim(), ContentHash = req.ContentHash?.Trim() };
        var created = await _sharing.AddCollectionItemAsync(item, ct);
        return CreatedAtAction(nameof(GetItems), new { id }, created);
    }

    [HttpPut("{id}/items/{itemId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateItem([FromRoute] Guid id, [FromRoute] Guid itemId, [FromBody] UpdateCollectionItemRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var items = await _sharing.GetCollectionItemsAsync(id, ct);
        var it = items.FirstOrDefault(x => x.Id == itemId);
        if (it == null) return NotFound();
        it.ContentId = req.ContentId ?? it.ContentId;
        it.MediaKind = req.MediaKind;
        it.ContentHash = req.ContentHash;
        await _sharing.UpdateCollectionItemAsync(it, ct);
        return Ok(it);
    }

    [HttpDelete("{id}/items/{itemId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveItem([FromRoute] Guid id, [FromRoute] Guid itemId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var ok = await _sharing.RemoveCollectionItemAsync(itemId, ct);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpPost("{id}/items/reorder")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReorderItems([FromRoute] Guid id, [FromBody] ReorderRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (req.ItemIds == null || req.ItemIds.Count == 0) return BadRequest("ItemIds is required.");
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var c = await _sharing.GetCollectionAsync(id, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        await _sharing.ReorderCollectionItemsAsync(id, req.ItemIds, ct);
        return NoContent();
    }
}

public class CreateCollectionRequest { [Required] public string? Title { get; set; } public string? Description { get; set; } public string? Type { get; set; } }
public class UpdateCollectionRequest { public string? Title { get; set; } public string? Description { get; set; } public string? Type { get; set; } }
public class AddCollectionItemRequest { [Required] public string? ContentId { get; set; } public string? MediaKind { get; set; } public string? ContentHash { get; set; } }
public class UpdateCollectionItemRequest { public string? ContentId { get; set; } public string? MediaKind { get; set; } public string? ContentHash { get; set; } }
public class ReorderRequest { [Required] public List<Guid>? ItemIds { get; set; } }
