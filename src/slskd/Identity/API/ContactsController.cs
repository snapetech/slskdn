// <copyright file="ContactsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity.API;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.Identity;

/// <summary>Contacts endpoints: CRUD, from-invite, from-discovery, nearby. Requires Feature.IdentityFriends.</summary>
[ApiController]
[Route("api/v{version:apiVersion}/contacts")]
[ApiVersion("0")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
[Produces("application/json")]
[Consumes("application/json")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contacts;
    private readonly IProfileService _profile;
    private readonly IOptionsMonitor<slskd.Options> _options;

    public ContactsController(IContactService contacts, IProfileService profile, IOptionsMonitor<slskd.Options> options)
    {
        _contacts = contacts ?? throw new ArgumentNullException(nameof(contacts));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private bool Enabled => _options.CurrentValue.Feature.IdentityFriends;

    /// <summary>List all contacts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Contact>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var list = await _contacts.GetAllAsync(ct).ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>Get a contact by ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Contact), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var c = await _contacts.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (c == null) return NotFound();
        return Ok(c);
    }

    /// <summary>Add a contact from an invite link.</summary>
    [HttpPost("from-invite")]
    [ProducesResponseType(typeof(Contact), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddFromInvite([FromBody] AddFromInviteRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (string.IsNullOrWhiteSpace(req.InviteLink)) return BadRequest("InviteLink is required.");
        if (string.IsNullOrWhiteSpace(req.Nickname)) return BadRequest("Nickname is required.");

        try
        {
            var base64 = req.InviteLink.Replace("slskdn://invite/", "").Replace('-', '+').Replace('_', '/');
            var padding = 4 - (base64.Length % 4);
            if (padding < 4) base64 += new string('=', padding);
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var invite = JsonSerializer.Deserialize<FriendInvite>(json);
            if (invite == null || invite.Profile == null) return BadRequest("Invalid invite format.");
            if (invite.ExpiresAt < DateTimeOffset.UtcNow) return BadRequest("Invite expired.");
            if (!_profile.VerifyProfile(invite.Profile)) return BadRequest("Invalid profile signature.");

            var contact = await _contacts.AddAsync(invite.Profile.PeerId, req.Nickname.Trim(), true, ct).ConfigureAwait(false);
            if (invite.Profile.Endpoints != null && invite.Profile.Endpoints.Count > 0)
            {
                contact.CachedEndpointsJson = JsonSerializer.Serialize(invite.Profile.Endpoints);
                await _contacts.UpdateAsync(contact, ct).ConfigureAwait(false);
            }
            return CreatedAtAction(nameof(Get), new { id = contact.Id }, contact);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to decode invite: {ex.Message}");
        }
    }

    /// <summary>Add a contact from LAN discovery (by PeerId, after fetching profile).</summary>
    [HttpPost("from-discovery")]
    [ProducesResponseType(typeof(Contact), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddFromDiscovery([FromBody] AddFromDiscoveryRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (string.IsNullOrWhiteSpace(req.PeerId)) return BadRequest("PeerId is required.");
        if (string.IsNullOrWhiteSpace(req.Nickname)) return BadRequest("Nickname is required.");

        var profile = await _profile.GetProfileAsync(req.PeerId, ct).ConfigureAwait(false);
        if (profile == null) return NotFound("Profile not found.");
        if (!_profile.VerifyProfile(profile)) return BadRequest("Invalid profile signature.");

        var contact = await _contacts.AddAsync(profile.PeerId, req.Nickname.Trim(), true, ct).ConfigureAwait(false);
        if (profile.Endpoints != null && profile.Endpoints.Count > 0)
        {
            contact.CachedEndpointsJson = JsonSerializer.Serialize(profile.Endpoints);
            await _contacts.UpdateAsync(contact, ct).ConfigureAwait(false);
        }
        return CreatedAtAction(nameof(Get), new { id = contact.Id }, contact);
    }

    /// <summary>Update contact nickname.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Contact), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateContactRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var c = await _contacts.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (c == null) return NotFound();
        if (req.Nickname != null) c.Nickname = req.Nickname.Trim();
        await _contacts.UpdateAsync(c, ct).ConfigureAwait(false);
        return Ok(c);
    }

    /// <summary>Remove a contact.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var ok = await _contacts.DeleteAsync(id, ct).ConfigureAwait(false);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>Browse nearby peers (mDNS).</summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(List<DiscoveredPeer>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetNearby(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var discovery = HttpContext.RequestServices.GetService<ILanDiscoveryService>();
        if (discovery == null) return Ok(new List<DiscoveredPeer>());
        var peers = await discovery.BrowseAsync(ct).ConfigureAwait(false);
        return Ok(peers);
    }
}

public class AddFromInviteRequest
{
    [Required] public string? InviteLink { get; set; }
    [Required] public string? Nickname { get; set; }
}

public class AddFromDiscoveryRequest
{
    [Required] public string? PeerId { get; set; }
    [Required] public string? Nickname { get; set; }
}

public class UpdateContactRequest
{
    public string? Nickname { get; set; }
}
