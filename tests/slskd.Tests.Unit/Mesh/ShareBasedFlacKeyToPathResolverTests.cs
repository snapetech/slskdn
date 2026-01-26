// <copyright file="ShareBasedFlacKeyToPathResolverTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
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
