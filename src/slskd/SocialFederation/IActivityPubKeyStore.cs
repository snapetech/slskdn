// <copyright file="IActivityPubKeyStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Interface for ActivityPub keypair management.
    /// </summary>
    /// <remarks>
    ///     T-FED01: Keypair management for ActivityPub actors.
    ///     Manages Ed25519/RSA keypairs for HTTP signature verification.
    /// </remarks>
    public interface IActivityPubKeyStore
    {
        /// <summary>
        ///     Gets the public key for the specified actor.
        /// </summary>
        /// <param name="actorId">The actor identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The public key as a PEM string.</returns>
        Task<string> GetPublicKeyAsync(string actorId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the private key for the specified actor (for signing).
        /// </summary>
        /// <param name="actorId">The actor identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The private key (protected).</returns>
        Task<ProtectedPrivateKey> GetPrivateKeyAsync(string actorId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Ensures a keypair exists for the specified actor, creating one if necessary.
        /// </summary>
        /// <param name="actorId">The actor identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnsureKeypairAsync(string actorId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Rotates the keypair for the specified actor.
        /// </summary>
        /// <param name="actorId">The actor identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RotateKeypairAsync(string actorId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Verifies an HTTP signature using the actor's public key.
        /// </summary>
        /// <param name="actorId">The actor identifier.</param>
        /// <param name="signature">The signature to verify.</param>
        /// <param name="signedString">The signed string.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the signature is valid.</returns>
        Task<bool> VerifySignatureAsync(string actorId, string signature, string signedString, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Represents a protected private key.
    /// </summary>
    public sealed class ProtectedPrivateKey : IDisposable
    {
        private readonly string _privateKeyPem;
        private readonly IDisposable? _unprotector;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ProtectedPrivateKey"/> class.
        /// </summary>
        /// <param name="privateKeyPem">The private key in PEM format.</param>
        /// <param name="unprotector">Optional protector to dispose.</param>
        public ProtectedPrivateKey(string privateKeyPem, IDisposable? unprotector = null)
        {
            _privateKeyPem = privateKeyPem ?? throw new ArgumentNullException(nameof(privateKeyPem));
            _unprotector = unprotector;
        }

        /// <summary>
        ///     Gets the private key PEM string.
        /// </summary>
        public string PemString => _privateKeyPem;

        /// <inheritdoc/>
        public void Dispose()
        {
            _unprotector?.Dispose();
        }
    }
}

