// <copyright file="ProofOfPossessionServiceTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using slskd.Mesh;
    using slskd.Mesh.Messages;
    using Xunit;

    public class ProofOfPossessionServiceTests
    {
        [Fact]
        public async Task VerifyAsync_ReturnsTrue_WhenChallengeMatchesHash()
        {
            var service = new ProofOfPossessionService(NullLogger<ProofOfPossessionService>.Instance);
            var entry = CreateEntry("peer-a", 4096, out var chunkBytes, out var byteHash);

            var result = await service.VerifyAsync(
                "peer-a",
                entry,
                challenge => Task.FromResult<MeshChallengeResponseMessage?>(new MeshChallengeResponseMessage
                {
                    ChallengeId = challenge.ChallengeId,
                    FlacKey = challenge.FlacKey,
                    Success = true,
                    Data = chunkBytes,
                }));

            Assert.True(result);
        }

        [Fact]
        public async Task VerifyAsync_ReturnsFalse_OnHashMismatch()
        {
            var service = new ProofOfPossessionService(NullLogger<ProofOfPossessionService>.Instance);
            var entry = CreateEntry("peer-b", 4096, out var chunkBytes, out var byteHash);

            // Corrupt chunk to trigger mismatch
            chunkBytes[0] ^= 0xFF;

            var result = await service.VerifyAsync(
                "peer-b",
                entry,
                challenge => Task.FromResult<MeshChallengeResponseMessage?>(new MeshChallengeResponseMessage
                {
                    ChallengeId = challenge.ChallengeId,
                    FlacKey = challenge.FlacKey,
                    Success = true,
                    Data = chunkBytes,
                }));

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyAsync_CachesSuccess()
        {
            var service = new ProofOfPossessionService(NullLogger<ProofOfPossessionService>.Instance);
            var entry = CreateEntry("peer-c", 4096, out var chunkBytes, out var byteHash);
            var callCount = 0;

            Func<MeshChallengeRequestMessage, Task<MeshChallengeResponseMessage?>> handler = challenge =>
            {
                callCount++;
                return Task.FromResult<MeshChallengeResponseMessage?>(new MeshChallengeResponseMessage
                {
                    ChallengeId = challenge.ChallengeId,
                    FlacKey = challenge.FlacKey,
                    Success = true,
                    Data = chunkBytes,
                });
            };

            var first = await service.VerifyAsync("peer-c", entry, handler);
            var second = await service.VerifyAsync("peer-c", entry, handler);

            Assert.True(first);
            Assert.True(second);
            Assert.Equal(1, callCount); // cached on second call
        }

        private static slskd.Mesh.Messages.MeshHashEntry CreateEntry(string peer, int length, out byte[] chunk, out string hashHex)
        {
            chunk = Encoding.UTF8.GetBytes($"test-chunk-{peer}-{Guid.NewGuid()}".PadRight(length, 'x')).AsSpan(0, length).ToArray();
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(chunk);
            hashHex = Convert.ToHexString(hash).ToLowerInvariant();

            return new MeshHashEntry
            {
                FlacKey = "0123456789abcdef",
                ByteHash = hashHex,
                Size = length,
                SeqId = 1,
            };
        }
    }
}
