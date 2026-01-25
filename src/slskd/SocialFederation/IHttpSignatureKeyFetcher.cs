// <copyright file="IHttpSignatureKeyFetcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Fetches a signer's public key from a keyId URL with SSRF protections. PR-14.
    /// </summary>
    public interface IHttpSignatureKeyFetcher
    {
        /// <summary>
        ///     Fetches the public key (PKIX bytes) for the given keyId. Returns null if unavailable or SSRF-unsafe.
        /// </summary>
        Task<byte[]?> FetchPublicKeyPkixAsync(string keyId, CancellationToken cancellationToken = default);
    }
}
