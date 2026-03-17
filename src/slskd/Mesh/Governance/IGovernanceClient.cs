// <copyright file="IGovernanceClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Governance
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for governance document management and validation.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Base interface for governance operations.
    ///     Extended by IRealmAwareGovernanceClient for realm-specific governance.
    /// </remarks>
    public interface IGovernanceClient
    {
        /// <summary>
        ///     Validates a governance document.
        /// </summary>
        /// <param name="document">The governance document to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the document is valid.</returns>
        Task<bool> ValidateDocumentAsync(GovernanceDocument document, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stores a validated governance document.
        /// </summary>
        /// <param name="document">The governance document to store.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StoreDocumentAsync(GovernanceDocument document, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves a governance document by ID.
        /// </summary>
        /// <param name="documentId">The document ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The governance document, or null if not found.</returns>
        Task<GovernanceDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Extended interface for realm-aware governance operations.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Realm-aware extension that associates documents with specific realms
    ///     and validates signatures against realm-specific governance roots.
    /// </remarks>
    public interface IRealmAwareGovernanceClient : IGovernanceClient
    {
        /// <summary>
        ///     Validates a governance document for a specific realm.
        /// </summary>
        /// <param name="document">The governance document to validate.</param>
        /// <param name="realmId">The realm ID to validate against.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the document is valid for the realm.</returns>
        Task<bool> ValidateDocumentForRealmAsync(GovernanceDocument document, string realmId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stores a governance document associated with a specific realm.
        /// </summary>
        /// <param name="document">The governance document to store.</param>
        /// <param name="realmId">The realm ID to associate with the document.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StoreDocumentForRealmAsync(GovernanceDocument document, string realmId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves governance documents for a specific realm.
        /// </summary>
        /// <param name="realmId">The realm ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of governance documents for the realm.</returns>
        Task<IReadOnlyCollection<GovernanceDocument>> GetDocumentsForRealmAsync(string realmId, CancellationToken cancellationToken = default);
    }
}
