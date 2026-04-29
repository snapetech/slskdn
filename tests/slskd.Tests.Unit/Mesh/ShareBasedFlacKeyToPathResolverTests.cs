// <copyright file="ShareBasedFlacKeyToPathResolverTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.HashDb.Models;
    using slskd.Mesh;
    using slskd.Shares;
    using Xunit;

    public class ShareBasedFlacKeyToPathResolverTests
    {
        [Fact]
        public async Task TryGetFilePathAsync_ReturnsPath_WhenFlacKeyMatchesSharedFile()
        {
            var path = "/share/Music/album.flac";
            var size = 1024L;
            var flacKey = HashDbEntry.GenerateFlacKey(path, size);

            var mockRepo = new Mock<IShareRepository>();
            mockRepo.Setup(r => r.ListLocalPathsAndSizes(null)).Returns(new List<(string, long)> { (path, size) });

            var mockFactory = new Mock<IShareRepositoryFactory>();
            mockFactory.Setup(f => f.CreateFromHost(It.IsAny<string>())).Returns(mockRepo.Object);

            var resolver = new ShareBasedFlacKeyToPathResolver(mockFactory.Object, "local");

            var result = await resolver.TryGetFilePathAsync(flacKey);

            Assert.Equal(path, result);
        }

        [Fact]
        public async Task TryGetFilePathAsync_ReturnsNull_WhenFlacKeyDoesNotMatch()
        {
            var mockRepo = new Mock<IShareRepository>();
            mockRepo.Setup(r => r.ListLocalPathsAndSizes(null)).Returns(new List<(string, long)> { ("/other/file.flac", 999L) });

            var mockFactory = new Mock<IShareRepositoryFactory>();
            mockFactory.Setup(f => f.CreateFromHost(It.IsAny<string>())).Returns(mockRepo.Object);

            var resolver = new ShareBasedFlacKeyToPathResolver(mockFactory.Object, "local");

            var result = await resolver.TryGetFilePathAsync("0123456789abcdef");

            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetFilePathAsync_ReturnsNull_WhenSharesEmpty()
        {
            var mockRepo = new Mock<IShareRepository>();
            mockRepo.Setup(r => r.ListLocalPathsAndSizes(null)).Returns(new List<(string, long)>());

            var mockFactory = new Mock<IShareRepositoryFactory>();
            mockFactory.Setup(f => f.CreateFromHost(It.IsAny<string>())).Returns(mockRepo.Object);

            var resolver = new ShareBasedFlacKeyToPathResolver(mockFactory.Object, "local");

            var result = await resolver.TryGetFilePathAsync("0123456789abcdef");

            Assert.Null(result);
        }
    }
}
