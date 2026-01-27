// <copyright file="SharesController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing.API;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.Identity;
using slskd.Sharing;
using slskd.Transfers.Downloads;
using Soulseek;

/// <summary>Share-grant CRUD, token, and manifest. "Shares" = grants of a collection to a user/group. Requires Feature.CollectionsSharing (or Streaming for manifest with token).</summary>
[ApiController]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/share-grants")]
[ValidateCsrfForCookiesOnly]
[Produces("application/json")]
[Consumes("application/json")]
public class SharesController : ControllerBase
{
    private readonly ISharingService _sharing;
    private readonly IShareTokenService _tokens;
    private readonly ILogger<SharesController> _log;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISoulseekClient? _soulseekClient;
    private readonly slskd.Shares.IShareService? _shareService;
    private readonly slskd.Transfers.Downloads.IDownloadService? _downloadService;

    public SharesController(ISharingService sharing, IShareTokenService tokens, ILogger<SharesController> log, IOptionsMonitor<slskd.Options> options, IServiceProvider serviceProvider, ISoulseekClient? soulseekClient = null, slskd.Shares.IShareService? shareService = null, slskd.Transfers.Downloads.IDownloadService? downloadService = null)
    {
        _sharing = sharing;
        _tokens = tokens;
        _log = log;
        _options = options;
        _serviceProvider = serviceProvider;
        _soulseekClient = soulseekClient;
        _shareService = shareService;
        _downloadService = downloadService;
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

    /// <summary>
    /// Gets share grants for a collection owned by the current user. This is the "outgoing shares" view (owner perspective).
    /// </summary>
    [HttpGet("by-collection/{collectionId}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<ShareGrant>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCollection([FromRoute] Guid collectionId, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        if (string.IsNullOrWhiteSpace(currentUserId)) return NotFound();
        var c = await _sharing.GetCollectionAsync(collectionId, ct);
        if (c == null || c.OwnerUserId != currentUserId) return NotFound();
        var list = await _sharing.GetShareGrantsByCollectionAsync(collectionId, ct);
        return Ok(list.ToList());
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

        // Best-effort cross-node discovery: announce incoming share to recipients via PM with token + owner endpoint
        if (_soulseekClient == null)
        {
            _log.LogWarning("[ShareGrantAnnounce] Soulseek client not available; cannot announce share {ShareId}", created.Id);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await AnnounceShareGrantAsync(created, currentUserId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[ShareGrantAnnounce] Announcement task failed for share {ShareId}", created.Id);
            }
        });

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    private async Task AnnounceShareGrantAsync(ShareGrant created, string ownerUserId, CancellationToken ct)
    {
        if (_soulseekClient == null) return;

        var web = _options.CurrentValue.Web;
        var scheme = web.Https?.Disabled != true ? "https" : "http";
        var urlBase = string.IsNullOrWhiteSpace(web.UrlBase) ? "/" : web.UrlBase;
        var basePath = urlBase == "/" ? string.Empty : "/" + urlBase.Trim('/'); // normalize: "" or "/slskd"
        // Use IPv4 loopback explicitly (Playwright request client may prefer ::1 for "localhost")
        var ownerEndpoint = web.Port > 0 ? $"{scheme}://127.0.0.1:{web.Port}{basePath}" : $"{scheme}://127.0.0.1{basePath}";

        var token = await _sharing.CreateTokenAsync(created.Id, TimeSpan.FromDays(7), ct).ConfigureAwait(false);
        var collection = await _sharing.GetCollectionAsync(created.CollectionId, ct).ConfigureAwait(false);
        var items = await _sharing.GetCollectionItemsAsync(created.CollectionId, ct).ConfigureAwait(false);

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(created.AudienceType, AudienceTypes.User, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(created.AudienceId)) recipients.Add(created.AudienceId);
        }
        else if (string.Equals(created.AudienceType, AudienceTypes.ShareGroup, StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(created.AudienceId, out var groupId))
            {
                var members = await _sharing.GetShareGroupMembersAsync(groupId, ct).ConfigureAwait(false);
                foreach (var m in members.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    recipients.Add(m);
                }
            }
        }

        recipients.Remove(ownerUserId);

        if (recipients.Count == 0)
        {
            _log.LogInformation("[ShareGrantAnnounce] No recipients for share {ShareId}", created.Id);
            return;
        }

        foreach (var recipient in recipients)
        {
            var msg = new ShareGrantAnnouncement
            {
                ShareGrantId = created.Id,
                CollectionId = created.CollectionId,
                CollectionTitle = collection?.Title,
                CollectionDescription = collection?.Description,
                CollectionType = collection?.Type,
                OwnerUserId = ownerUserId,
                OwnerEndpoint = ownerEndpoint,
                Token = token,
                RecipientUserId = recipient,
                AllowStream = created.AllowStream,
                AllowDownload = created.AllowDownload,
                AllowReshare = created.AllowReshare,
                ExpiryUtc = created.ExpiryUtc,
                MaxConcurrentStreams = created.MaxConcurrentStreams,
                MaxBitrateKbps = created.MaxBitrateKbps,
                Items = items.Select(i => new ShareGrantAnnouncementItem
                {
                    Ordinal = i.Ordinal,
                    ContentId = i.ContentId,
                    MediaKind = i.MediaKind,
                }).ToList(),
            };

            var payload = JsonSerializer.Serialize(msg);
            try
            {
                await _soulseekClient.SendPrivateMessageAsync(recipient, $"SHAREGRANT:{payload}").ConfigureAwait(false);
                _log?.LogInformation("[ShareGrantAnnounce] Sent SHAREGRANT {ShareId} to {Recipient}", created.Id, recipient);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "[ShareGrantAnnounce] Failed to send SHAREGRANT {ShareId} to {Recipient}", created.Id, recipient);
            }
        }
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
        // IMPORTANT: The web UI uses a JWT in the Authorization header. Share tokens may also be passed via query (?token=)
        // or (for non-UI clients) via Authorization: Bearer <share-token>. We must not treat a JWT as a share token.
        if (!string.IsNullOrEmpty(tokenForStream))
        {
            var claims = await _tokens.ValidateAsync(tokenForStream, ct);
            if (claims == null) return Unauthorized();
            if (claims.ShareId != id.ToString()) return NotFound();
            var m = await _sharing.GetManifestAsync(id, tokenForStream, null, ct);
            if (m == null) return NotFound();
            return Ok(m);
        }

        // If no query token was provided, do NOT attempt to interpret Authorization: Bearer <jwt> as a share token.
        // Authenticated users should use their normal JWT to access manifests for shares they can see.

        if (User?.Identity?.IsAuthenticated != true)
            return Unauthorized();
        var currentUserId = await GetCurrentUserIdAsync(ct);
        var m2 = await _sharing.GetManifestAsync(id, null, currentUserId, ct);
        if (m2 == null) return NotFound();
        return Ok(m2);
    }

    /// <summary>
    /// Backfill (download) all items from a shared collection. Requires AllowDownload policy.
    /// Supports both HTTP downloads (for cross-node shares, no Soulseek required) and Soulseek downloads (if owner is a Soulseek user).
    /// </summary>
    [HttpPost("{id}/backfill")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(BackfillResponse), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Backfill([FromRoute] Guid id, CancellationToken ct)
    {
        if (!CollectionsEnabled) return NotFound();

        var currentUserId = await GetCurrentUserIdAsync(ct);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        // Get the share grant
        var accessible = await _sharing.GetShareGrantsAccessibleByUserAsync(currentUserId, ct).ConfigureAwait(false);
        var grant = accessible.FirstOrDefault(x => x.Id == id);
        if (grant == null) return NotFound();

        // Check AllowDownload policy
        if (!grant.AllowDownload)
            return StatusCode(403, "Download not allowed for this share");

        // Get the collection and manifest
        var collection = await _sharing.GetCollectionAsync(grant.CollectionId, ct).ConfigureAwait(false);
        if (collection == null) return NotFound();

        var manifest = await _sharing.GetManifestAsync(id, grant.ShareToken, currentUserId, ct).ConfigureAwait(false);
        if (manifest == null || manifest.Items == null || manifest.Items.Count == 0)
            return Ok(new BackfillResponse { Enqueued = 0, Failed = 0, Message = "No items to backfill" });

        var enqueued = 0;
        var failed = 0;
        var errors = new List<string>();

        // Determine download method: HTTP (cross-node) or Soulseek (same network)
        var ownerEndpoint = grant.OwnerEndpoint;
        var ownerUsername = collection.OwnerUserId;
        var useHttpDownload = !string.IsNullOrWhiteSpace(ownerEndpoint) && !string.IsNullOrWhiteSpace(grant.ShareToken);

        if (useHttpDownload)
        {
            // HTTP download from owner's endpoint (cross-node, no Soulseek required)
            _log.LogInformation("[Backfill] Using HTTP download from {OwnerEndpoint} for {Count} items", ownerEndpoint, manifest.Items.Count);
            
            var downloadsDir = _options.CurrentValue.Directories.Downloads;
            if (string.IsNullOrWhiteSpace(downloadsDir) || !System.IO.Directory.Exists(downloadsDir))
            {
                return StatusCode(500, "Downloads directory not configured or does not exist");
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            
            // Add authorization header with share token if available
            if (!string.IsNullOrWhiteSpace(grant.ShareToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", grant.ShareToken);
            }

            foreach (var item in manifest.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ContentId) || string.IsNullOrWhiteSpace(item.StreamUrl))
                {
                    failed++;
                    errors.Add($"Item missing ContentId or StreamUrl");
                    continue;
                }

                try
                {
                    // Use streamUrl from manifest (already includes owner endpoint and token)
                    var streamUrl = item.StreamUrl;
                    if (!streamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ownerEndpoint))
                    {
                        // Relative URL - prepend owner endpoint
                        streamUrl = $"{ownerEndpoint.TrimEnd('/')}{streamUrl}";
                    }

                    _log.LogDebug("[Backfill] Downloading {ContentId} from {Url}", item.ContentId, streamUrl);

                    // Download file
                    using var response = await httpClient.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    // Generate safe filename from ContentId
                    var safeFilename = item.ContentId.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
                    var extension = item.MediaKind?.ToLowerInvariant() switch
                    {
                        "audio" => ".mp3", // Default, could be improved with content-type detection
                        "video" => ".mp4",
                        "image" => ".jpg",
                        _ => ".bin"
                    };
                    var filename = $"{safeFilename}{extension}";
                    var filePath = Path.Combine(downloadsDir, filename);

                    // Ensure unique filename
                    var counter = 1;
                    while (System.IO.File.Exists(filePath))
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                        filePath = Path.Combine(downloadsDir, $"{nameWithoutExt}_{counter}{extension}");
                        counter++;
                    }

                    // Save file
                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    using var fileStream = new System.IO.FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream, ct).ConfigureAwait(false);

                    enqueued++;
                    _log.LogInformation("[Backfill] Downloaded {ContentId} to {Path}", item.ContentId, filePath);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[Backfill] Failed to download {ContentId}", item.ContentId);
                    failed++;
                    errors.Add($"Failed to download {item.ContentId.Substring(0, Math.Min(16, item.ContentId.Length))}...: {ex.Message}");
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(ownerUsername) && _downloadService != null)
        {
            // Soulseek download (same network, owner is a Soulseek user)
            _log.LogInformation("[Backfill] Using Soulseek download from {Owner} for {Count} items", ownerUsername, manifest.Items.Count);

            if (_shareService == null)
                return StatusCode(503, "Share service not available");

            // Resolve each ContentId to filename and enqueue download
            var repo = _shareService.GetLocalRepository();
            var filesToDownload = new List<(string Filename, long Size)>();

            foreach (var item in manifest.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ContentId))
                {
                    failed++;
                    errors.Add($"Item has no ContentId");
                    continue;
                }

                try
                {
                    // Try to resolve ContentId to masked filename (what the owner is sharing)
                    var contentItem = repo.FindContentItem(item.ContentId);
                    if (contentItem == null)
                    {
                        _log.LogDebug("[Backfill] ContentId {ContentId} not found locally, skipping", item.ContentId);
                        failed++;
                        errors.Add($"ContentId {item.ContentId.Substring(0, Math.Min(16, item.ContentId.Length))}... not found locally");
                        continue;
                    }

                    var fileInfo = repo.FindFileInfo(contentItem.Value.MaskedFilename);
                    if (string.IsNullOrEmpty(fileInfo.Filename) || fileInfo.Size <= 0)
                    {
                        failed++;
                        errors.Add($"Could not resolve filename for ContentId {item.ContentId.Substring(0, Math.Min(16, item.ContentId.Length))}...");
                        continue;
                    }

                    // Use masked filename (remote path) for download
                    filesToDownload.Add((contentItem.Value.MaskedFilename, fileInfo.Size));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[Backfill] Failed to resolve ContentId {ContentId}", item.ContentId);
                    failed++;
                    errors.Add($"Error resolving {item.ContentId.Substring(0, Math.Min(16, item.ContentId.Length))}...: {ex.Message}");
                }
            }

            // Enqueue all downloads in one batch
            if (filesToDownload.Count > 0)
            {
                try
                {
                    var (enqueuedTransfers, failedFiles) = await _downloadService.EnqueueAsync(ownerUsername, filesToDownload, ct).ConfigureAwait(false);
                    enqueued = enqueuedTransfers.Count;
                    failed += failedFiles.Count;
                    if (failedFiles.Count > 0)
                    {
                        errors.AddRange(failedFiles);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[Backfill] Failed to enqueue downloads from {Owner}", ownerUsername);
                    return StatusCode(500, $"Failed to enqueue downloads: {ex.Message}");
                }
            }
        }
        else
        {
            return BadRequest("Cannot backfill: owner endpoint and token not available for HTTP download, and owner username or download service not available for Soulseek download");
        }

        return Ok(new BackfillResponse
        {
            Enqueued = enqueued,
            Failed = failed,
            Total = manifest.Items.Count,
            Message = failed == 0
                ? $"Successfully downloaded {enqueued} files"
                : $"Downloaded {enqueued}, failed {failed}",
            Errors = errors.Count > 0 ? errors : null,
        });
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

public class BackfillResponse
{
    public int Enqueued { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? Errors { get; set; }
}
