// <copyright file="IMessageSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for signing and verifying pod messages.
/// </summary>
public interface IMessageSigner
{
    /// <summary>
    /// Signs a pod message with the sender's private key.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <param name="privateKey">The sender's private key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signed message.</returns>
    Task<PodMessage> SignMessageAsync(PodMessage message, string privateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the signature of a pod message.
    /// </summary>
    /// <param name="message">The message to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
    Task<bool> VerifyMessageAsync(PodMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a new key pair for message signing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated key pair.</returns>
    Task<KeyPair> GenerateKeyPairAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets signature statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Signature statistics.</returns>
    Task<SignatureStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A cryptographic key pair.
/// </summary>
public record KeyPair(
    string PublicKey,
    string PrivateKey);

/// <summary>
/// Message signature statistics.
/// </summary>
public record SignatureStats(
    long TotalSignaturesCreated,
    long TotalSignaturesVerified,
    long SuccessfulVerifications,
    long FailedVerifications,
    double AverageSigningTimeMs,
    double AverageVerificationTimeMs,
    DateTimeOffset LastSignatureOperation);
