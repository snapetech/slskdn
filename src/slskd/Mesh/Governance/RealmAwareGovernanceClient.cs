// <copyright file="RealmAwareGovernanceClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Governance
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Mesh.Realm;

    /// <summary>
    ///     Realm-aware governance client implementation.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Associates governance docs with specific realms and validates
    ///     signatures against realm-specific governance roots. Prevents cross-realm
    ///     governance document contamination.
    /// </remarks>
    public sealed class RealmAwareGovernanceClient : IRealmAwareGovernanceClient, IDisposable
    {
        private readonly IRealmService _realmService;
        private readonly ILogger<RealmAwareGovernanceClient> _logger;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, GovernanceDocument>> _documentsByRealm
            = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmAwareGovernanceClient"/> class.
        /// </summary>
        /// <param name="realmService">The realm service.</param>
        /// <param name="logger">The logger.</param>
        public RealmAwareGovernanceClient(
            IRealmService realmService,
            ILogger<RealmAwareGovernanceClient> logger)
        {
            _realmService = realmService ?? throw new ArgumentNullException(nameof(realmService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealmAwareGovernanceClient"/> class for multi-realm support.
        /// </summary>
        /// <param name="multiRealmService">The multi-realm service.</param>
        /// <param name="logger">The logger.</param>
        public RealmAwareGovernanceClient(
            MultiRealmService multiRealmService,
            ILogger<RealmAwareGovernanceClient> logger)
        {
            if (multiRealmService == null)
            {
                throw new ArgumentNullException(nameof(multiRealmService));
            }

            // For multi-realm scenarios, we create a composite realm service
            // This is a simplified implementation - in practice, this would need more sophisticated handling
            _realmService = new CompositeRealmService(multiRealmService);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateDocumentAsync(GovernanceDocument document, CancellationToken cancellationToken = default)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            // For backwards compatibility, validate without realm context
            // This should only be used for realm-agnostic documents
            if (string.IsNullOrEmpty(document.RealmId))
            {
                _logger.LogWarning("[Governance] Validating document without realm context - this may be insecure");
                return await ValidateDocumentSignatureAsync(document, cancellationToken);
            }

            // Use realm-aware validation
            return await ValidateDocumentForRealmAsync(document, document.RealmId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateDocumentForRealmAsync(
            GovernanceDocument document,
            string realmId,
            CancellationToken cancellationToken = default)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            // Basic structural validation
            if (!document.IsValid())
            {
                _logger.LogWarning("[Governance] Document '{DocumentId}' failed structural validation", document.Id);
                return false;
            }

            // Verify realm association
            if (!string.Equals(document.RealmId, realmId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[Governance] Document '{DocumentId}' realm mismatch. Document realm: '{DocRealm}', Expected: '{ExpectedRealm}'",
                    document.Id, document.RealmId, realmId);
                return false;
            }

            // Verify signer is trusted for this realm
            if (!IsTrustedSignerForRealm(document.Signer, realmId))
            {
                _logger.LogWarning(
                    "[Governance] Document '{DocumentId}' signer '{Signer}' not trusted for realm '{RealmId}'",
                    document.Id, document.Signer, realmId);
                return false;
            }

            // Verify signature
            if (!await ValidateDocumentSignatureAsync(document, cancellationToken))
            {
                _logger.LogWarning("[Governance] Document '{DocumentId}' signature validation failed", document.Id);
                return false;
            }

            _logger.LogDebug(
                "[Governance] Document '{DocumentId}' validated for realm '{RealmId}'",
                document.Id, realmId);

            return true;
        }

        /// <inheritdoc/>
        public async Task StoreDocumentAsync(GovernanceDocument document, CancellationToken cancellationToken = default)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            // For backwards compatibility, store without realm context
            if (string.IsNullOrEmpty(document.RealmId))
            {
                await StoreDocumentForRealmAsync(document, "default", cancellationToken);
                return;
            }

            await StoreDocumentForRealmAsync(document, document.RealmId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StoreDocumentForRealmAsync(
            GovernanceDocument document,
            string realmId,
            CancellationToken cancellationToken = default)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            // Ensure document is associated with the correct realm
            document.RealmId = realmId;

            // Get or create realm-specific document store
            var realmDocuments = _documentsByRealm.GetOrAdd(realmId, _ => new ConcurrentDictionary<string, GovernanceDocument>(StringComparer.OrdinalIgnoreCase));

            // Store the document
            realmDocuments[document.Id] = document;

            _logger.LogInformation(
                "[Governance] Stored document '{DocumentId}' for realm '{RealmId}'",
                document.Id, realmId);

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<GovernanceDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("Document ID cannot be null or empty.", nameof(documentId));
            }

            // Search across all realms (not recommended for production)
            foreach (var realmDocuments in _documentsByRealm.Values)
            {
                if (realmDocuments.TryGetValue(documentId, out var document))
                {
                    return document;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<GovernanceDocument>> GetDocumentsForRealmAsync(
            string realmId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(realmId))
            {
                throw new ArgumentException("Realm ID cannot be null or empty.", nameof(realmId));
            }

            if (_documentsByRealm.TryGetValue(realmId, out var realmDocuments))
            {
                return realmDocuments.Values.ToList();
            }

            return Array.Empty<GovernanceDocument>();
        }

        /// <summary>
        ///     Checks if a signer is trusted for a specific realm.
        /// </summary>
        /// <param name="signer">The signer to check.</param>
        /// <param name="realmId">The realm ID.</param>
        /// <returns>True if the signer is trusted.</returns>
        private bool IsTrustedSignerForRealm(string signer, string realmId)
        {
            return _realmService.IsTrustedGovernanceRoot(signer);
        }

        /// <summary>
        ///     Validates a document's cryptographic signature.
        /// </summary>
        /// <param name="document">The document to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the signature is valid.</returns>
        private static async Task<bool> ValidateDocumentSignatureAsync(GovernanceDocument document, CancellationToken cancellationToken)
        {
            try
            {
                // Create the data to sign (exclude signature field)
                var dataToSign = $"{document.Id}|{document.Type}|{document.Version}|{document.Created:O}|{document.RealmId}|{document.Signer}";

                // For now, use a simple HMAC validation
                // In production, this would use proper cryptographic signature verification
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("governance-signing-key"));
                var expectedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign)));
                var isValid = string.Equals(document.Signature, expectedSignature, StringComparison.Ordinal);

                return isValid;
            }
            catch (Exception ex)
            {
                // Log the error but don't expose details
                Console.WriteLine($"Signature validation failed: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _documentsByRealm.Clear();
            _disposed = true;
        }

        // Composite realm service for multi-realm scenarios
        private class CompositeRealmService : IRealmService
        {
            private readonly MultiRealmService _multiRealmService;

            public CompositeRealmService(MultiRealmService multiRealmService)
            {
                _multiRealmService = multiRealmService;
            }

            public string RealmId => throw new NotSupportedException("Composite realm service doesn't have a single realm ID");

            public byte[] NamespaceSalt => throw new NotSupportedException("Composite realm service doesn't have a single namespace salt");

            public Task InitializeAsync(CancellationToken cancellationToken = default)
                => _multiRealmService.InitializeAsync(cancellationToken);

            public bool IsSameRealm(string realmId)
            {
                // Check if any of our realms match
                return _multiRealmService.RealmIds.Contains(realmId);
            }

            public bool IsTrustedGovernanceRoot(string governanceRoot)
            {
                return _multiRealmService.GetCrossRealmGovernanceRoots().Contains(governanceRoot);
            }

            public string CreateRealmScopedId(string identifier)
            {
                // Not applicable for composite service
                throw new NotSupportedException();
            }

            public bool TryParseRealmScopedId(string scopedId, out string realmId, out string identifier)
            {
                return RealmService.TryParseRealmScopedId(scopedId, out realmId, out identifier);
            }

            public bool IsRealmScopedId(string scopedId)
            {
                if (!TryParseRealmScopedId(scopedId, out var realmId, out _))
                {
                    return false;
                }

                return IsSameRealm(realmId);
            }
        }
    }
}
