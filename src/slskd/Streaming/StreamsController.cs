// <copyright file="StreamsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.Sharing;

/// <summary>GET /api/v0/streams/{contentId} — range, token or normal auth, single-range only. Requires Feature.Streaming.</summary>
[ApiController]
[Route("api/v{version:apiVersion}/streams")]
[ApiVersion("0")]
[AllowAnonymous]
[ValidateCsrfForCookiesOnly]
[Produces("application/octet-stream")]
public class StreamsController : ControllerBase
{
    /// <summary>Max concurrent streams per normal (non-token) user when using normal auth.</summary>
    private const int NormalUserMaxConcurrentStreams = 5;

    private readonly IContentLocator _locator;
    private readonly IShareTokenService _tokens;
    private readonly ISharingService _sharing;
    private readonly IStreamSessionLimiter _limiter;
    private readonly IOptionsMonitor<slskd.Options> _options;

    public StreamsController(
        IContentLocator locator,
        IShareTokenService tokens,
        ISharingService sharing,
        IStreamSessionLimiter limiter,
        IOptionsMonitor<slskd.Options> options)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _sharing = sharing ?? throw new ArgumentNullException(nameof(sharing));
        _limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private bool StreamingEnabled => _options.CurrentValue.Feature.Streaming;
    private string CurrentUserId => _options.CurrentValue.Soulseek.Username ?? string.Empty;

    /// <summary>Stream content by ID. Auth: ?token= or Authorization: Bearer (share:token) or normal [Authorize]. Single byte-range only; multi-range returns 400.</summary>
    [HttpGet("{contentId}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> Get([FromRoute] string contentId, [FromQuery] string? token, CancellationToken ct)
    {
        if (!StreamingEnabled) return NotFound();

        // Reject multi-range before any File/range handling
        var rangeHeader = Request.Headers.Range.FirstOrDefault();
        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.IndexOf(',') >= 0)
            return BadRequest("Multiple byte ranges are not supported.");

        ShareTokenClaims? claims = null;
        var tokenRaw = token;
        if (string.IsNullOrEmpty(tokenRaw))
        {
            var auth = Request.Headers.Authorization.FirstOrDefault();
            if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                tokenRaw = auth.Substring("Bearer ".Length).Trim();
        }
        if (!string.IsNullOrEmpty(tokenRaw))
        {
            var toValidate = tokenRaw.StartsWith("share:", StringComparison.OrdinalIgnoreCase)
                ? tokenRaw.Substring("share:".Length)
                : tokenRaw;
            claims = await _tokens.ValidateAsync(toValidate, ct);
            if (claims == null) return Unauthorized();
            if (!claims.AllowStream) return Unauthorized();
            if (!Guid.TryParse(claims.CollectionId, out var collectionId)) return NotFound();
            var items = await _sharing.GetCollectionItemsAsync(collectionId, ct);
            if (items.All(x => !string.Equals(x.ContentId, contentId, StringComparison.Ordinal))) return NotFound();
        }
        else
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
        }

        var resolved = _locator.Resolve(contentId, ct);
        if (resolved == null) return NotFound();

        string limiterKey;
        int maxConcurrent;
        if (claims != null)
        {
            limiterKey = claims.ShareId;
            maxConcurrent = claims.MaxConcurrentStreams <= 0 ? 1 : claims.MaxConcurrentStreams;
        }
        else
        {
            limiterKey = "user:" + CurrentUserId;
            maxConcurrent = NormalUserMaxConcurrentStreams;
        }

        if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
            return StatusCode(429, "Too many concurrent streams.");

        Stream stream;
        try
        {
            stream = new FileStream(resolved.AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            _limiter.Release(limiterKey);
            return NotFound();
        }

        var wrapped = new ReleaseOnDisposeStream(stream, () => _limiter.Release(limiterKey));
        return File(wrapped, resolved.ContentType, enableRangeProcessing: true);
    }
}
