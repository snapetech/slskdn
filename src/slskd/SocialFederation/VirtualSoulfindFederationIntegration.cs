// <copyright file="VirtualSoulfindFederationIntegration.cs" company="slskdN Team">
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
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Integration service between VirtualSoulfind and federation publishing.
    /// </summary>
    /// <remarks>
    ///     T-FED03: Hooks into VirtualSoulfind content events to publish to federation.
    ///     Listens for content additions, list creations, and publishes appropriate activities.
    /// </remarks>
    public sealed class VirtualSoulfindFederationIntegration
    {
        private readonly IOptionsMonitor<FederationPublishingOptions> _publishingOptions;
        private readonly FederationService _federationService;
        private readonly ILogger<VirtualSoulfindFederationIntegration> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="VirtualSoulfindFederationIntegration"/> class.
        /// </summary>
        /// <param name="publishingOptions">The publishing options.</param>
        /// <param name="federationService">The federation service.</param>
        /// <param name="logger">The logger.</param>
        public VirtualSoulfindFederationIntegration(
            IOptionsMonitor<FederationPublishingOptions> publishingOptions,
            FederationService federationService,
            ILogger<VirtualSoulfindFederationIntegration> logger)
        {
            _publishingOptions = publishingOptions ?? throw new ArgumentNullException(nameof(publishingOptions));
            _federationService = federationService ?? throw new ArgumentNullException(nameof(federationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Handles content being added to VirtualSoulfind.
        /// </summary>
        /// <param name="contentItem">The content item that was added.</param>
        /// <param name="isAdvertisable">Whether the content is marked as advertisable.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task OnContentAddedAsync(
            IContentItem contentItem,
            bool isAdvertisable,
            CancellationToken cancellationToken = default)
        {
            if (contentItem == null)
            {
                throw new ArgumentNullException(nameof(contentItem));
            }

            var opts = _publishingOptions.CurrentValue;

            // Check if publishing is enabled
            if (!opts.Enabled)
            {
                return;
            }

            // Check if we can publish this content
            if (!_federationService.CanPublishContent(contentItem.Domain.ToString(), isAdvertisable))
            {
                _logger.LogDebug("[VSFederation] Skipping content {Id} - not publishable", contentItem.Id);
                return;
            }

            try
            {
                // Create WorkRef based on content type
                var workRef = CreateWorkRefFromContentItem(contentItem);

                if (workRef != null && workRef.ValidateSecurity())
                {
                    await _federationService.PublishWorkRefAsync(workRef, cancellationToken);
                    _logger.LogInformation("[VSFederation] Published content {Id} to federation", contentItem.Id);
                }
                else
                {
                    _logger.LogWarning("[VSFederation] Content {Id} failed validation or WorkRef creation", contentItem.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VSFederation] Failed to publish content {Id}", contentItem.Id);
            }
        }

        /// <summary>
        ///     Handles a list being created or modified in VirtualSoulfind.
        /// </summary>
        /// <param name="listId">The list identifier.</param>
        /// <param name="listName">The list name.</param>
        /// <param name="visibility">The list visibility.</param>
        /// <param name="contentItems">The content items in the list.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task OnListModifiedAsync(
            string listId,
            string listName,
            string visibility,
            IReadOnlyList<IContentItem> contentItems,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(listId))
            {
                throw new ArgumentException("List ID cannot be null or empty.", nameof(listId));
            }

            var opts = _publishingOptions.CurrentValue;

            // Check if publishing is enabled
            if (!opts.Enabled)
            {
                return;
            }

            // Skip private lists
            if (string.Equals(visibility, "private", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[VSFederation] Skipping private list {ListId}", listId);
                return;
            }

            try
            {
                // Convert content items to WorkRefs
                var workRefs = contentItems
                    .Select(CreateWorkRefFromContentItem)
                    .Where(w => w != null && w.ValidateSecurity())
                    .Cast<WorkRef>()
                    .ToList();

                if (workRefs.Any())
                {
                    await _federationService.PublishListAsync(listId, listName, visibility, workRefs, cancellationToken);
                    _logger.LogInformation("[VSFederation] Published list {ListId} with {Count} items", listId, workRefs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VSFederation] Failed to publish list {ListId}", listId);
            }
        }

        /// <summary>
        ///     Handles content being removed from VirtualSoulfind.
        /// </summary>
        /// <param name="contentId">The content identifier.</param>
        /// <param name="domain">The content domain.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task OnContentRemovedAsync(
            string contentId,
            string domain,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement tombstone activities for removed content
            // For now, just log
            _logger.LogDebug("[VSFederation] Content {ContentId} removed from domain {Domain}", contentId, domain);
            await Task.CompletedTask;
        }

        /// <summary>
        ///     Creates a WorkRef from a content item.
        /// </summary>
        /// <param name="contentItem">The content item.</param>
        /// <returns>The WorkRef, or null if creation fails.</returns>
        private WorkRef? CreateWorkRefFromContentItem(IContentItem contentItem)
        {
            try
            {
                // Create base WorkRef
                var workRef = new WorkRef
                {
                    Id = $"work:{contentItem.Domain}:{SanitizeId(contentItem.Id.ToString())}",
                    Domain = contentItem.Domain.ToString(),
                    Title = contentItem.PrimaryName ?? "Unknown Title",
                    Published = DateTimeOffset.UtcNow
                };

                // Add domain-specific metadata
                switch (contentItem.Domain.ToLowerInvariant())
                {
                    case "music":
                        if (contentItem is MusicItem musicItem)
                        {
                            workRef = WorkRef.FromMusicItem(musicItem, "https://localhost:5000");
                        }
                        break;

                    case "books":
                    case "movies":
                    case "tv":
                    case "software":
                    case "games":
                        // For other domains, use basic metadata
                        workRef.Metadata["contentType"] = contentItem.Domain;
                        if (!string.IsNullOrEmpty(contentItem.PrimaryName))
                        {
                            workRef.Metadata["name"] = contentItem.PrimaryName;
                        }
                        break;

                    default:
                        _logger.LogWarning("[VSFederation] Unknown domain {Domain} for content {Id}",
                            contentItem.Domain, contentItem.Id);
                        return null;
                }

                // Set attribution to the appropriate library actor
                workRef.AttributedTo = $"/actors/{contentItem.Domain}";

                return workRef;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VSFederation] Failed to create WorkRef for content {Id}", contentItem.Id);
                return null;
            }
        }

        /// <summary>
        ///     Sanitizes an ID for use in federation.
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
}
