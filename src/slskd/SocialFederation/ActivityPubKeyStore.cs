// <copyright file="ActivityPubKeyStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Logging;
    using NSec.Cryptography;

    /// <summary>
    ///     ActivityPub keypair management implementation.
    /// </summary>
    /// <remarks>
    ///     T-FED01: Keypair management for ActivityPub HTTP signatures.
    ///     Uses Ed25519 for modern, secure keypairs with data protection.
    /// </remarks>
    public sealed class ActivityPubKeyStore : IActivityPubKeyStore, IDisposable
    {
        private readonly IDataProtector _dataProtector;
        private readonly ILogger<ActivityPubKeyStore> _logger;
        private readonly ConcurrentDictionary<string, KeypairInfo> _keypairs = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ActivityPubKeyStore"/> class.
        /// </summary>
        /// <param name="dataProtector">The data protector for private key encryption.</param>
        /// <param name="logger">The logger.</param>
        public ActivityPubKeyStore(IDataProtector dataProtector, ILogger<ActivityPubKeyStore> logger)
        {
            _dataProtector = dataProtector ?? throw new ArgumentNullException(nameof(dataProtector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<string> GetPublicKeyAsync(string actorId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID cannot be null or empty.", nameof(actorId));
            }

            await EnsureKeypairAsync(actorId, cancellationToken);

            if (_keypairs.TryGetValue(actorId, out var keypairInfo))
            {
                return keypairInfo.PublicKeyPem;
            }

            throw new InvalidOperationException($"Keypair not found for actor {actorId}");
        }

        /// <inheritdoc/>
        public async Task<ProtectedPrivateKey> GetPrivateKeyAsync(string actorId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID cannot be null or empty.", nameof(actorId));
            }

            await EnsureKeypairAsync(actorId, cancellationToken);

            if (_keypairs.TryGetValue(actorId, out var keypairInfo))
            {
                // Decrypt the private key for use
                var decryptedPem = _dataProtector.Unprotect(keypairInfo.ProtectedPrivateKeyPem);
                return new ProtectedPrivateKey(decryptedPem, null);
            }

            throw new InvalidOperationException($"Keypair not found for actor {actorId}");
        }

        /// <inheritdoc/>
        public async Task EnsureKeypairAsync(string actorId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID cannot be null or empty.", nameof(actorId));
            }

            // Check if we already have a keypair
            if (_keypairs.ContainsKey(actorId))
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_keypairs.ContainsKey(actorId))
                {
                    return;
                }

                _logger.LogInformation("[ActivityPub] Generating keypair for actor {ActorId}", actorId);

                // Generate Ed25519 keypair
                using var algorithm = SignatureAlgorithm.Ed25519;
                using var key = Key.Create(algorithm);

                // Export public key
                var publicKeyBytes = key.Export(KeyBlobFormat.PkixPublicKey);
                var publicKeyPem = ConvertToPem(publicKeyBytes, "PUBLIC KEY");

                // Export private key
                var privateKeyBytes = key.Export(KeyBlobFormat.PkixPrivateKey);
                var privateKeyPem = ConvertToPem(privateKeyBytes, "PRIVATE KEY");

                // Protect the private key
                var protectedPrivateKeyPem = _dataProtector.Protect(privateKeyPem);

                var keypairInfo = new KeypairInfo
                {
                    ActorId = actorId,
                    PublicKeyPem = publicKeyPem,
                    ProtectedPrivateKeyPem = protectedPrivateKeyPem,
                    Created = DateTime.UtcNow,
                    Algorithm = "Ed25519"
                };

                _keypairs[actorId] = keypairInfo;

                _logger.LogInformation("[ActivityPub] Keypair generated for actor {ActorId}", actorId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task RotateKeypairAsync(string actorId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID cannot be null or empty.", nameof(actorId));
            }

            _logger.LogInformation("[ActivityPub] Rotating keypair for actor {ActorId}", actorId);

            // Remove existing keypair
            _keypairs.TryRemove(actorId, out _);

            // Generate new keypair
            await EnsureKeypairAsync(actorId, cancellationToken);

            _logger.LogInformation("[ActivityPub] Keypair rotated for actor {ActorId}", actorId);
        }

        /// <inheritdoc/>
        public async Task<bool> VerifySignatureAsync(string actorId, string signature, string signedString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentException("Actor ID cannot be null or empty.", nameof(actorId));
            }

            if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(signedString))
            {
                return false;
            }

            try
            {
                var publicKeyPem = await GetPublicKeyAsync(actorId, cancellationToken);

                // Parse PEM and verify signature
                using var algorithm = SignatureAlgorithm.Ed25519;
                var publicKeyBytes = ConvertFromPem(publicKeyPem);
                using var key = Key.Import(algorithm, publicKeyBytes, KeyBlobFormat.PkixPublicKey);

                var signatureBytes = Convert.FromBase64String(signature);
                var messageBytes = Encoding.UTF8.GetBytes(signedString);

                return algorithm.Verify(key, messageBytes, signatureBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ActivityPub] Signature verification failed for actor {ActorId}", actorId);
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

            _semaphore.Dispose();
            _disposed = true;
        }

        private static string ConvertToPem(byte[] keyBytes, string label)
        {
            var base64 = Convert.ToBase64String(keyBytes);
            var pem = new StringBuilder();
            pem.AppendLine($"-----BEGIN {label}-----");

            // Insert line breaks every 64 characters
            for (var i = 0; i < base64.Length; i += 64)
            {
                var length = Math.Min(64, base64.Length - i);
                pem.AppendLine(base64.Substring(i, length));
            }

            pem.AppendLine($"-----END {label}-----");
            return pem.ToString();
        }

        private static byte[] ConvertFromPem(string pem)
        {
            // Extract base64 content between BEGIN and END markers
            var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var base64Lines = new System.Collections.Generic.List<string>();

            var inKey = false;
            foreach (var line in lines)
            {
                if (line.Contains("-----BEGIN"))
                {
                    inKey = true;
                    continue;
                }

                if (line.Contains("-----END"))
                {
                    break;
                }

                if (inKey)
                {
                    base64Lines.Add(line.Trim());
                }
            }

            var base64 = string.Concat(base64Lines);
            return Convert.FromBase64String(base64);
        }

        private sealed class KeypairInfo
        {
            public string ActorId { get; set; } = string.Empty;
            public string PublicKeyPem { get; set; } = string.Empty;
            public string ProtectedPrivateKeyPem { get; set; } = string.Empty;
            public DateTime Created { get; set; }
            public string Algorithm { get; set; } = string.Empty;
        }
    }
}
