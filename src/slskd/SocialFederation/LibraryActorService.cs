// <copyright file="LibraryActorService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Service for managing library actors.
    /// </summary>
    /// <remarks>
    ///     T-FED02: Manages library actors for different content domains.
    ///     Provides access to available actors and their documents.
    /// </remarks>
    public sealed class LibraryActorService
    {
        private readonly ConcurrentDictionary<string, LibraryActor> _actors = new();
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly IActivityPubKeyStore _keyStore;
        private readonly ILogger<LibraryActorService> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LibraryActorService"/> class.
        /// </summary>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="musicActor">The music library actor (optional).</param>
        /// <param name="logger">The logger.</param>
        public LibraryActorService(
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IActivityPubKeyStore keyStore,
            MusicLibraryActor? musicActor,
            ILogger<LibraryActorService> logger)
        {
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Register the music actor if available
            if (musicActor != null)
            {
                _actors[musicActor.ActorName] = musicActor;
            }

            // Register generic actors for other domains
            RegisterGenericActors();
        }

        /// <summary>
        ///     Gets all available library actors.
        /// </summary>
        public IReadOnlyDictionary<string, LibraryActor> AvailableActors
        {
            get
            {
                var opts = _federationOptions.CurrentValue;

                // Only return actors that are available based on federation mode
                return _actors.Where(kvp => kvp.Value.IsAvailable)
                             .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <summary>
        ///     Gets a library actor by name.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <returns>The actor, or null if not found or not available.</returns>
        public LibraryActor? GetActor(string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName))
            {
                return null;
            }

            if (_actors.TryGetValue(actorName, out var actor) && actor.IsAvailable)
            {
                return actor;
            }

            return null;
        }

        /// <summary>
        ///     Checks if an actor name corresponds to a valid library actor.
        /// </summary>
        /// <param name="actorName">The actor name.</param>
        /// <returns>True if the actor exists and is available.</returns>
        public bool IsLibraryActor(string actorName)
        {
            return GetActor(actorName) != null;
        }

        /// <summary>
        ///     Gets the available domains for federation.
        /// </summary>
        /// <returns>The list of available domains.</returns>
        public IReadOnlyList<string> GetAvailableDomains()
        {
            return AvailableActors.Keys.ToList();
        }

        private void RegisterGenericActors()
        {
            var genericDomains = new[] { "books", "movies", "tv", "software", "games" };

            foreach (var domain in genericDomains)
            {
                try
                {
                    // Create generic actors - these can be replaced with specific implementations later
                    var logger = _logger; // Use the same logger for all generic actors
                    var actor = new GenericLibraryActor(
                        domain,
                        _federationOptions,
                        _keyStore,
                        logger);

                    _actors[domain] = actor;
                    _logger.LogDebug("[LibraryActorService] Registered generic actor for domain {Domain}", domain);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LibraryActorService] Failed to register generic actor for domain {Domain}", domain);
                }
            }
        }
    }
}
