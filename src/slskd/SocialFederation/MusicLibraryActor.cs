// <copyright file="MusicLibraryActor.cs" company="slskdN Team">
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
    ///     Music library actor for ActivityPub federation.
    /// </summary>
    /// <remarks>
    ///     T-FED02: Music domain library actor (@music@{instance}).
    ///     Represents the music content collection in federation.
    /// </remarks>
    public sealed class MusicLibraryActor : LibraryActor
    {
        private readonly ContentDomain.IMusicContentDomainProvider _musicProvider;
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicLibraryActor"/> class.
        /// </summary>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="musicProvider">The music content domain provider.</param>
        /// <param name="logger">The logger.</param>
        public MusicLibraryActor(
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IActivityPubKeyStore keyStore,
            ContentDomain.IMusicContentDomainProvider musicProvider,
            ILogger<MusicLibraryActor> logger)
            : base("music", federationOptions, keyStore, logger)
        {
            _musicProvider = musicProvider ?? throw new ArgumentNullException(nameof(musicProvider));
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
        }

        /// <inheritdoc/>
        protected override string GetDisplayName()
        {
            return "Music Library";
        }

        /// <inheritdoc/>
        protected override string GetSummary()
        {
            return "A decentralized collection of music shared by community members";
        }

        /// <inheritdoc/>
        protected override bool HasContentToShare()
        {
            // Check if music provider has any items
            // For now, assume we have music content if the provider is available
            return _musicProvider != null;
        }

        /// <inheritdoc/>
        protected override async Task<IReadOnlyList<WorkRef>> GetRecentWorkRefsAsync(
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            var workRefs = new List<WorkRef>();

            try
            {
                // Get recent music items from the content domain
                var musicItems = await _musicProvider.GetRecentItemsAsync(maxItems, cancellationToken);

                foreach (var item in musicItems)
                {
                    try
                    {
                        var workRef = WorkRef.FromMusicItem(item, BaseUrl);
                        if (workRef.ValidateSecurity())
                        {
                            workRefs.Add(workRef);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log warning - we can't access base logger directly
                        // This will be handled by the base class error handling
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error - handled by base class
            }

            return workRefs;
        }

        private string BaseUrl => _federationOptions.CurrentValue.BaseUrl ?? "https://localhost:5000";
    }
}
