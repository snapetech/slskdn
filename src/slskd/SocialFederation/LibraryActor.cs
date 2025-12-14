// <copyright file="LibraryActor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Base class for library actors in ActivityPub federation.
    /// </summary>
    /// <remarks>
    ///     T-FED02: Library actors represent content collections for specific domains.
    ///     Each domain (music, books, movies, tv) has its own actor.
    /// </remarks>
    public abstract class LibraryActor
    {
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly IActivityPubKeyStore _keyStore;
        private readonly ILogger _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LibraryActor"/> class.
        /// </summary>
        /// <param name="domain">The content domain this actor represents.</param>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="logger">The logger.</param>
        protected LibraryActor(
            string domain,
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IActivityPubKeyStore keyStore,
            ILogger logger)
        {
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Gets the content domain this actor represents.
        /// </summary>
        public string Domain { get; }

        /// <summary>
        ///     Gets the actor name (used in URLs).
        /// </summary>
        public string ActorName => Domain;

        /// <summary>
        ///     Gets the full actor ID.
        /// </summary>
        public string ActorId => $"{_federationOptions.CurrentValue.BaseUrl}/actors/{ActorName}";

        /// <summary>
        ///     Gets a value indicating whether this actor is available.
        /// </summary>
        /// <remarks>
        ///     Checks federation mode and domain-specific availability.
        /// </remarks>
        public virtual bool IsAvailable
        {
            get
            {
                var opts = _federationOptions.CurrentValue;

                // Must be enabled and not in hermit mode
                if (!opts.Enabled || opts.IsHermit)
                {
                    return false;
                }

                // Check if we have content to share
                return HasContentToShare();
            }
        }

        /// <summary>
        ///     Gets the ActivityPub actor document for this library actor.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The actor document.</returns>
        public async Task<ActivityPubActor> GetActorDocumentAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException($"Actor {ActorName} is not available");
            }

            var publicKeyPem = await _keyStore.GetPublicKeyAsync(ActorId, cancellationToken);
            var opts = _federationOptions.CurrentValue;

            return new ActivityPubActor
            {
                Id = ActorId,
                Type = "Service",
                PreferredUsername = ActorName,
                Name = GetDisplayName(),
                Summary = GetSummary(),
                Inbox = $"{ActorId}/inbox",
                Outbox = $"{ActorId}/outbox",
                Followers = $"{ActorId}/followers",
                Following = $"{ActorId}/following",
                PublicKey = new ActivityPubPublicKey
                {
                    Id = $"{ActorId}#main-key",
                    Owner = ActorId,
                    PublicKeyPem = publicKeyPem
                },
                // Add domain-specific context
                Context = new[]
                {
                    "https://www.w3.org/ns/activitystreams",
                    "https://w3id.org/security/v1",
                    "https://w3id.org/federation/workref#"
                }
            };
        }

        /// <summary>
        ///     Gets recent activities from this actor's outbox.
        /// </summary>
        /// <param name="maxItems">The maximum number of items to return.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The recent activities.</returns>
        public async Task<IReadOnlyList<ActivityPubActivity>> GetRecentActivitiesAsync(
            int maxItems = 20,
            CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return Array.Empty<ActivityPubActivity>();
            }

            var activities = new List<ActivityPubActivity>();
            var opts = _federationOptions.CurrentValue;

            // Get recent work references and convert to activities
            var workRefs = await GetRecentWorkRefsAsync(maxItems, cancellationToken);

            foreach (var workRef in workRefs)
            {
                var activity = new ActivityPubActivity
                {
                    Id = $"{ActorId}/activities/{SanitizeId(workRef.Id)}",
                    Type = "Create",
                    Actor = ActorId,
                    Object = workRef,
                    Published = workRef.Published,
                    To = new[] { "https://www.w3.org/ns/activitystreams#Public" }
                };

                activities.Add(activity);
            }

            return activities.OrderByDescending(a => a.Published).ToList();
        }

        /// <summary>
        ///     Gets the display name for this actor.
        /// </summary>
        /// <returns>The display name.</returns>
        protected abstract string GetDisplayName();

        /// <summary>
        ///     Gets the summary/description for this actor.
        /// </summary>
        /// <returns>The summary.</returns>
        protected abstract string GetSummary();

        /// <summary>
        ///     Checks if this actor has content to share.
        /// </summary>
        /// <returns>True if content is available.</returns>
        protected abstract bool HasContentToShare();

        /// <summary>
        ///     Gets recent work references from this domain.
        /// </summary>
        /// <param name="maxItems">The maximum number of items.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The work references.</returns>
        protected abstract Task<IReadOnlyList<WorkRef>> GetRecentWorkRefsAsync(
            int maxItems,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Sanitizes an ID for use in URLs.
        /// </summary>
        /// <param name="id">The ID to sanitize.</param>
        /// <returns>The sanitized ID.</returns>
        private static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "unknown";
            }

            return System.Text.RegularExpressions.Regex.Replace(id, @"[^a-zA-Z0-9\-_]", "-")
                .Trim('-')
                .ToLowerInvariant();
        }
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
        public object PublicKey { get; set; } = new();
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

        /// <summary>
        ///     Gets or sets the publication timestamp.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        ///     Gets or sets the recipients.
        /// </summary>
        public object To { get; set; } = new();
    }
}
