// <copyright file="SharesController.cs" company="slskdN Team">
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

/// <summary>Share-grant CRUD, token, and manifest. "Shares" = grants of a collection to a user/group. Requires Feature.CollectionsSharing (or Streaming for manifest with token).</summary>
[ApiController]
[Route("api/v0/shares")]
[ValidateCsrfForCookiesOnly]
[Produces("application/json")]
[Consumes("application/json")]
public class SharesController : ControllerBase
{
    private readonly ISharingService _sharing;
    private readonly IShareTokenService _tokens;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly IServiceProvider _serviceProvider;

    public SharesController(ISharingService sharing, IShareTokenService tokens, IOptionsMonitor<slskd.Options> options, IServiceProvider serviceProvider)
    {
        _sharing = sharing;
        _tokens = tokens;
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
    private bool CollectionsEnabled => _options.CurrentValue.Feature.CollectionsSharing;

    [HttpGet]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<ShareGrant>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        // If we can't determine user identity, return empty list instead of error
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Ok(new List<ShareGrant>());
        var list = await _sharing.GetShareGrantsAccessibleByUserAsync(currentUserId, ct);
        return Ok(list);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(ShareGrant), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGrantAsync(id, ct);
        if (g == null) return NotFound();
        var accessible = await _sharing.GetShareGrantsAccessibleByUserAsync(currentUserId, ct);
        if (accessible.All(x => x.Id != id)) return NotFound();
        return Ok(g);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(ShareGrant), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateShareGrantRequest req, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        if (req.CollectionId == default) 
            return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "CollectionId is required.", Detail = "CollectionId is required." });
        if (string.IsNullOrWhiteSpace(req.AudienceType) || string.IsNullOrWhiteSpace(req.AudienceId)) 
            return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "AudienceType and AudienceId are required.", Detail = "AudienceType and AudienceId are required." });
        var currentUserId = await GetCurrentUserIdAsync(ct);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Title = "User identity not available", Detail = "Cannot create share: user identity not available. Please configure Soulseek username or enable Identity & Friends." });
        var c = await _sharing.GetCollectionAsync(req.CollectionId, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var g = new ShareGrant
        {
            CollectionId = req.CollectionId,
            AudienceType = req.AudienceType.Trim(),
            AudienceId = req.AudienceId.Trim(),
            AudiencePeerId = req.AudiencePeerId?.Trim(),
            AllowStream = req.AllowStream,
            AllowDownload = req.AllowDownload,
            AllowReshare = req.AllowReshare,
            ExpiryUtc = req.ExpiryUtc,
            MaxConcurrentStreams = req.MaxConcurrentStreams <= 0 ? 1 : req.MaxConcurrentStreams,
            MaxBitrateKbps = req.MaxBitrateKbps,
        };
        var created = await _sharing.CreateShareGrantAsync(g, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateShareGrantRequest req, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGrantAsync(id, ct);
        if (g == null) return NotFound();
        var c = await _sharing.GetCollectionAsync(g.CollectionId, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        if (req.AllowStream != null) g.AllowStream = req.AllowStream.Value;
        if (req.AllowDownload != null) g.AllowDownload = req.AllowDownload.Value;
        if (req.AllowReshare != null) g.AllowReshare = req.AllowReshare.Value;
        if (req.ExpiryUtc != null) g.ExpiryUtc = req.ExpiryUtc;
        if (req.MaxConcurrentStreams != null) g.MaxConcurrentStreams = req.MaxConcurrentStreams.Value;
        if (req.MaxBitrateKbps != null) g.MaxBitrateKbps = req.MaxBitrateKbps;
        await _sharing.UpdateShareGrantAsync(g, ct);
        return Ok(g);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGrantAsync(id, ct);
        if (g == null) return NotFound();
        var c = await _sharing.GetCollectionAsync(g.CollectionId, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        await _sharing.DeleteShareGrantAsync(id, ct);
        return NoContent();
    }

    /// <summary>Create a share token for this grant. Caller must own the collection. Requires Sharing:TokenSigningKey.</summary>
    [HttpPost("{id}/token")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(TokenResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CreateToken([FromRoute] Guid id, [FromBody] CreateTokenRequest req, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var g = await _sharing.GetShareGrantAsync(id, ct);
        if (g == null) return NotFound();
        var c = await _sharing.GetCollectionAsync(g.CollectionId, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var expiresIn = req.ExpiresInSeconds.HasValue && req.ExpiresInSeconds.Value > 0
            ? TimeSpan.FromSeconds(req.ExpiresInSeconds.Value)
            : TimeSpan.FromHours(24);
        try
        {
            var token = await _sharing.CreateTokenAsync(id, expiresIn, ct);
            return Ok(new TokenResponse { Token = token, ExpiresInSeconds = (int)expiresIn.TotalSeconds });
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Token signing is not configured (Sharing:TokenSigningKey).");
        }
    }

    /// <summary>Get manifest. Auth: normal (requires Authorize) or ?token= or Authorization: Bearer. If AllowStream=false, streamUrl is omitted.</summary>
    [HttpGet("{id}/manifest")]
    [ProducesResponseType(typeof(ShareManifestDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetManifest([FromRoute] Guid id, [FromQuery] string? token, CancellationToken ct)
    {
        var collectionsOrStreaming = CollectionsEnabled || _options.CurrentValue.Feature.Streaming;
        if (!collectionsOrStreaming) return NotFound();

        string? tokenForStream = token;
        if (string.IsNullOrEmpty(tokenForStream))
        {
            var auth = Request.Headers.Authorization.FirstOrDefault();
            if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                tokenForStream = auth.Substring("Bearer ".Length).Trim();
        }

        if (!string.IsNullOrEmpty(tokenForStream))
        {
            var claims = await _tokens.ValidateAsync(tokenForStream, ct);
            if (claims == null) return Unauthorized();
            if (claims.ShareId != id.ToString()) return NotFound();
            var m = await _sharing.GetManifestAsync(id, tokenForStream, null, ct);
            if (m == null) return NotFound();
            return Ok(m);
        }

        if (User?.Identity?.IsAuthenticated != true)
            return Unauthorized();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var m2 = await _sharing.GetManifestAsync(id, null, currentUserId, ct);
        if (m2 == null) return NotFound();
        return Ok(m2);
    }
}

public class CreateShareGrantRequest
{
    [Required] public Guid CollectionId { get; set; }
    [Required] public string? AudienceType { get; set; }
    [Required] public string? AudienceId { get; set; }
    /// <summary>Contact PeerId when AudienceType is User and audience is Contact-based (Identity & Friends).</summary>
    public string? AudiencePeerId { get; set; }
    public bool AllowStream { get; set; } = true;
    public bool AllowDownload { get; set; } = true;
    public bool AllowReshare { get; set; }
    public DateTime? ExpiryUtc { get; set; }
    public int MaxConcurrentStreams { get; set; } = 1;
    public int? MaxBitrateKbps { get; set; }
}

public class UpdateShareGrantRequest
{
    public bool? AllowStream { get; set; }
    public bool? AllowDownload { get; set; }
    public bool? AllowReshare { get; set; }
    public DateTime? ExpiryUtc { get; set; }
    public int? MaxConcurrentStreams { get; set; }
    public int? MaxBitrateKbps { get; set; }
}

public class CreateTokenRequest { public int? ExpiresInSeconds { get; set; } }
public class TokenResponse { public string Token { get; set; } = string.Empty; public int ExpiresInSeconds { get; set; } }
