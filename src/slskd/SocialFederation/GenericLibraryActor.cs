// <copyright file="GenericLibraryActor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Generic library actor for domains without specific implementations.
    /// </summary>
    /// <remarks>
    ///     T-FED02: Generic library actor for books, movies, tv, software, games domains.
    ///     Can be extended with domain-specific logic as needed.
    /// </remarks>
    public sealed class GenericLibraryActor : LibraryActor
    {
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GenericLibraryActor"/> class.
        /// </summary>
        /// <param name="domain">The content domain.</param>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="logger">The logger.</param>
        public GenericLibraryActor(
            string domain,
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IActivityPubKeyStore keyStore,
            ILogger<GenericLibraryActor> logger)
            : base(domain, federationOptions, keyStore, logger)
        {
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));

            if (!IsValidDomain(domain))
            {
                throw new ArgumentException($"Invalid domain: {domain}", nameof(domain));
            }
        }

        /// <inheritdoc/>
        protected override string GetDisplayName()
        {
            return $"{char.ToUpper(Domain[0])}{Domain[1..]} Library";
        }

        /// <inheritdoc/>
        protected override string GetSummary()
        {
            var domainName = Domain switch
            {
                "books" => "books",
                "movies" => "movies",
                "tv" => "TV shows",
                "software" => "software",
                "games" => "games",
                _ => "content"
            };

            return $"A decentralized collection of {domainName} shared by community members";
        }

        /// <inheritdoc/>
        protected override bool HasContentToShare()
        {
            // For generic actors, assume content is available if the domain is enabled
            // In a real implementation, this would check with the actual content providers
            return true;
        }

        /// <inheritdoc/>
        protected override async Task<IReadOnlyList<WorkRef>> GetRecentWorkRefsAsync(
            int maxItems,
            CancellationToken cancellationToken = default)
        {
            // For now, return empty list - would be populated when domain-specific providers are implemented
            // This allows the actor infrastructure to work while individual domains are developed
            await Task.CompletedTask;
            return Array.Empty<WorkRef>();
        }

        private static bool IsValidDomain(string domain)
        {
            return domain switch
            {
                "books" or "movies" or "tv" or "software" or "games" => true,
                _ => false
            };
        }
    }
}

