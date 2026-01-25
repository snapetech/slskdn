// <copyright file="ActivityPubController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NSec.Cryptography;
    using slskd.Core.Security;
    using slskd.Mesh;
    using slskd.Mesh.Transport;
    using slskd.SocialFederation;

    /// <summary>
    ///     ActivityPub protocol endpoints.
    /// </summary>
    /// <remarks>
    ///     T-FED01: Core ActivityPub server implementation.
    ///     Handles actor documents, inbox, and outbox endpoints.
    /// </remarks>
    [ApiController]
    [Route("actors")]
    [AllowAnonymous]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class ActivityPubController : ControllerBase
    {
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly IActivityPubKeyStore _keyStore;
        private readonly IHttpSignatureKeyFetcher _keyFetcher;
        private readonly LibraryActorService _libraryActorService;
        private readonly ILogger<ActivityPubController> _logger;
        private readonly int _maxPayload;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ActivityPubController"/> class.
        /// </summary>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="keyFetcher">SSRF-safe key fetcher for HTTP Signature verification. PR-14.</param>
        /// <param name="libraryActorService">The library actor service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="meshOptions">Optional mesh options for payload size limit (§8).</param>
        public ActivityPubController(
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IActivityPubKeyStore keyStore,
            IHttpSignatureKeyFetcher keyFetcher,
            LibraryActorService libraryActorService,
            ILogger<ActivityPubController> logger,
            IOptions<MeshOptions>? meshOptions = null)
        {
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _keyFetcher = keyFetcher ?? throw new ArgumentNullException(nameof(keyFetcher));
            _libraryActorService = libraryActorService ?? throw new ArgumentNullException(nameof(libraryActorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxPayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
        }

        /// <summary>
        ///     Gets an actor's ActivityPub document.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The actor document.</returns>
        /// <remarks>
        ///     GET /actors/{actorName}
        ///     Returns ActivityPub Actor object in JSON-LD format.
        /// </remarks>
        [HttpGet("{actorName}")]
        [Produces("application/activity+json")]
        public async Task<IActionResult> GetActor(string actorName, CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;

            // Check if federation is enabled and not in hermit mode
            if (!opts.Enabled || opts.IsHermit)
            {
                _logger.LogDebug("[ActivityPub] Federation disabled or in hermit mode, returning 404");
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(actorName))
            {
                return BadRequest("Actor name is required");
            }

            // For friends-only mode, check authorization
            if (opts.IsFriendsOnly && !IsAuthorizedRequest())
            {
                _logger.LogDebug("[ActivityPub] Unauthorized request for friends-only mode");
                return NotFound();
            }

            // Get the actor from the library actor service
            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                _logger.LogDebug("[ActivityPub] Actor not found or not available: {ActorName}", actorName);
                return NotFound();
            }

            // Get the actor document from the library actor
            var actor = await libraryActor.GetActorDocumentAsync(cancellationToken);

            // Add context for JSON-LD
            Response.Headers.Add("Content-Type", "application/activity+json");
            return Ok(actor);
        }

        /// <summary>
        ///     Gets an actor's inbox collection.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <param name="page">Optional page parameter for pagination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The inbox collection.</returns>
        /// <remarks>
        ///     GET /actors/{actorName}/inbox
        ///     Returns ActivityPub OrderedCollection of received activities.
        /// </remarks>
        [HttpGet("{actorName}/inbox")]
        [Produces("application/activity+json")]
        public async Task<IActionResult> GetInbox(
            string actorName,
            [FromQuery] int? page = null,
            CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;

            if (!opts.Enabled || opts.IsHermit)
            {
                return NotFound();
            }

            // Get the actor from the library actor service
            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                return NotFound();
            }

            // For now, return empty inbox
            // TODO: Implement actual inbox with received activities from the actor
            var inbox = new ActivityPubOrderedCollection
            {
                Id = $"{libraryActor.ActorId}/inbox",
                Type = "OrderedCollection",
                TotalItems = 0,
                OrderedItems = Array.Empty<object>()
            };

            return Ok(inbox);
        }

        /// <summary>
        ///     Posts an activity to an actor's inbox.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <param name="activity">The activity object.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of processing the activity.</returns>
        /// <remarks>
        ///     POST /actors/{actorName}/inbox
        ///     Receives and processes incoming ActivityPub activities.
        /// </remarks>
        [HttpPost("{actorName}/inbox")]
        [Consumes("application/activity+json")]
        public async Task<IActionResult> PostToInbox(string actorName, CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;

            if (!opts.Enabled || opts.IsHermit)
                return NotFound();

            if (!string.Equals(actorName, "library", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var bodyBytes = HttpContext.Items["ActivityPubInboxBody"] as byte[];
            if (bodyBytes == null)
            {
                _logger.LogWarning("[ActivityPub] Inbox body not captured (middleware)");
                return Unauthorized();
            }

            if (bodyBytes.Length > _maxPayload)
            {
                _logger.LogWarning("[ActivityPub] Inbox body exceeds MaxRemotePayloadSize ({Max} bytes)", _maxPayload);
                return StatusCode(413, "Payload too large");
            }

            if (opts.VerifySignatures && !await VerifyHttpSignatureAsync(bodyBytes, cancellationToken))
            {
                _logger.LogWarning("[ActivityPub] HTTP signature verification failed");
                return Unauthorized();
            }

            ActivityPubActivity? activity;
            try
            {
                var json = Encoding.UTF8.GetString(bodyBytes);
                activity = SecurityUtils.ParseJsonSafely<ActivityPubActivity>(json, _maxPayload, SecurityUtils.MaxParseDepth);
            }
            catch (ArgumentException)
            {
                return BadRequest("Invalid or oversized JSON body");
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest("Invalid JSON body");
            }

            if (activity == null)
                return BadRequest("Invalid activity");

            _logger.LogInformation("[ActivityPub] Received activity {Type} from {Actor}", activity.Type, activity.Actor);
            await ProcessActivityAsync(activity, cancellationToken);
            return Accepted();
        }

        /// <summary>
        ///     Gets an actor's outbox collection.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <param name="page">Optional page parameter for pagination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The outbox collection.</returns>
        /// <remarks>
        ///     GET /actors/{actorName}/outbox
        ///     Returns ActivityPub OrderedCollection of sent activities.
        /// </remarks>
        [HttpGet("{actorName}/outbox")]
        [Produces("application/activity+json")]
        public async Task<IActionResult> GetOutbox(
            string actorName,
            [FromQuery] int? page = null,
            CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;

            if (!opts.Enabled || opts.IsHermit)
            {
                return NotFound();
            }

            // Get the actor from the library actor service
            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                return NotFound();
            }

            // Get recent activities from the actor
            var activities = await libraryActor.GetRecentActivitiesAsync(opts.PageSize, cancellationToken);

            var outbox = new ActivityPubOrderedCollection
            {
                Id = $"{libraryActor.ActorId}/outbox",
                Type = "OrderedCollection",
                TotalItems = activities.Count,
                OrderedItems = activities.Cast<object>().ToArray()
            };

            return Ok(outbox);
        }

        /// <summary>
        ///     Posts an activity to an actor's outbox.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <param name="activity">The activity object.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of posting the activity.</returns>
        /// <remarks>
        ///     POST /actors/{actorName}/outbox
        ///     Creates and distributes outgoing ActivityPub activities.
        /// </remarks>
        [HttpPost("{actorName}/outbox")]
        [Consumes("application/activity+json")]
        public async Task<IActionResult> PostToOutbox(
            string actorName,
            [FromBody] ActivityPubActivity activity,
            CancellationToken cancellationToken = default)
        {
            // Outbox posting is typically restricted to the actor owner
            // For now, return not implemented
            return StatusCode(501, "Outbox posting not yet implemented");
        }

        private bool IsAuthorizedRequest()
        {
            var opts = _federationOptions.CurrentValue;
            if (!opts.IsFriendsOnly)
                return true;
            if (IsLoopback(HttpContext.Connection.RemoteIpAddress))
                return true;
            var host = Request.Host.Value;
            if (!string.IsNullOrEmpty(host) && opts.ApprovedPeers != null && opts.ApprovedPeers.Length > 0
                && opts.ApprovedPeers.Contains(host, StringComparer.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static bool IsLoopback(IPAddress? a) => a != null && IPAddress.IsLoopback(a);

        /// <summary>PR-14: Verify HTTP Signature (Date ±5min, Digest, Ed25519).</summary>
        private async Task<bool> VerifyHttpSignatureAsync(byte[] bodyBytes, CancellationToken cancellationToken)
        {
            var sig = Request.Headers["Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(sig))
                return false;

            if (!TryParseSignature(sig, out var keyId, out var algorithm, out var headersList, out var signatureB64))
                return false;
            if (!string.Equals(algorithm, "ed25519", StringComparison.OrdinalIgnoreCase) && !string.Equals(algorithm, "hs2019", StringComparison.OrdinalIgnoreCase))
                return false;

            var date = Request.Headers["Date"].FirstOrDefault();
            if (string.IsNullOrEmpty(date) || !DateTimeOffset.TryParse(date, out var dt) || Math.Abs((DateTimeOffset.UtcNow - dt).TotalMinutes) > 5)
                return false;

            var digest = Request.Headers["Digest"].FirstOrDefault();
            var expectedDigest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(bodyBytes));
            if (string.IsNullOrEmpty(digest) || !string.Equals(digest, expectedDigest, StringComparison.Ordinal))
                return false;

            var signingString = BuildSigningString(headersList);
            if (signingString == null)
                return false;

            var pkix = await _keyFetcher.FetchPublicKeyPkixAsync(keyId, cancellationToken);
            if (pkix == null || pkix.Length == 0)
                return false;

            byte[] signatureBytes;
            try
            {
                signatureBytes = Convert.FromBase64String(signatureB64);
            }
            catch
            {
                return false;
            }

            try
            {
                var alg = SignatureAlgorithm.Ed25519;
                using var key = Key.Import(alg, pkix, KeyBlobFormat.PkixPublicKey);
                return alg.Verify(key.PublicKey, Encoding.UTF8.GetBytes(signingString), signatureBytes);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseSignature(string sig, out string keyId, out string algorithm, out string headersList, out string signatureB64)
        {
            keyId = algorithm = headersList = signatureB64 = string.Empty;
            foreach (var part in sig.Split(','))
            {
                var eq = part.IndexOf('=', StringComparison.Ordinal);
                if (eq <= 0) continue;
                var k = part[..eq].Trim();
                var v = part[(eq + 1)..].Trim();
                if (v.Length >= 2 && v[0] == '"' && v[^1] == '"')
                    v = v[1..^1];
                if (k == "keyId") keyId = v;
                else if (k == "algorithm") algorithm = v;
                else if (k == "headers") headersList = v;
                else if (k == "signature") signatureB64 = v;
            }
            return keyId.Length > 0 && algorithm.Length > 0 && headersList.Length > 0 && signatureB64.Length > 0;
        }

        private string? BuildSigningString(string headersList)
        {
            var sb = new StringBuilder();
            var names = headersList.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < names.Length; i++)
            {
                var n = names[i].ToLowerInvariant();
                var v = n switch
                {
                    "(request-target)" => $"(request-target): post {Request.Path}",
                    "host" => "host: " + Request.Host.Value,
                    "date" => "date: " + Request.Headers["Date"].FirstOrDefault(),
                    "digest" => "digest: " + Request.Headers["Digest"].FirstOrDefault(),
                    _ => Request.Headers[n].FirstOrDefault() is { } h ? $"{n}: {h}" : null
                };
                if (v == null) return null;
                if (i > 0) sb.Append('\n');
                sb.Append(v);
            }
            return sb.ToString();
        }

        private async Task ProcessActivityAsync(ActivityPubActivity activity, CancellationToken cancellationToken)
        {
            // TODO: Implement activity processing based on type
            // Handle Follow, Like, Announce, Create, etc.
            _logger.LogDebug("[ActivityPub] Processing activity of type {Type}", activity.Type);
        }

        /// <summary>
        ///     ActivityPub Actor object.
        /// </summary>
        public sealed class ActivityPubActor
        {
            /// <summary>
            ///     Gets or sets the @context.
            /// </summary>
            public object Context { get; set; } = new[]
            {
                "https://www.w3.org/ns/activitystreams",
                "https://w3id.org/security/v1"
            };

            /// <summary>
            ///     Gets or sets the actor ID.
            /// </summary>
            public string Id { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the actor type.
            /// </summary>
            public string Type { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the preferred username.
            /// </summary>
            public string PreferredUsername { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the display name.
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the summary/bio.
            /// </summary>
            public string Summary { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the inbox URL.
            /// </summary>
            public string Inbox { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the outbox URL.
            /// </summary>
            public string Outbox { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the followers collection URL.
            /// </summary>
            public string Followers { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the following collection URL.
            /// </summary>
            public string Following { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the public key.
            /// </summary>
            public ActivityPubPublicKey PublicKey { get; set; } = new();
        }

        /// <summary>
        ///     ActivityPub public key object.
        /// </summary>
        public sealed class ActivityPubPublicKey
        {
            /// <summary>
            ///     Gets or sets the key ID.
            /// </summary>
            public string Id { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the key owner.
            /// </summary>
            public string Owner { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the public key PEM.
            /// </summary>
            public string PublicKeyPem { get; set; } = string.Empty;
        }

        /// <summary>
        ///     ActivityPub OrderedCollection.
        /// </summary>
        public sealed class ActivityPubOrderedCollection
        {
            /// <summary>
            ///     Gets or sets the @context.
            /// </summary>
            public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

            /// <summary>
            ///     Gets or sets the collection ID.
            /// </summary>
            public string Id { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the collection type.
            /// </summary>
            public string Type { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the total number of items.
            /// </summary>
            public int TotalItems { get; set; }

            /// <summary>
            ///     Gets or sets the ordered items.
            /// </summary>
            public object[] OrderedItems { get; set; } = Array.Empty<object>();
        }

        /// <summary>
        ///     ActivityPub Activity object.
        /// </summary>
        public sealed class ActivityPubActivity
        {
            /// <summary>
            ///     Gets or sets the @context.
            /// </summary>
            public object Context { get; set; } = "https://www.w3.org/ns/activitystreams";

            /// <summary>
            ///     Gets or sets the activity ID.
            /// </summary>
            public string Id { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the activity type.
            /// </summary>
            public string Type { get; set; } = string.Empty;

            /// <summary>
            ///     Gets or sets the actor.
            /// </summary>
            public object Actor { get; set; } = new();

            /// <summary>
            ///     Gets or sets the object.
            /// </summary>
            public object Object { get; set; } = new();
        }
    }
}
