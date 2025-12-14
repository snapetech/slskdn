// <copyright file="FederationService.cs" company="slskdN Team">
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
    ///     Service for publishing VirtualSoulfind content to ActivityPub federation.
    /// </summary>
    /// <remarks>
    ///     T-FED03: Outgoing publishing from VirtualSoulfind to federation.
    ///     Handles per-domain and per-list publish policies with moderation integration.
    /// </remarks>
    public sealed class FederationService
    {
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly IOptionsMonitor<FederationPublishingOptions> _publishingOptions;
        private readonly LibraryActorService _libraryActorService;
        private readonly IActivityPubKeyStore _keyStore;
        private readonly ActivityDeliveryService _deliveryService;
        private readonly ILogger<FederationService> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FederationService"/> class.
        /// </summary>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="publishingOptions">The publishing options.</param>
        /// <param name="libraryActorService">The library actor service.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="deliveryService">The activity delivery service.</param>
        /// <param name="logger">The logger.</param>
        public FederationService(
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IOptionsMonitor<FederationPublishingOptions> publishingOptions,
            LibraryActorService libraryActorService,
            IActivityPubKeyStore keyStore,
            ActivityDeliveryService deliveryService,
            ILogger<FederationService> logger)
        {
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _publishingOptions = publishingOptions ?? throw new ArgumentNullException(nameof(publishingOptions));
            _libraryActorService = libraryActorService ?? throw new ArgumentNullException(nameof(libraryActorService));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _deliveryService = deliveryService ?? throw new ArgumentNullException(nameof(deliveryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Publishes a WorkRef to the appropriate domain actor.
        /// </summary>
        /// <param name="workRef">The work reference to publish.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task PublishWorkRefAsync(WorkRef workRef, CancellationToken cancellationToken = default)
        {
            if (workRef == null)
            {
                throw new ArgumentNullException(nameof(workRef));
            }

            var opts = _federationOptions.CurrentValue;
            var pubOpts = _publishingOptions.CurrentValue;

            // Check if federation is enabled and not in hermit mode
            if (!opts.Enabled || opts.IsHermit)
            {
                _logger.LogDebug("[Federation] Skipping WorkRef publish - federation disabled or hermit mode");
                return;
            }

            // Check if this domain is publishable
            if (!pubOpts.PublishableDomains.Contains(workRef.Domain, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[Federation] Skipping WorkRef publish - domain '{Domain}' not publishable", workRef.Domain);
                return;
            }

            // Validate security before publishing
            if (!workRef.ValidateSecurity())
            {
                _logger.LogWarning("[Federation] WorkRef failed security validation - not publishing");
                return;
            }

            // Find the appropriate library actor
            var actor = _libraryActorService.GetActor(workRef.Domain);
            if (actor == null)
            {
                _logger.LogWarning("[Federation] No actor available for domain '{Domain}'", workRef.Domain);
                return;
            }

            try
            {
                // Create Create activity for the WorkRef
                var createActivity = new ActivityPubActivity
                {
                    Id = $"{actor.ActorId}/activities/workref-{SanitizeId(workRef.Id)}",
                    Type = "Create",
                    Actor = actor.ActorId,
                    Object = workRef,
                    Published = DateTimeOffset.UtcNow,
                    To = new[] { "https://www.w3.org/ns/activitystreams#Public" }
                };

                // Add to actor's outbox (this would queue for delivery)
                await PublishActivityAsync(createActivity, cancellationToken);

                _logger.LogInformation("[Federation] Published WorkRef for '{Title}' in domain '{Domain}'",
                    workRef.Title, workRef.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Federation] Failed to publish WorkRef '{WorkRefId}'", workRef.Id);
            }
        }

        /// <summary>
        ///     Publishes a content list/collection to federation.
        /// </summary>
        /// <param name="listId">The list identifier.</param>
        /// <param name="listName">The list name.</param>
        /// <param name="visibility">The list visibility (private/circle:xxx/public).</param>
        /// <param name="workRefs">The work references in the list.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task PublishListAsync(
            string listId,
            string listName,
            string visibility,
            IReadOnlyList<WorkRef> workRefs,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(listId))
            {
                throw new ArgumentException("List ID cannot be null or empty.", nameof(listId));
            }

            var opts = _federationOptions.CurrentValue;

            // Check federation mode
            if (!opts.Enabled || opts.IsHermit)
            {
                _logger.LogDebug("[Federation] Skipping list publish - federation disabled or hermit mode");
                return;
            }

            // Parse visibility
            var visibilityType = ParseVisibility(visibility);
            if (visibilityType == ListVisibility.Private)
            {
                _logger.LogDebug("[Federation] Skipping private list '{ListId}'", listId);
                return;
            }

            // For circle visibility, validate circle exists
            string? circleName = null;
            if (visibilityType == ListVisibility.Circle)
            {
                circleName = ExtractCircleName(visibility);
                if (string.IsNullOrWhiteSpace(circleName))
                {
                    _logger.LogWarning("[Federation] Invalid circle visibility format: '{Visibility}'", visibility);
                    return;
                }
            }

            try
            {
                // Create Collection activity for the list
                var collectionActivity = CreateCollectionActivity(listId, listName, workRefs, visibilityType, circleName);

                await PublishActivityAsync(collectionActivity, cancellationToken);

                _logger.LogInformation("[Federation] Published list '{ListName}' with {Count} items (visibility: {Visibility})",
                    listName, workRefs.Count, visibility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Federation] Failed to publish list '{ListId}'", listId);
            }
        }

        /// <summary>
        ///     Checks if content can be published to federation.
        /// </summary>
        /// <param name="domain">The content domain.</param>
        /// <param name="isAdvertisable">Whether the content is marked as advertisable.</param>
        /// <returns>True if the content can be published.</returns>
        public bool CanPublishContent(string domain, bool isAdvertisable)
        {
            var opts = _federationOptions.CurrentValue;
            var pubOpts = _publishingOptions.CurrentValue;

            // Federation must be enabled and not hermit
            if (!opts.Enabled || opts.IsHermit)
            {
                return false;
            }

            // Domain must be publishable
            if (!pubOpts.PublishableDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // Content must be advertisable (from T-MCP03 integration)
            return isAdvertisable;
        }

        private ActivityPubActivity CreateCollectionActivity(
            string listId,
            string listName,
            IReadOnlyList<WorkRef> workRefs,
            ListVisibility visibility,
            string? circleName)
        {
            var opts = _federationOptions.CurrentValue;

            // Create the collection object
            var collection = new
            {
                id = $"{opts.BaseUrl}/collections/lists/{SanitizeId(listId)}",
                type = "Collection",
                name = listName,
                totalItems = workRefs.Count,
                items = workRefs.Select(w => w.Id).ToArray()
            };

            // Determine audience based on visibility
            var to = visibility switch
            {
                ListVisibility.Public => new[] { "https://www.w3.org/ns/activitystreams#Public" },
                ListVisibility.Circle when !string.IsNullOrWhiteSpace(circleName) =>
                    new[] { $"{opts.BaseUrl}/circles/{SanitizeId(circleName)}" },
                _ => Array.Empty<string>()
            };

            return new ActivityPubActivity
            {
                Id = $"{opts.BaseUrl}/activities/list-{SanitizeId(listId)}",
                Type = "Announce", // Announce the collection
                Actor = $"{opts.BaseUrl}/actors/user", // TODO: Use actual user actor
                Object = collection,
                Published = DateTimeOffset.UtcNow,
                To = to
            };
        }

        private async Task PublishActivityAsync(ActivityPubActivity activity, CancellationToken cancellationToken)
        {
            var recipients = activity.To as IEnumerable<string> ?? Array.Empty<string>();

            if (!recipients.Any())
            {
                _logger.LogDebug("[Federation] No recipients for activity {ActivityId}", activity.Id);
                return;
            }

            try
            {
                // Resolve recipient URLs to inbox URLs
                var inboxUrls = await ResolveInboxUrlsAsync(recipients, cancellationToken);

                if (!inboxUrls.Any())
                {
                    _logger.LogWarning("[Federation] No valid inbox URLs resolved for activity {ActivityId}", activity.Id);
                    return;
                }

                // Deliver the activity
                await _deliveryService.DeliverActivityAsync(activity, inboxUrls, cancellationToken);

                _logger.LogInformation("[Federation] Published activity {Type} to {Count} recipients",
                    activity.Type, inboxUrls.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Federation] Failed to publish activity {ActivityId}", activity.Id);
            }
        }

        private async Task<IReadOnlyList<string>> ResolveInboxUrlsAsync(
            IEnumerable<string> recipients,
            CancellationToken cancellationToken)
        {
            var inboxUrls = new List<string>();

            foreach (var recipient in recipients)
            {
                try
                {
                    if (recipient == "https://www.w3.org/ns/activitystreams#Public")
                    {
                        // For public posts, we would need to discover followers
                        // For now, skip public delivery
                        _logger.LogDebug("[Federation] Skipping public delivery for now");
                        continue;
                    }

                    // Check if it's already an inbox URL
                    if (recipient.Contains("/inbox"))
                    {
                        inboxUrls.Add(recipient);
                        continue;
                    }

                    // Try to resolve actor to inbox URL
                    var inboxUrl = await ResolveActorToInboxAsync(recipient, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(inboxUrl))
                    {
                        inboxUrls.Add(inboxUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Federation] Failed to resolve inbox for recipient {Recipient}", recipient);
                }
            }

            return inboxUrls;
        }

        private async Task<string?> ResolveActorToInboxAsync(string actorId, CancellationToken cancellationToken)
        {
            // TODO: Implement actor discovery to get inbox URL
            // For now, assume the actor ID ends with the actor name and construct inbox URL
            if (actorId.Contains("/actors/"))
            {
                var baseUrl = actorId.Substring(0, actorId.LastIndexOf("/actors/"));
                var actorName = actorId.Substring(actorId.LastIndexOf("/") + 1);
                return $"{baseUrl}/actors/{actorName}/inbox";
            }

            return null;
        }

        private static ListVisibility ParseVisibility(string visibility)
        {
            if (string.IsNullOrWhiteSpace(visibility))
            {
                return ListVisibility.Private;
            }

            if (string.Equals(visibility, "public", StringComparison.OrdinalIgnoreCase))
            {
                return ListVisibility.Public;
            }

            if (visibility.StartsWith("circle:", StringComparison.OrdinalIgnoreCase))
            {
                return ListVisibility.Circle;
            }

            return ListVisibility.Private;
        }

        private static string? ExtractCircleName(string visibility)
        {
            if (string.IsNullOrWhiteSpace(visibility) ||
                !visibility.StartsWith("circle:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return visibility.Substring(7); // Remove "circle:" prefix
        }

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

        private enum ListVisibility
        {
            Private,
            Circle,
            Public
        }
    }
}
