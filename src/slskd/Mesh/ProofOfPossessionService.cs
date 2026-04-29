// <copyright file="ProofOfPossessionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh
{
    using System;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Proof-of-possession: requests first 32KB from peer, SHA256, compares to expected ByteHash (T-1434).
    /// </summary>
    public class ProofOfPossessionService : IProofOfPossessionService
    {
        private const int ChunkSize = 32768;

        /// <inheritdoc/>
        public async Task<bool> VerifyAsync(string peer, string flacKey, string expectedByteHash, long size, IChunkRequestSender chunkSender, CancellationToken cancellationToken = default)
        {
            if (chunkSender == null || string.IsNullOrEmpty(expectedByteHash))
                return false;

            var length = size < ChunkSize ? (int)size : ChunkSize;
            var (dataBase64, success) = await chunkSender.RequestChunkAsync(peer, flacKey, 0, length, cancellationToken).ConfigureAwait(false);
            if (!success || string.IsNullOrEmpty(dataBase64))
                return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(dataBase64);
            }
            catch
            {
                return false;
            }

            if (bytes.Length == 0)
                return false;

            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(bytes);
            }

            var computed = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            var expected = (expectedByteHash ?? string.Empty).ToLowerInvariant();
            return string.Equals(computed, expected, StringComparison.Ordinal);
        }
    }
}
