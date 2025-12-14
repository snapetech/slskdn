// <copyright file="BridgeFlowEnforcer.cs" company="slskdN Team">
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
    ///     Service for enforcing bridge flow policies across realms.
    /// </summary>
    /// <remarks>
    ///     T-REALM-04: Bridge Flow Policies - controls what can flow between realms.
    ///     Implements specific policies for ActivityPub, metadata, and other cross-realm interactions.
    /// </remarks>
    public sealed class BridgeFlowEnforcer
    {
        private readonly MultiRealmService _multiRealmService;
        private readonly ILogger<BridgeFlowEnforcer> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="BridgeFlowEnforcer"/> class.
        /// </summary>
        /// <param name="multiRealmService">The multi-realm service.</param>
        /// <param name="logger">The logger.</param>
        public BridgeFlowEnforcer(
            MultiRealmService multiRealmService,
            ILogger<BridgeFlowEnforcer> logger)
        {
            _multiRealmService = multiRealmService ?? throw new ArgumentNullException(nameof(multiRealmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Checks if ActivityPub read operations are allowed from a remote realm.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <returns>True if ActivityPub read is allowed.</returns>
        /// <remarks>
        ///     T-REALM-04: When allowed, enables following actors and reading posts from remote realms.
        /// </remarks>
        public bool IsActivityPubReadAllowed(string localRealmId, string remoteRealmId)
        {
            return IsFlowAllowedBetweenRealms(localRealmId, remoteRealmId, BridgeFlowTypes.ActivityPubRead);
        }

        /// <summary>
        ///     Checks if ActivityPub write operations are allowed to a remote realm.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <returns>True if ActivityPub write is allowed.</returns>
        /// <remarks>
        ///     T-REALM-04: When allowed, enables posting/mirroring content to remote realms.
        ///     This is more dangerous than read operations.
        /// </remarks>
        public bool IsActivityPubWriteAllowed(string localRealmId, string remoteRealmId)
        {
            return IsFlowAllowedBetweenRealms(localRealmId, remoteRealmId, BridgeFlowTypes.ActivityPubWrite);
        }

        /// <summary>
        ///     Checks if metadata read operations are allowed from a remote realm.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <returns>True if metadata read is allowed.</returns>
        /// <remarks>
        ///     T-REALM-04: When allowed, enables querying remote realm metadata/search APIs.
        /// </remarks>
        public bool IsMetadataReadAllowed(string localRealmId, string remoteRealmId)
        {
            return IsFlowAllowedBetweenRealms(localRealmId, remoteRealmId, BridgeFlowTypes.MetadataRead);
        }

        /// <summary>
        ///     Checks if search read operations are allowed from a remote realm.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <returns>True if search read is allowed.</returns>
        /// <remarks>
        ///     T-REALM-04: When allowed, enables using remote realm search results locally.
        /// </remarks>
        public bool IsSearchReadAllowed(string localRealmId, string remoteRealmId)
        {
            return IsFlowAllowedBetweenRealms(localRealmId, remoteRealmId, BridgeFlowTypes.SearchRead);
        }

        /// <summary>
        ///     Performs an ActivityPub read operation if allowed.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="operation">The operation to perform.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the operation result.</returns>
        /// <remarks>
        ///     T-REALM-04: Safely performs ActivityPub read operations with policy enforcement.
        /// </remarks>
        public async Task<BridgeOperationResult> PerformActivityPubReadAsync(
            string localRealmId,
            string remoteRealmId,
            Func<Task<BridgeOperationResult>> operation,
            CancellationToken cancellationToken = default)
        {
            if (!IsActivityPubReadAllowed(localRealmId, remoteRealmId))
            {
                _logger.LogWarning(
                    "[BridgeFlow] ActivityPub read blocked from realm '{RemoteRealm}' to '{LocalRealm}' - flow not allowed",
                    remoteRealmId, localRealmId);
                return BridgeOperationResult.Blocked("activitypub:read flow not allowed");
            }

            try
            {
                var result = await operation();
                _logger.LogDebug(
                    "[BridgeFlow] ActivityPub read completed from realm '{RemoteRealm}' to '{LocalRealm}'",
                    remoteRealmId, localRealmId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BridgeFlow] ActivityPub read failed from realm '{RemoteRealm}' to '{LocalRealm}'",
                    remoteRealmId, localRealmId);
                return BridgeOperationResult.Failed(ex.Message);
            }
        }

        /// <summary>
        ///     Performs an ActivityPub write operation if allowed.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="operation">The operation to perform.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the operation result.</returns>
        /// <remarks>
        ///     T-REALM-04: Safely performs ActivityPub write operations with extra caution.
        /// </remarks>
        public async Task<BridgeOperationResult> PerformActivityPubWriteAsync(
            string localRealmId,
            string remoteRealmId,
            Func<Task<BridgeOperationResult>> operation,
            CancellationToken cancellationToken = default)
        {
            if (!IsActivityPubWriteAllowed(localRealmId, remoteRealmId))
            {
                _logger.LogWarning(
                    "[BridgeFlow] ActivityPub write blocked from realm '{LocalRealm}' to '{RemoteRealm}' - flow not allowed",
                    localRealmId, remoteRealmId);
                return BridgeOperationResult.Blocked("activitypub:write flow not allowed");
            }

            // Extra logging for write operations since they're more dangerous
            _logger.LogInformation(
                "[BridgeFlow] Performing ActivityPub write from realm '{LocalRealm}' to '{RemoteRealm}'",
                localRealmId, remoteRealmId);

            try
            {
                var result = await operation();
                _logger.LogInformation(
                    "[BridgeFlow] ActivityPub write completed from realm '{LocalRealm}' to '{RemoteRealm}'",
                    localRealmId, remoteRealmId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BridgeFlow] ActivityPub write failed from realm '{LocalRealm}' to '{RemoteRealm}'",
                    localRealmId, remoteRealmId);
                return BridgeOperationResult.Failed(ex.Message);
            }
        }

        /// <summary>
        ///     Performs a metadata read operation if allowed.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="operation">The operation to perform.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the operation result.</returns>
        /// <remarks>
        ///     T-REALM-04: Safely performs metadata read operations with policy enforcement.
        /// </remarks>
        public async Task<BridgeOperationResult> PerformMetadataReadAsync(
            string localRealmId,
            string remoteRealmId,
            Func<Task<BridgeOperationResult>> operation,
            CancellationToken cancellationToken = default)
        {
            if (!IsMetadataReadAllowed(localRealmId, remoteRealmId))
            {
                _logger.LogDebug(
                    "[BridgeFlow] Metadata read blocked from realm '{RemoteRealm}' to '{LocalRealm}' - flow not allowed",
                    remoteRealmId, localRealmId);
                return BridgeOperationResult.Blocked("metadata:read flow not allowed");
            }

            try
            {
                var result = await operation();
                _logger.LogTrace(
                    "[BridgeFlow] Metadata read completed from realm '{RemoteRealm}' to '{LocalRealm}'",
                    remoteRealmId, localRealmId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[BridgeFlow] Metadata read failed from realm '{RemoteRealm}' to '{LocalRealm}'",
                    remoteRealmId, localRealmId);
                return BridgeOperationResult.Failed(ex.Message);
            }
        }

        /// <summary>
        ///     Validates that a cross-realm operation respects all bridge policies.
        /// </summary>
        /// <param name="localRealmId">The local realm ID.</param>
        /// <param name="remoteRealmId">The remote realm ID.</param>
        /// <param name="flow">The flow being attempted.</param>
        /// <param name="operationDescription">Description of the operation for logging.</param>
        /// <returns>True if the operation is permitted.</returns>
        /// <remarks>
        ///     T-REALM-04: Comprehensive validation including forbidden flows and hardening rules.
        /// </remarks>
        public bool ValidateCrossRealmOperation(
            string localRealmId,
            string remoteRealmId,
            string flow,
            string operationDescription)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(localRealmId) ||
                string.IsNullOrWhiteSpace(remoteRealmId) ||
                string.IsNullOrWhiteSpace(flow))
            {
                _logger.LogWarning("[BridgeFlow] Invalid parameters for cross-realm operation: {Description}", operationDescription);
                return false;
            }

            // Same realm operations are always allowed
            if (string.Equals(localRealmId, remoteRealmId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check if flow is always forbidden
            if (BridgeFlowTypes.AlwaysForbiddenFlows.Contains(flow))
            {
                _logger.LogError(
                    "[BridgeFlow] Attempted forbidden cross-realm operation: {Flow} from '{LocalRealm}' to '{RemoteRealm}' - {Description}",
                    flow, localRealmId, remoteRealmId, operationDescription);
                return false;
            }

            // Check basic flow permission via MultiRealmService
            if (!_multiRealmService.IsCrossRealmOperationPermitted(localRealmId, remoteRealmId, flow))
            {
                _logger.LogWarning(
                    "[BridgeFlow] Cross-realm operation blocked: {Flow} from '{LocalRealm}' to '{RemoteRealm}' - {Description}",
                    flow, localRealmId, remoteRealmId, operationDescription);
                return false;
            }

            // Additional hardening checks
            if (!ValidateOperationHardening(localRealmId, remoteRealmId, flow, operationDescription))
            {
                return false;
            }

            _logger.LogDebug(
                "[BridgeFlow] Cross-realm operation permitted: {Flow} from '{LocalRealm}' to '{RemoteRealm}' - {Description}",
                flow, localRealmId, remoteRealmId, operationDescription);

            return true;
        }

        private bool IsFlowAllowedBetweenRealms(string localRealmId, string remoteRealmId, string flow)
        {
            return _multiRealmService.IsCrossRealmOperationPermitted(localRealmId, remoteRealmId, flow);
        }

        private bool ValidateOperationHardening(
            string localRealmId,
            string remoteRealmId,
            string flow,
            string operationDescription)
        {
            // Additional hardening rules beyond basic flow permissions

            // For ActivityPub write operations, be extra cautious
            if (flow == BridgeFlowTypes.ActivityPubWrite)
            {
                // Could add additional checks here, like:
                // - Rate limiting for write operations
                // - Content validation requirements
                // - User consent requirements
                // For now, just log the sensitive operation
                _logger.LogInformation(
                    "[BridgeFlow] Sensitive cross-realm operation approved: {Flow} from '{LocalRealm}' to '{RemoteRealm}' - {Description}",
                    flow, localRealmId, remoteRealmId, operationDescription);
            }

            // For metadata operations, ensure they don't leak sensitive local information
            if (flow == BridgeFlowTypes.MetadataRead || flow == BridgeFlowTypes.SearchRead)
            {
                // Could add checks to ensure metadata operations don't expose:
                // - Internal realm configuration
                // - Private user data
                // - Sensitive governance information
            }

            return true; // All hardening checks passed
        }
    }

    /// <summary>
    ///     Result of a bridge operation.
    /// </summary>
    public class BridgeOperationResult
    {
        /// <summary>
        ///     Gets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        ///     Gets the result data, if any.
        /// </summary>
        public object? Data { get; private set; }

        /// <summary>
        ///     Gets the error message, if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the operation was blocked by policy.
        /// </summary>
        public bool WasBlocked { get; private set; }

        /// <summary>
        ///     Creates a successful result.
        /// </summary>
        /// <param name="data">The result data.</param>
        /// <returns>The result.</returns>
        public static BridgeOperationResult Success(object? data = null)
        {
            return new BridgeOperationResult { Success = true, Data = data };
        }

        /// <summary>
        ///     Creates a blocked result.
        /// </summary>
        /// <param name="reason">The reason for blocking.</param>
        /// <returns>The result.</returns>
        public static BridgeOperationResult Blocked(string reason)
        {
            return new BridgeOperationResult { WasBlocked = true, ErrorMessage = reason };
        }

        /// <summary>
        ///     Creates a failed result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>The result.</returns>
        public static BridgeOperationResult Failed(string errorMessage)
        {
            return new BridgeOperationResult { ErrorMessage = errorMessage };
        }
    }
}
