// <copyright file="IEd25519KeyPairGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    /// <summary>
    ///     Generates Ed25519 (public, private) keypairs as PEM strings.
    ///     Abstraction allows production to use NSec and tests to use a fake that
    ///     avoids NSec Key.Export (which can throw in some environments).
    /// </summary>
    public interface IEd25519KeyPairGenerator
    {
        /// <summary>
        ///     Generates a new Ed25519 keypair.
        /// </summary>
        /// <returns>Public key PEM and private key PEM (e.g. "PRIVATE KEY" or "RAW PRIVATE KEY").</returns>
        (string PublicKeyPem, string PrivateKeyPem) GenerateKeypair();
    }
}
