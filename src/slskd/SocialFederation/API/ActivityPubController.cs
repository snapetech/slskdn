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
    using System.Text.Json;
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
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class ActivityPubController : ControllerBase
    {
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly IActivityPubInboxStore _inboxStore;
        private readonly IActivityPubOutboxStore _outboxStore;
        private readonly IActivityPubRelationshipStore _relationshipStore;
        private readonly FederationService _federationService;
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
            IActivityPubInboxStore inboxStore,
            IActivityPubOutboxStore outboxStore,
            IActivityPubRelationshipStore relationshipStore,
            FederationService federationService,
            IActivityPubKeyStore keyStore,
            IHttpSignatureKeyFetcher keyFetcher,
            LibraryActorService libraryActorService,
            ILogger<ActivityPubController> logger,
            IOptions<MeshOptions>? meshOptions = null)
        {
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
            _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
            _relationshipStore = relationshipStore ?? throw new ArgumentNullException(nameof(relationshipStore));
            _federationService = federationService ?? throw new ArgumentNullException(nameof(federationService));
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
        [AllowAnonymous]
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
            Response.Headers["Content-Type"] = "application/activity+json";
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
        [AllowAnonymous]
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

            var activities = await _inboxStore.GetActivitiesAsync(actorName, _federationOptions.CurrentValue.PageSize, cancellationToken);
            var orderedItems = new List<object>(activities.Count);
            foreach (var entry in activities)
            {
                using var document = JsonDocument.Parse(entry.RawJson);
                orderedItems.Add(document.RootElement.Clone());
            }

            return Ok(new ActivityPubOrderedCollection
            {
                Id = $"{libraryActor.ActorId}/inbox",
                Type = "OrderedCollection",
                TotalItems = orderedItems.Count,
                OrderedItems = orderedItems.ToArray()
            });
        }

        /// <summary>
        ///     Posts an activity to an actor's inbox.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of processing the activity.</returns>
        /// <remarks>
        ///     POST /actors/{actorName}/inbox
        ///     Receives and processes incoming ActivityPub activities.
        /// </remarks>
        [HttpPost("{actorName}/inbox")]
        [AllowAnonymous]
        [Consumes("application/activity+json")]
        public async Task<IActionResult> PostToInbox(string actorName, CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;

            if (!opts.Enabled || opts.IsHermit)
                return NotFound();

            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                return NotFound();
            }

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

            if (!opts.VerifySignatures)
            {
                // MED-03: signature verification is disabled via configuration — all incoming ActivityPub
                // messages are accepted without cryptographic validation. Only disable in controlled environments.
                _logger.LogWarning("[ActivityPub] MED-03: HTTP signature verification is DISABLED " +
                    "(federation.verify_signatures=false). Accepting unauthenticated inbox message.");
            }
            else if (!await VerifyHttpSignatureAsync(bodyBytes, cancellationToken))
            {
                _logger.LogWarning("[ActivityPub] HTTP signature verification failed");
                return Unauthorized();
            }

            ActivityPubActivity? activity;
            string json;
            try
            {
                json = Encoding.UTF8.GetString(bodyBytes);
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

            await _inboxStore.StoreAsync(actorName, MapActivity(activity), json, cancellationToken);
            var (processed, error) = await ProcessActivityAsync(actorName, activity, cancellationToken);
            var storedActivityId = string.IsNullOrWhiteSpace(activity.Id) ? string.Empty : activity.Id;
            if (!string.IsNullOrWhiteSpace(storedActivityId))
            {
                await _inboxStore.MarkProcessedAsync(actorName, storedActivityId, processed, error, cancellationToken);
            }

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
        [AllowAnonymous]
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

            var orderedItems = new List<object>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var storedActivities = await _outboxStore.GetActivitiesAsync(actorName, opts.PageSize, cancellationToken).ConfigureAwait(false);
            foreach (var entry in storedActivities)
            {
                using var document = JsonDocument.Parse(entry.RawJson);
                orderedItems.Add(document.RootElement.Clone());
                seenIds.Add(entry.ActivityId);
            }

            if (orderedItems.Count < opts.PageSize)
            {
                var activities = await libraryActor.GetRecentActivitiesAsync(opts.PageSize, cancellationToken);
                foreach (var activity in activities)
                {
                    if (!seenIds.Add(activity.Id))
                    {
                        continue;
                    }

                    orderedItems.Add(activity);
                    if (orderedItems.Count >= opts.PageSize)
                    {
                        break;
                    }
                }
            }

            return Ok(new ActivityPubOrderedCollection
            {
                Id = $"{libraryActor.ActorId}/outbox",
                Type = "OrderedCollection",
                TotalItems = orderedItems.Count,
                OrderedItems = orderedItems.ToArray()
            });
        }

        [HttpGet("{actorName}/followers")]
        [AllowAnonymous]
        [Produces("application/activity+json")]
        public async Task<IActionResult> GetFollowers(string actorName, CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;
            if (!opts.Enabled || opts.IsHermit)
            {
                return NotFound();
            }

            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                return NotFound();
            }

            var followers = await _relationshipStore.GetFollowersAsync(actorName, opts.PageSize, cancellationToken).ConfigureAwait(false);
            return Ok(new ActivityPubOrderedCollection
            {
                Id = $"{libraryActor.ActorId}/followers",
                Type = "OrderedCollection",
                TotalItems = followers.Count,
                OrderedItems = followers.Cast<object>().ToArray()
            });
        }

        [HttpGet("{actorName}/following")]
        [AllowAnonymous]
        [Produces("application/activity+json")]
        public async Task<IActionResult> GetFollowing(string actorName, CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;
            if (!opts.Enabled || opts.IsHermit)
            {
                return NotFound();
            }

            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                return NotFound();
            }

            var following = await _relationshipStore.GetFollowingAsync(actorName, opts.PageSize, cancellationToken).ConfigureAwait(false);
            return Ok(new ActivityPubOrderedCollection
            {
                Id = $"{libraryActor.ActorId}/following",
                Type = "OrderedCollection",
                TotalItems = following.Count,
                OrderedItems = following.Cast<object>().ToArray()
            });
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
        [AllowAnonymous]
        [Consumes("application/activity+json")]
        public async Task<IActionResult> PostToOutbox(
            string actorName,
            [FromBody] ActivityPubActivity activity,
            CancellationToken cancellationToken = default)
        {
            var opts = _federationOptions.CurrentValue;
            if (!opts.Enabled || opts.IsHermit)
            {
                return NotFound();
            }

            var libraryActor = _libraryActorService.GetActor(actorName);
            if (libraryActor == null)
            {
                return NotFound();
            }

            if (activity == null || string.IsNullOrWhiteSpace(activity.Type))
            {
                return BadRequest("Activity type is required");
            }

            var (published, error) = await _federationService.PublishOutboxActivityAsync(actorName, MapActivity(activity), cancellationToken).ConfigureAwait(false);
            if (published == null)
            {
                _logger.LogWarning("[ActivityPub] Failed to publish outbox activity for {Actor}: {Error}", actorName, error ?? "Unknown error");
                return BadRequest("Unable to publish activity");
            }

            var rawJson = JsonSerializer.Serialize(published, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            await _outboxStore.StoreAsync(actorName, published, rawJson, cancellationToken).ConfigureAwait(false);
            return Ok(published);
        }

        private bool IsAuthorizedRequest()
        {
            var opts = _federationOptions.CurrentValue;
            if (!opts.IsFriendsOnly)
                return true;
            if (IsLoopback(HttpContext.Connection.RemoteIpAddress))
                return true;
            var candidateHosts = new[]
            {
                Request.Headers["Origin"].FirstOrDefault(),
                Request.Headers["Referer"].FirstOrDefault()
            }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value =>
                {
                    if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    {
                        return uri.Host;
                    }

                    return value;
                });
            if (opts.ApprovedPeers != null && opts.ApprovedPeers.Length > 0
                && candidateHosts.Any(host => !string.IsNullOrWhiteSpace(host)
                    && opts.ApprovedPeers.Contains(host, StringComparer.OrdinalIgnoreCase)))
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
            if (_federationOptions.CurrentValue.IsFriendsOnly && !IsApprovedKeyHost(keyId))
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

        private bool IsApprovedKeyHost(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId) ||
                !Uri.TryCreate(keyId, UriKind.Absolute, out var keyUri))
            {
                return false;
            }

            var approvedPeers = _federationOptions.CurrentValue.ApprovedPeers;
            return approvedPeers != null &&
                approvedPeers.Length > 0 &&
                approvedPeers.Contains(keyUri.Host, StringComparer.OrdinalIgnoreCase);
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

        private async Task<(bool Processed, string? Error)> ProcessActivityAsync(string actorName, ActivityPubActivity activity, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(activity.Type))
            {
                return (false, "Missing activity type");
            }

            switch (activity.Type)
            {
                case "Follow":
                    {
                        var remoteActorId = activity.Actor?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(remoteActorId))
                        {
                            return (false, "Follow activity missing actor");
                        }

                        await _relationshipStore.UpsertFollowerAsync(actorName, remoteActorId, cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("[ActivityPub] Recorded follower {RemoteActor} for actor {ActorName}", remoteActorId, actorName);
                        return (true, null);
                    }

                case "Undo":
                    {
                        if (!string.Equals(TryGetObjectType(activity.Object), "Follow", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, null);
                        }

                        var remoteActorId = TryGetObjectActor(activity.Object) ?? activity.Actor?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(remoteActorId))
                        {
                            return (false, "Undo follow missing actor");
                        }

                        await _relationshipStore.RemoveFollowerAsync(actorName, remoteActorId, cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("[ActivityPub] Removed follower {RemoteActor} for actor {ActorName}", remoteActorId, actorName);
                        return (true, null);
                    }

                case "Accept":
                    {
                        if (!string.Equals(TryGetObjectType(activity.Object), "Follow", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("[ActivityPub] Stored inbound activity {Type} for actor {ActorName} from {Actor}",
                                activity.Type, actorName, activity.Actor);
                            return (true, null);
                        }

                        var remoteActorId = activity.Actor?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(remoteActorId))
                        {
                            await _relationshipStore.UpsertFollowingAsync(actorName, remoteActorId, cancellationToken).ConfigureAwait(false);
                        }

                        _logger.LogInformation("[ActivityPub] Accepted follow relationship with {RemoteActor} for actor {ActorName}", remoteActorId, actorName);
                        return (true, null);
                    }

                case "Reject":
                    {
                        if (!string.Equals(TryGetObjectType(activity.Object), "Follow", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("[ActivityPub] Stored inbound activity {Type} for actor {ActorName} from {Actor}",
                                activity.Type, actorName, activity.Actor);
                            return (true, null);
                        }

                        var remoteActorId = activity.Actor?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(remoteActorId))
                        {
                            await _relationshipStore.RemoveFollowingAsync(actorName, remoteActorId, cancellationToken).ConfigureAwait(false);
                        }

                        _logger.LogInformation("[ActivityPub] Rejected follow relationship with {RemoteActor} for actor {ActorName}", remoteActorId, actorName);
                        return (true, null);
                    }

                case "Create":
                case "Update":
                case "Delete":
                case "Announce":
                case "Like":
                case "Add":
                    _logger.LogInformation("[ActivityPub] Stored inbound activity {Type} for actor {ActorName} from {Actor}",
                        activity.Type, actorName, activity.Actor);
                    return (true, null);

                case "Remove":
                    {
                        if (string.Equals(TryGetObjectType(activity.Object), "Follow", StringComparison.OrdinalIgnoreCase))
                        {
                            var remoteActorId = TryGetObjectActor(activity.Object) ?? activity.Actor?.ToString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(remoteActorId))
                            {
                                await _relationshipStore.RemoveFollowerAsync(actorName, remoteActorId, cancellationToken).ConfigureAwait(false);
                                await _relationshipStore.RemoveFollowingAsync(actorName, remoteActorId, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        _logger.LogInformation("[ActivityPub] Stored inbound activity {Type} for actor {ActorName} from {Actor}",
                            activity.Type, actorName, activity.Actor);
                        return (true, null);
                    }

                default:
                    _logger.LogWarning("[ActivityPub] Stored unsupported activity type {Type} for actor {ActorName}", activity.Type, actorName);
                    return (false, $"Unsupported activity type '{activity.Type}'");
            }
        }

        private static slskd.SocialFederation.ActivityPubActivity MapActivity(ActivityPubActivity activity)
        {
            return new slskd.SocialFederation.ActivityPubActivity
            {
                Context = activity.Context,
                Id = activity.Id,
                Type = activity.Type,
                Actor = activity.Actor,
                Object = activity.Object,
            };
        }

        private static string? TryGetObjectType(object? value)
        {
            if (value is JsonElement element &&
                element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String)
            {
                return typeElement.GetString();
            }

            return null;
        }

        private static string? TryGetObjectActor(object? value)
        {
            if (value is JsonElement element &&
                element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("actor", out var actorElement) &&
                actorElement.ValueKind == JsonValueKind.String)
            {
                return actorElement.GetString();
            }

            return null;
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
