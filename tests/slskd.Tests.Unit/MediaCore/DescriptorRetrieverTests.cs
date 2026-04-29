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

    [Fact]
    public async Task RetrieveAsync_WhenDhtLookupThrows_ReturnsSanitizedErrorMessage()
    {
        var dht = new Mock<IMeshDhtClient>();
        dht.Setup(client => client.GetAsync<ContentDescriptor>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive DHT detail"));

        var retriever = CreateRetriever(dht.Object);

        var result = await retriever.RetrieveAsync("content:mb:recording:3");

        Assert.False(result.Found);
        Assert.Equal("Failed to retrieve descriptor from DHT", result.ErrorMessage);
        Assert.DoesNotContain("sensitive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_WhenValidatorThrows_ReturnsSanitizedValidationError()
    {
        var validator = new Mock<IDescriptorValidator>();
        validator.Setup(v => v.Validate(It.IsAny<ContentDescriptor>(), out It.Ref<string?>.IsAny!))
            .Throws(new InvalidOperationException("sensitive validation detail"));

        var retriever = new DescriptorRetriever(
            NullLogger<DescriptorRetriever>.Instance,
            Mock.Of<IMeshDhtClient>(),
            validator.Object,
            Options.Create(new MediaCoreOptions()));

        var result = await retriever.VerifyAsync(new ContentDescriptor { ContentId = "content:a" }, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Equal("Descriptor verification failed", result.ValidationError);
        Assert.DoesNotContain("sensitive", result.ValidationError, StringComparison.OrdinalIgnoreCase);
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
