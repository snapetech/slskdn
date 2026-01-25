// <copyright file="FakeEd25519KeyPairGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SocialFederation
{
    using slskd.SocialFederation;

    /// <summary>
    ///     Test double that returns fixed Ed25519 keypairs (RFC 8032 test vectors).
    ///     Avoids NSec Key.Export, which can throw in some environments.
    ///     Each call to GenerateKeypair returns the next of two keypairs so RotateKeypair tests see a change.
    /// </summary>
    public sealed class FakeEd25519KeyPairGenerator : IEd25519KeyPairGenerator
    {
        private int _callCount;

        /// <inheritdoc/>
        public (string PublicKeyPem, string PrivateKeyPem) GenerateKeypair()
        {
            var n = System.Threading.Interlocked.Increment(ref _callCount);
            return (n % 2) == 1 ? (PublicKeyPem1, PrivateKeyPem1) : (PublicKeyPem2, PrivateKeyPem2);
        }

        /// <summary>RFC 8032 test vector 1, SPKI (Ed25519).</summary>
        public const string PublicKeyPem1 =
            "-----BEGIN PUBLIC KEY-----\n" +
            "MCowBQYDK2VwAyEAd1mpgBgrEat1S/7TyWQHOi7hc/PapkjJrwIaaPcHURo=\n" +
            "-----END PUBLIC KEY-----";

        /// <summary>RFC 8032 test vector 1, 32-byte raw private key.</summary>
        public const string PrivateKeyPem1 =
            "-----BEGIN RAW PRIVATE KEY-----\n" +
            "nWGxne/9WqC6hCmKMH36EcONbGqL9a5f/c4g+NpR+n0=\n" +
            "-----END RAW PRIVATE KEY-----";

        /// <summary>RFC 8032 test vector 2, SPKI (Ed25519).</summary>
        public const string PublicKeyPem2 =
            "-----BEGIN PUBLIC KEY-----\n" +
            "MCoMBQYDK2VwAyEAPUAXw+hDiVqStxcqdR1+7JyYLs8uyWjMDNVfEq9GZgw=\n" +
            "-----END PUBLIC KEY-----";

        /// <summary>RFC 8032 test vector 2, 32-byte raw private key.</summary>
        public const string PrivateKeyPem2 =
            "-----BEGIN RAW PRIVATE KEY-----\n" +
            "TM0ImSj/lbadtsNG7BFOD1uKMZ81q6Yk2oz27U+4pvs=\n" +
            "-----END RAW PRIVATE KEY-----";
    }
}
