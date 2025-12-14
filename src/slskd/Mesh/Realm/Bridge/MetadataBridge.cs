// <copyright file="MetadataBridge.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm.Bridge
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Metadata bridge for controlled cross-realm metadata sharing.
    /// </summary>
    /// <remarks>
    ///     T-REALM-04: Bridge Flow Policies - enables metadata queries and search
    ///     between realms when explicitly allowed by bridge policies.
    /// </remarks>
    public sealed class MetadataBridge
    {
        private readonly BridgeFlowEnforcer _flowEnforcer;
        private readonly ILogger<MetadataBridge> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MetadataBridge"/> class.
        /// </summary>
        /// <param name="flowEnforcer">The bridge flow enforcer.</param>
        /// <param name="logger">The logger.</param>
        public MetadataBridge(
            BridgeFlowEnforcer flowEnforcer,
            ILogger<MetadataBridge> logger)
        {
            _flowEnforcer = flowEnforcer ?? throw new ArgumentNullException(nameof(flowEnforcer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Queries metadata from a remote realm if metadata read is allowed.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="query">The metadata query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the metadata query.</returns>
        /// <remarks>
        ///     T-REALM-04: When metadata:read is allowed, enables querying remote realm metadata APIs.
        /// </remarks>
        public async Task<BridgeOperationResult> QueryRemoteMetadataAsync(
            string localRealmId,
            string remoteRealmId,
            MetadataQuery query,
            CancellationToken cancellationToken = default)
        {
            return await _flowEnforcer.PerformMetadataReadAsync(
                localRealmId,
                remoteRealmId,
                async () =>
                {
                    // Implementation would:
                    // 1. Validate query doesn't request sensitive information
                    // 2. Make authenticated request to remote realm's metadata API
                    // 3. Filter response to remove any sensitive data
                    // 4. Return sanitized metadata

                    _logger.LogInformation(
                        "[MetadataBridge] Querying metadata from realm '{RemoteRealm}' for local realm '{LocalRealm}' - Query: {QueryType}",
                        remoteRealmId, localRealmId, query.QueryType);

                    // Validate query safety
                    if (!IsQuerySafe(query))
                    {
                        return BridgeOperationResult.Blocked("unsafe metadata query");
                    }

                    // Placeholder implementation
                    var result = new
                    {
                        QueryType = query.QueryType,
                        RemoteRealm = remoteRealmId,
                        LocalRealm = localRealmId,
                        Results = new[] { "safe-metadata-result-1", "safe-metadata-result-2" }
                    };

                    return BridgeOperationResult.CreateSuccess(result);
                },
                cancellationToken);
        }

        /// <summary>
        ///     Searches content in a remote realm if search read is allowed.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="searchQuery">The search query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the search operation.</returns>
        /// <remarks>
        ///     T-REALM-04: When search:read is allowed, enables using remote realm search results locally.
        /// </remarks>
        public async Task<BridgeOperationResult> SearchRemoteContentAsync(
            string localRealmId,
            string remoteRealmId,
            SearchQuery searchQuery,
            CancellationToken cancellationToken = default)
        {
            // Search read uses the same permission as metadata read for now
            return await _flowEnforcer.PerformMetadataReadAsync(
                localRealmId,
                remoteRealmId,
                async () =>
                {
                    // Implementation would:
                    // 1. Validate search query doesn't contain sensitive terms
                    // 2. Execute search against remote realm's search API
                    // 3. Filter results to remove private/inappropriate content
                    // 4. Return search results with proper attribution

                    _logger.LogInformation(
                        "[MetadataBridge] Searching content in realm '{RemoteRealm}' for local realm '{LocalRealm}' - Query: '{SearchTerm}'",
                        remoteRealmId, localRealmId, searchQuery.SearchTerm);

                    // Validate search safety
                    if (!IsSearchSafe(searchQuery))
                    {
                        return BridgeOperationResult.Blocked("unsafe search query");
                    }

                    // Placeholder implementation
                    var result = new
                    {
                        SearchTerm = searchQuery.SearchTerm,
                        RemoteRealm = remoteRealmId,
                        LocalRealm = localRealmId,
                        Results = new[]
                        {
                            new { Id = "result-1", Title = "Safe Search Result 1", Relevance = 0.95 },
                            new { Id = "result-2", Title = "Safe Search Result 2", Relevance = 0.87 }
                        }
                    };

                    return BridgeOperationResult.CreateSuccess(result);
                },
                cancellationToken);
        }

        /// <summary>
        ///     Gets realm information from a remote realm if metadata read is allowed.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The remote realm information.</returns>
        /// <remarks>
        ///     T-REALM-04: Provides basic realm discovery while respecting bridge policies.
        /// </remarks>
        public async Task<BridgeOperationResult> GetRemoteRealmInfoAsync(
            string localRealmId,
            string remoteRealmId,
            CancellationToken cancellationToken = default)
        {
            return await _flowEnforcer.PerformMetadataReadAsync(
                localRealmId,
                remoteRealmId,
                async () =>
                {
                    // Implementation would:
                    // 1. Query remote realm's public information endpoint
                    // 2. Return sanitized realm metadata (name, description, etc.)
                    // 3. Never expose sensitive information like governance roots

                    _logger.LogDebug(
                        "[MetadataBridge] Getting realm info for '{RemoteRealm}' from local realm '{LocalRealm}'",
                        remoteRealmId, localRealmId);

                    // Placeholder implementation - only return safe public information
                    var realmInfo = new
                    {
                        RealmId = remoteRealmId,
                        DisplayName = $"Realm {remoteRealmId}",
                        Description = "A connected realm",
                        IsPubliclyVisible = true,
                        // Never expose: GovernanceRoots, BootstrapNodes, internal policies
                    };

                    return BridgeOperationResult.CreateSuccess(realmInfo);
                },
                cancellationToken);
        }

        /// <summary>
        ///     Checks if metadata operations are allowed with a remote realm.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <returns>True if metadata operations are allowed.</returns>
        public bool IsMetadataAccessAllowed(string localRealmId, string remoteRealmId)
        {
            return _flowEnforcer.IsMetadataReadAllowed(localRealmId, remoteRealmId);
        }

        /// <summary>
        ///     Gets the metadata capabilities for a remote realm.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <returns>The metadata capabilities available for the remote realm.</returns>
        public MetadataCapabilities GetRemoteRealmCapabilities(string localRealmId, string remoteRealmId)
        {
            return new MetadataCapabilities
            {
                CanQueryMetadata = _flowEnforcer.IsMetadataReadAllowed(localRealmId, remoteRealmId),
                CanSearchContent = _flowEnforcer.IsSearchReadAllowed(localRealmId, remoteRealmId),
                CanGetRealmInfo = _flowEnforcer.IsMetadataReadAllowed(localRealmId, remoteRealmId)
            };
        }

        private static bool IsQuerySafe(MetadataQuery query)
        {
            // Validate that metadata queries don't request sensitive information
            var sensitivePatterns = new[]
            {
                "governance",
                "private",
                "secret",
                "password",
                "key",
                "token",
                "auth"
            };

            var queryText = $"{query.QueryType} {query.Parameters}".ToLowerInvariant();
            return !sensitivePatterns.Any(pattern => queryText.Contains(pattern));
        }

        private static bool IsSearchSafe(SearchQuery query)
        {
            // Validate that search queries don't contain malicious or sensitive terms
            var unsafePatterns = new[]
            {
                "admin",
                "root",
                "private",
                "secret",
                "password",
                "governance",
                "<script",
                "javascript:",
                "onload",
                "onerror"
            };

            var searchText = query.SearchTerm.ToLowerInvariant();
            return !unsafePatterns.Any(pattern => searchText.Contains(pattern)) &&
                   searchText.Length <= 200; // Reasonable length limit
        }
    }

    /// <summary>
    ///     Metadata query parameters.
    /// </summary>
    public class MetadataQuery
    {
        /// <summary>
        ///     Gets or sets the query type.
        /// </summary>
        public string QueryType { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the query parameters.
        /// </summary>
        public string Parameters { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the maximum number of results to return.
        /// </summary>
        public int MaxResults { get; set; } = 50;
    }

    /// <summary>
    ///     Search query parameters.
    /// </summary>
    public class SearchQuery
    {
        /// <summary>
        ///     Gets or sets the search term.
        /// </summary>
        public string SearchTerm { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the content type to search for.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        ///     Gets or sets the maximum number of results to return.
        /// </summary>
        public int MaxResults { get; set; } = 20;
    }

    /// <summary>
    ///     Metadata capabilities for a remote realm.
    /// </summary>
    public class MetadataCapabilities
    {
        /// <summary>
        ///     Gets or sets a value indicating whether querying metadata is allowed.
        /// </summary>
        public bool CanQueryMetadata { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether searching content is allowed.
        /// </summary>
        public bool CanSearchContent { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether getting realm info is allowed.
        /// </summary>
        public bool CanGetRealmInfo { get; set; }

        /// <summary>
        ///     Gets a value indicating whether any metadata operations are allowed.
        /// </summary>
        public bool AnyAllowed => CanQueryMetadata || CanSearchContent || CanGetRealmInfo;
    }
}
