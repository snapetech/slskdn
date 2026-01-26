// <copyright file="ProfileController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity.API;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.Identity;

/// <summary>Profile endpoints: own profile, get by PeerId, create invite. Requires Feature.IdentityFriends.</summary>
[ApiController]
[Route("api/v{version:apiVersion}/profile")]
[ApiVersion("0")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
[Produces("application/json")]
[Consumes("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profile;
    private readonly IOptionsMonitor<slskd.Options> _options;

    public ProfileController(IProfileService profile, IOptionsMonitor<slskd.Options> options)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private bool Enabled => _options.CurrentValue.Feature.IdentityFriends;

    /// <summary>Get this peer's own profile (signed).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PeerProfile), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var p = await _profile.GetMyProfileAsync(ct).ConfigureAwait(false);
        return Ok(p);
    }

    /// <summary>Update this peer's profile (re-signs).</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(PeerProfile), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("DisplayName is required.");
        var p = await _profile.UpdateMyProfileAsync(
            req.DisplayName.Trim(),
            req.Avatar?.Trim(),
            req.Capabilities ?? 0,
            req.Endpoints ?? new List<PeerEndpoint>(),
            ct).ConfigureAwait(false);
        return Ok(p);
    }

    /// <summary>Get a peer's profile by PeerId (public, no auth required).</summary>
    [HttpGet("{peerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PeerProfile), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProfile([FromRoute] string peerId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var p = await _profile.GetProfileAsync(peerId, ct).ConfigureAwait(false);
        if (p == null) return NotFound();
        return Ok(p);
    }

    /// <summary>Generate an invite link/QR.</summary>
    [HttpPost("invite")]
    [ProducesResponseType(typeof(InviteResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CreateInvite([FromBody] CreateInviteRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        try
        {
            var profile = await _profile.GetMyProfileAsync(ct).ConfigureAwait(false);
            if (profile == null || string.IsNullOrWhiteSpace(profile.PeerId))
            {
                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "Profile not available", Detail = "Cannot create invite: profile not available. Please ensure Identity & Friends is properly configured." };
                var result = new BadRequestObjectResult(problem);
                result.ContentTypes.Clear();
                result.ContentTypes.Add("application/problem+json");
                return result;
            }
            var invite = new FriendInvite
            {
                InviteVersion = 1,
                Profile = profile,
                Nonce = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTimeOffset.UtcNow.Add(req.ExpiresInHours > 0 ? TimeSpan.FromHours(req.ExpiresInHours) : TimeSpan.FromHours(24))
            };
            var json = System.Text.Json.JsonSerializer.Serialize(invite);
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var link = $"slskdn://invite/{base64}";
            return Ok(new InviteResponse { InviteLink = link, FriendCode = _profile.GetFriendCode(profile.PeerId) });
        }
        catch (Exception ex)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "Failed to create invite", Detail = $"Cannot create invite: {ex.Message}" };
            var result = new BadRequestObjectResult(problem);
            result.ContentTypes.Clear();
            result.ContentTypes.Add("application/problem+json");
            return result;
        }
    }
}

public class UpdateProfileRequest
{
    [Required] public string? DisplayName { get; set; }
    public string? Avatar { get; set; }
    public int? Capabilities { get; set; }
    public List<PeerEndpoint>? Endpoints { get; set; }
}

public class CreateInviteRequest
{
    public int ExpiresInHours { get; set; } = 24;
}

public class InviteResponse
{
    public string InviteLink { get; set; } = string.Empty;
    public string FriendCode { get; set; } = string.Empty;
}
