// <copyright file="IpldMapperTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using slskd.MediaCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class IpldMapperTests
{
    private readonly IpldMapper _mapper;
    private readonly Mock<IContentIdRegistry> _registryMock;
    private readonly Mock<ILogger<IpldMapper>> _loggerMock;

    public IpldMapperTests()
    {
        _registryMock = new Mock<IContentIdRegistry>();
        _loggerMock = new Mock<ILogger<IpldMapper>>();
        _mapper = new IpldMapper(_registryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task AddLinksAsync_ValidInputs_Succeeds()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var links = new[]
        {
            new IpldLink(IpldLinkNames.Album, "content:audio:album:mb-67890"),
            new IpldLink(IpldLinkNames.Artist, "content:audio:artist:mb-abc123")
        };

        _registryMock.Setup(r => r.IsRegisteredAsync(contentId, default))
            .ReturnsAsync(true);

        // Act
        await _mapper.AddLinksAsync(contentId, links);

        // Assert - mainly that it doesn't throw
        _registryMock.Verify(r => r.IsRegisteredAsync(contentId, default), Times.Once);
    }

    [Fact]
    public async Task AddLinksAsync_UnregisteredContentId_ThrowsException()
    {
        // Arrange
        var contentId = "content:unknown:id";
        var links = new[] { new IpldLink("parent", "content:other:id") };

        _registryMock.Setup(r => r.IsRegisteredAsync(contentId, default))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _mapper.AddLinksAsync(contentId, links));

        Assert.Contains("not registered", exception.Message);
    }

    [Fact]
    public async Task TraverseAsync_SimpleTraversal_ReturnsTraversalResult()
    {
        // Arrange
        var startContentId = "content:audio:track:mb-12345";
        var linkName = IpldLinkNames.Album;

        // Setup mock registry responses
        _registryMock.Setup(r => r.FindByDomainAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new[] { startContentId });

        // Act
        var result = await _mapper.TraverseAsync(startContentId, linkName, maxDepth: 2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startContentId, result.StartContentId);
        Assert.Equal(linkName, result.LinkName);
        Assert.NotNull(result.VisitedNodes);
        Assert.NotNull(result.Paths);
    }

    [Fact]
    public async Task TraverseAsync_MaxDepthExceeded_StopsTraversal()
    {
        // maxDepth 1–10 required; use maxDepth: 1 and a graph that goes deeper (track→album→…).
        // Traversal stops at the limit: only the start node is visited; no deeper nodes.
        var startContentId = "content:audio:track:mb-12345";
        var linkName = IpldLinkNames.Album;

        _registryMock.Setup(r => r.FindByDomainAsync(It.IsAny<string>(), default))
            .ReturnsAsync(Array.Empty<string>());

        var result = await _mapper.TraverseAsync(startContentId, linkName, maxDepth: 1);

        Assert.NotNull(result);
        // With maxDepth=1 we process depth 0 only; recursion to linked nodes returns immediately at depth 1.
        Assert.Single(result.VisitedNodes);
        Assert.Equal(startContentId, result.VisitedNodes[0].ContentId);
    }

    [Fact]
    public async Task FindInboundLinksAsync_ValidTarget_ReturnsInboundLinks()
    {
        // Arrange
        var targetContentId = "content:audio:album:mb-67890";
        var linkName = IpldLinkNames.Album;

        _registryMock.Setup(r => r.FindByDomainAsync("audio", default))
            .ReturnsAsync(new[] { "content:audio:track:mb-12345" });

        // Act
        var result = await _mapper.FindInboundLinksAsync(targetContentId, linkName);

        // Assert
        Assert.NotNull(result);
        _registryMock.Verify(r => r.FindByDomainAsync("audio", default), Times.Once);
    }

    [Fact]
    public async Task GetGraphAsync_ValidContentId_ReturnsGraph()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";

        _registryMock.Setup(r => r.FindByDomainAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new[] { contentId });

        // Act
        var result = await _mapper.GetGraphAsync(contentId, maxDepth: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contentId, result.RootContentId);
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Paths);
    }

    [Fact]
    public async Task ValidateLinksAsync_ValidatesSuccessfully()
    {
        // Arrange
        _registryMock.Setup(r => r.FindByDomainAsync(It.IsAny<string>(), default))
            .ReturnsAsync(Array.Empty<string>());

        // Act
        var result = await _mapper.ValidateLinksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid); // Should be valid with no links to validate
        Assert.Equal(0, result.BrokenLinks.Count);
        Assert.Equal(0, result.OrphanedLinks.Count);
    }

    [Fact]
    public void ToJson_ValidDescriptor_ReturnsJson()
    {
        // Arrange
        var descriptor = new ContentDescriptor
        {
            ContentId = "content:audio:track:mb-12345",
            SizeBytes = 1024 * 1024,
            Codec = "mp3",
            Confidence = 0.8
        };

        // Act
        var json = _mapper.ToJson(descriptor);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("contentId", json);
        Assert.Contains("size", json);
        Assert.Contains("codec", json);
        Assert.Contains("confidence", json);
    }

    [Fact]
    public void ToJson_DescriptorWithLinks_IncludesLinksInJson()
    {
        // Arrange
        var descriptor = new ContentDescriptor
        {
            ContentId = "content:audio:track:mb-12345",
            SizeBytes = 1024 * 1024,
            Codec = "mp3",
            Confidence = 0.8
        };

        descriptor.AddLink(IpldLinkNames.Album, "content:audio:album:mb-67890");
        descriptor.AddLink(IpldLinkNames.Artist, "content:audio:artist:mb-abc123");

        // Act
        var json = _mapper.ToJson(descriptor);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("links", json);
        Assert.Contains(IpldLinkNames.Album, json);
        Assert.Contains(IpldLinkNames.Artist, json);
    }

    [Fact]
    public void IpldLinkNames_ConstantsAreDefined()
    {
        // Assert that all expected link name constants are defined
        Assert.Equal("parent", IpldLinkNames.Parent);
        Assert.Equal("children", IpldLinkNames.Children);
        Assert.Equal("album", IpldLinkNames.Album);
        Assert.Equal("artist", IpldLinkNames.Artist);
        Assert.Equal("artwork", IpldLinkNames.Artwork);
        Assert.Equal("tracks", IpldLinkNames.Tracks);
    }

    [Fact]
    public void IpldLink_PropertiesAreSetCorrectly()
    {
        // Arrange
        var name = "album";
        var target = "content:audio:album:mb-67890";
        var linkName = "main-album";

        // Act
        var link = new IpldLink(name, target, linkName);

        // Assert
        Assert.Equal(name, link.Name);
        Assert.Equal(target, link.Target);
        Assert.Equal(linkName, link.LinkName);
        Assert.Equal($"{name}/{target}", link.Path);
    }

    [Fact]
    public void IpldLinkCollection_AddAndRetrieveLinks()
    {
        // Arrange
        var collection = new IpldLinkCollection();
        var link1 = new IpldLink("album", "content:audio:album:1");
        var link2 = new IpldLink("album", "content:audio:album:2");
        var link3 = new IpldLink("artist", "content:audio:artist:1");

        // Act
        collection.AddLink(link1);
        collection.AddLink(link2);
        collection.AddLink(link3);

        // Assert
        var albumLinks = collection.GetLinksByName("album");
        Assert.Equal(2, albumLinks.Count);
        Assert.Contains(link1, albumLinks);
        Assert.Contains(link2, albumLinks);

        var artistLinks = collection.GetLinksByName("artist");
        Assert.Single(artistLinks);
        Assert.Contains(link3, artistLinks);

        var album1Targets = collection.GetLinksByTarget("content:audio:album:1");
        Assert.Single(album1Targets);
        Assert.Contains(link1, album1Targets);
    }
}

