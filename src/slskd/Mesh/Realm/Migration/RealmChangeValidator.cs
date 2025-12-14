// <copyright file="RealmChangeValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm.Migration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Mesh.Realm;

    /// <summary>
    ///     Validates and provides guardrails for realm configuration changes.
    /// </summary>
    /// <remarks>
    ///     T-REALM-05: Realm Change & Migration Guardrails - ensures safe realm transitions
    ///     with warnings, confirmations, and documented expectations.
    /// </remarks>
    public class RealmChangeValidator
    {
        private readonly IRealmService _currentRealmService;
        private readonly ILogger<RealmChangeValidator> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmChangeValidator"/> class.
        /// </summary>
        /// <param name="currentRealmService">The current realm service.</param>
        /// <param name="logger">The logger.</param>
        public RealmChangeValidator(
            IRealmService currentRealmService,
            ILogger<RealmChangeValidator> logger)
        {
            _currentRealmService = currentRealmService ?? throw new ArgumentNullException(nameof(currentRealmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Validates a proposed realm configuration change.
        /// </summary>
        /// <param name="proposedConfig">The proposed new realm configuration.</param>
        /// <param name="confirmationToken">The confirmation token (must match current realm ID).</param>
        /// <returns>The validation result with warnings and requirements.</returns>
        /// <remarks>
        ///     T-REALM-05: High-friction validation requiring explicit confirmation.
        /// </remarks>
        public async Task<RealmChangeValidationResult> ValidateRealmChangeAsync(
            RealmConfig proposedConfig,
            string? confirmationToken = null)
        {
            if (proposedConfig == null)
            {
                throw new ArgumentNullException(nameof(proposedConfig));
            }

            var result = new RealmChangeValidationResult
            {
                CurrentRealmId = _currentRealmService.RealmId,
                ProposedRealmId = proposedConfig.Id,
                IsValid = false,
                Warnings = new List<string>(),
                Requirements = new List<string>(),
                BreakingChanges = new List<string>()
            };

            // Basic validation of proposed config
            var configErrors = proposedConfig.Validate().ToList();
            if (configErrors.Any())
            {
                result.ValidationErrors = configErrors.Select(e => e.ErrorMessage ?? "Unknown error").ToList();
                _logger.LogError(
                    "[RealmChange] Proposed realm configuration is invalid: {Errors}",
                    string.Join("; ", result.ValidationErrors));
                return result;
            }

            // Check if this is actually a change
            if (string.Equals(_currentRealmService.RealmId, proposedConfig.Id, StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = true;
                result.Warnings.Add("No realm change detected - same realm ID");
                _logger.LogInformation("[RealmChange] No realm change - same realm ID: {RealmId}", proposedConfig.Id);
                return result;
            }

            // HIGH FRICTION: Require explicit confirmation
            if (string.IsNullOrWhiteSpace(confirmationToken) ||
                !string.Equals(confirmationToken, _currentRealmService.RealmId, StringComparison.Ordinal))
            {
                result.Requirements.Add($"Type the current realm ID '{_currentRealmService.RealmId}' to confirm this destructive operation");
                result.Warnings.Add("This operation will disconnect from the current realm and may break existing relationships");
                _logger.LogWarning(
                    "[RealmChange] Realm change requires confirmation. Current realm: {Current}, Proposed: {Proposed}",
                    _currentRealmService.RealmId, proposedConfig.Id);
                return result;
            }

            // Analyze breaking changes
            await AnalyzeBreakingChangesAsync(proposedConfig, result);

            // Add standard warnings
            AddStandardWarnings(result);

            // If we get here, the change is considered valid (though potentially destructive)
            result.IsValid = true;
            result.Warnings.Add("Realm change approved - this is a destructive operation that cannot be easily undone");

            _logger.LogWarning(
                "[RealmChange] Realm change validated and approved. Current: {Current}, New: {New}, Breaking changes: {Count}",
                _currentRealmService.RealmId, proposedConfig.Id, result.BreakingChanges.Count);

            return result;
        }

        /// <summary>
        ///     Validates a proposed multi-realm configuration change.
        /// </summary>
        /// <param name="proposedConfig">The proposed new multi-realm configuration.</param>
        /// <param name="confirmationToken">The confirmation token.</param>
        /// <returns>The validation result.</returns>
        public async Task<MultiRealmChangeValidationResult> ValidateMultiRealmChangeAsync(
            MultiRealmConfig proposedConfig,
            string? confirmationToken = null)
        {
            if (proposedConfig == null)
            {
                throw new ArgumentNullException(nameof(proposedConfig));
            }

            var result = new MultiRealmChangeValidationResult
            {
                CurrentRealmCount = 1, // Assuming single realm currently
                ProposedRealmCount = proposedConfig.Realms.Length,
                IsValid = false,
                Warnings = new List<string>(),
                Requirements = new List<string>(),
                RealmChanges = new List<RealmChangeValidationResult>()
            };

            // Validate the multi-realm config
            var configErrors = proposedConfig.Validate().ToList();
            if (configErrors.Any())
            {
                result.ValidationErrors = configErrors.Select(e => e.ErrorMessage ?? "Unknown error").ToList();
                return result;
            }

            // Validate each realm change
            foreach (var proposedRealm in proposedConfig.Realms)
            {
                var realmResult = await ValidateRealmChangeAsync(proposedRealm, confirmationToken);
                result.RealmChanges.Add(realmResult);
            }

            // Aggregate results
            result.IsValid = result.RealmChanges.All(r => r.IsValid);
            result.Warnings.AddRange(result.RealmChanges.SelectMany(r => r.Warnings));
            result.Requirements.AddRange(result.RealmChanges.SelectMany(r => r.Requirements));

            // Add multi-realm specific warnings
            if (proposedConfig.Realms.Length > 1)
            {
                result.Warnings.Add("Multi-realm configuration enables bridging between realms");
                result.Warnings.Add("Bridge policies will control cross-realm interactions");
            }

            if (proposedConfig.IsBridgingEnabled)
            {
                result.Warnings.Add("Bridging is enabled - flows between realms will be allowed based on bridge policies");
            }
            else
            {
                result.Warnings.Add("Bridging is disabled - realms will be completely isolated");
            }

            return result;
        }

        private async Task AnalyzeBreakingChangesAsync(RealmConfig proposedConfig, RealmChangeValidationResult result)
        {
            // Governance root changes
            var currentRoots = new[] { "current-governance-root" }; // Would get from current config
            var newRoots = proposedConfig.GovernanceRoots;
            if (!currentRoots.SequenceEqual(newRoots))
            {
                result.BreakingChanges.Add("Governance roots changed - existing governance documents may become invalid");
                result.BreakingChanges.Add("Pod may lose authority to validate governance in new realm");
            }

            // Bootstrap node changes
            var currentBootstrap = new[] { "current-bootstrap-node" }; // Would get from current config
            var newBootstrap = proposedConfig.BootstrapNodes;
            if (!currentBootstrap.SequenceEqual(newBootstrap))
            {
                result.BreakingChanges.Add("Bootstrap nodes changed - pod will need to discover new peers");
                result.BreakingChanges.Add("Existing mesh connections will be lost");
            }

            // Social/federation impact
            result.BreakingChanges.Add("All ActivityPub follows and relationships will be lost");
            result.BreakingChanges.Add("Federation relationships with other instances may become invalid");
            result.BreakingChanges.Add("Social connections and follows will need to be re-established");

            // Content and data impact
            result.BreakingChanges.Add("Local content discovery may be affected by realm change");
            result.BreakingChanges.Add("Peer relationships and trust may need rebuilding");
            result.BreakingChanges.Add("Gossip and reputation data may not transfer between realms");

            await Task.CompletedTask; // For async compatibility
        }

        private static void AddStandardWarnings(RealmChangeValidationResult result)
        {
            result.Warnings.AddRange(new[]
            {
                "This operation will disconnect the pod from its current realm",
                "The pod will need to rejoin the mesh network with new identity",
                "All existing connections will be lost and need re-establishing",
                "This operation cannot be easily undone",
                "Backup important data before proceeding",
                "Plan for downtime during realm transition"
            });
        }
    }

    /// <summary>
    ///     Result of a realm change validation.
    /// </summary>
    public class RealmChangeValidationResult
    {
        /// <summary>
        ///     Gets or sets the current realm ID.
        /// </summary>
        public string? CurrentRealmId { get; set; }

        /// <summary>
        ///     Gets or sets the proposed new realm ID.
        /// </summary>
        public string? ProposedRealmId { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the change is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        ///     Gets or sets the validation errors (if any).
        /// </summary>
        public List<string>? ValidationErrors { get; set; }

        /// <summary>
        ///     Gets or sets the warnings about the change.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the requirements that must be met.
        /// </summary>
        public List<string> Requirements { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the breaking changes that will occur.
        /// </summary>
        public List<string> BreakingChanges { get; set; } = new List<string>();

        /// <summary>
        ///     Gets a value indicating whether confirmation is required.
        /// </summary>
        public bool RequiresConfirmation => Requirements.Any(r =>
            r.Contains("confirmation", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        ///     Gets a summary of the validation result.
        /// </summary>
        public string Summary
        {
            get
            {
                if (!IsValid)
                {
                    return ValidationErrors?.Any() == true
                        ? $"Invalid: {string.Join(", ", ValidationErrors)}"
                        : "Invalid: Requirements not met";
                }

                var changeType = CurrentRealmId == ProposedRealmId ? "No change" : "Realm change";
                return $"{changeType}: {BreakingChanges.Count} breaking changes, {Warnings.Count} warnings";
            }
        }
    }

    /// <summary>
    ///     Result of a multi-realm change validation.
    /// </summary>
    public class MultiRealmChangeValidationResult : RealmChangeValidationResult
    {
        /// <summary>
        ///     Gets or sets the current number of realms.
        /// </summary>
        public int CurrentRealmCount { get; set; }

        /// <summary>
        ///     Gets or sets the proposed number of realms.
        /// </summary>
        public int ProposedRealmCount { get; set; }

        /// <summary>
        ///     Gets or sets the individual realm change results.
        /// </summary>
        public List<RealmChangeValidationResult> RealmChanges { get; set; } = new List<RealmChangeValidationResult>();

        /// <summary>
        ///     Gets a value indicating whether this is a transition to multi-realm.
        /// </summary>
        public bool IsTransitionToMultiRealm => CurrentRealmCount == 1 && ProposedRealmCount > 1;

        /// <summary>
        ///     Gets a value indicating whether this is a transition from multi-realm to single.
        /// </summary>
        public bool IsTransitionFromMultiRealm => CurrentRealmCount > 1 && ProposedRealmCount == 1;
    }
}


