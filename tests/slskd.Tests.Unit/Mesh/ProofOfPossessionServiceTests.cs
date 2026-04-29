// <copyright file="ProofOfPossessionServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Mesh;
    using Xunit;

    public class ProofOfPossessionServiceTests
    {
        [Fact]
        public async Task VerifyAsync_ReturnsFalse_WhenChunkSenderReturnsNull()
        {
            var mockSender = new Mock<IChunkRequestSender>();
            mockSender.Setup(s => s.RequestChunkAsync(It.IsAny<string>(), It.IsAny<string>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((null, false));

            var svc = new ProofOfPossessionService();
            var result = await svc.VerifyAsync("peer", "key", "abc", 100, mockSender.Object);

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyAsync_ReturnsFalse_WhenChunkHashDoesNotMatch()
        {
            var data = Encoding.UTF8.GetBytes("hello");
            var b64 = Convert.ToBase64String(data);
            var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

            var mockSender = new Mock<IChunkRequestSender>();
            mockSender.Setup(s => s.RequestChunkAsync(It.IsAny<string>(), It.IsAny<string>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((b64, true));

            var svc = new ProofOfPossessionService();
            var result = await svc.VerifyAsync("peer", "key", wrongHash, 5, mockSender.Object);

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyAsync_ReturnsTrue_WhenChunkHashMatches()
        {
            var data = Encoding.UTF8.GetBytes("hello");
            var b64 = Convert.ToBase64String(data);
            byte[] hash;
            using (var sha = SHA256.Create())
                hash = sha.ComputeHash(data);
            var expectedHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            var mockSender = new Mock<IChunkRequestSender>();
            mockSender.Setup(s => s.RequestChunkAsync(It.IsAny<string>(), It.IsAny<string>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((b64, true));

            var svc = new ProofOfPossessionService();
            var result = await svc.VerifyAsync("peer", "key", expectedHash, 5, mockSender.Object);

            Assert.True(result);
        }
    }
}
