// <copyright file="DescriptorRetrieverTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.MediaCore;
using slskd.Mesh.Dht;
using Xunit;

public class DescriptorRetrieverTests
{
    [Fact]
    public async Task QueryByDomainAsync_WithMbDomainAndNoType_DoesNotThrow()
    {
        var retriever = CreateRetriever();

        var result = await retriever.QueryByDomainAsync("mb");

        Assert.NotNull(result);
        Assert.Empty(result.Descriptors);
        Assert.Equal("audio", result.Domain);
        Assert.Null(result.Type);
    }

    [Fact]
    public async Task QueryByDomainAsync_WithWhitespaceType_TreatsTypeAsMissing()
    {
        var retriever = CreateRetriever();

        var result = await retriever.QueryByDomainAsync("audio", "   ");

        Assert.NotNull(result);
        Assert.Empty(result.Descriptors);
        Assert.Equal("audio", result.Domain);
        Assert.Null(result.Type);
    }

    [Fact]
    public async Task RetrieveBatchAsync_TrimsAndDeduplicatesContentIds()
    {
        var dht = new Mock<IMeshDhtClient>();
        var retriever = CreateRetriever(dht.Object);

        await retriever.RetrieveBatchAsync(new[] { " content:mb:recording:1 ", "content:mb:recording:1", "", "   " });

        dht.Verify(client => client.GetAsync<ContentDescriptor>("mesh:content:content:mb:recording:1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_TrimsContentIdBeforeLookup()
    {
        var dht = new Mock<IMeshDhtClient>();
        var retriever = CreateRetriever(dht.Object);

        await retriever.RetrieveAsync(" content:mb:recording:2 ");

        dht.Verify(client => client.GetAsync<ContentDescriptor>("mesh:content:content:mb:recording:2", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DescriptorRetriever CreateRetriever(IMeshDhtClient? dht = null)
    {
        return new DescriptorRetriever(
            NullLogger<DescriptorRetriever>.Instance,
            dht ?? Mock.Of<IMeshDhtClient>(),
            Mock.Of<IDescriptorValidator>(),
            Options.Create(new MediaCoreOptions()));
    }
}
