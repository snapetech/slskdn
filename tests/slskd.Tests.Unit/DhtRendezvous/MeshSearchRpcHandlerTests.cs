// <copyright file="MeshSearchRpcHandlerTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Search;
using slskd.Shares;
using Soulseek;
using Xunit;

/// <summary>
/// Unit tests for MeshSearchRpcHandler: max_results clamp, deterministic ordering.
/// </summary>
public class MeshSearchRpcHandlerTests
{
    private static MeshSearchRequestMessage Req(string searchText = "test", int maxResults = 50) => new()
    {
        RequestId = Guid.NewGuid().ToString("N"),
        SearchText = searchText,
        MaxResults = maxResults,
    };

    private static Soulseek.File Sf(string filename, long size = 1000, string ext = ".flac") =>
        new Soulseek.File(1, filename, size, ext);

    [Fact]
    public async Task HandleAsync_RespectsMaxResults_ClampsToRequested()
    {
        var files = new[]
        {
            Sf("c.flac"),
            Sf("a.flac"),
            Sf("b.flac"),
            Sf("d.flac"),
            Sf("e.flac"),
        };
        var share = new Mock<IShareService>();
        share.Setup(s => s.SearchLocalAsync(It.IsAny<Soulseek.SearchQuery>()))
            .ReturnsAsync(files);

        var log = new Mock<ILogger<MeshSearchRpcHandler>>();
        var handler = new MeshSearchRpcHandler(share.Object, log.Object);

        var resp = await handler.HandleAsync(Req("x", maxResults: 3), CancellationToken.None);

        Assert.NotNull(resp);
        Assert.Equal(3, resp.Files.Count);
        Assert.True(resp.Truncated, "Truncated should be true when more results exist");
    }

    [Fact]
    public async Task HandleAsync_DeterministicOrdering_ByFilename()
    {
        var files = new[] { Sf("z.flac"), Sf("a.flac"), Sf("m.flac") };
        var share = new Mock<IShareService>();
        share.Setup(s => s.SearchLocalAsync(It.IsAny<Soulseek.SearchQuery>()))
            .ReturnsAsync(files);

        var log = new Mock<ILogger<MeshSearchRpcHandler>>();
        var handler = new MeshSearchRpcHandler(share.Object, log.Object);

        var resp = await handler.HandleAsync(Req("x", maxResults: 10), CancellationToken.None);

        var names = resp.Files.Select(f => f.Filename).ToList();
        Assert.Equal("a.flac", names[0]);
        Assert.Equal("m.flac", names[1]);
        Assert.Equal("z.flac", names[2]);
    }

    [Fact]
    public async Task HandleAsync_WhenSearchThrows_ReturnsErrorResponse()
    {
        var share = new Mock<IShareService>();
        share.Setup(s => s.SearchLocalAsync(It.IsAny<Soulseek.SearchQuery>()))
            .ThrowsAsync(new InvalidOperationException("db fail"));

        var log = new Mock<ILogger<MeshSearchRpcHandler>>();
        var handler = new MeshSearchRpcHandler(share.Object, log.Object);
        var req = Req();

        var resp = await handler.HandleAsync(req, CancellationToken.None);

        Assert.NotNull(resp);
        Assert.Equal(req.RequestId, resp.RequestId);
        Assert.Empty(resp.Files);
        Assert.NotNull(resp.Error);
        Assert.Contains("fail", resp.Error, StringComparison.OrdinalIgnoreCase);
    }
}
