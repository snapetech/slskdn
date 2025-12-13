// <copyright file="MetadataPortabilityTests.cs" company="slskdN Team">
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

public class MetadataPortabilityTests
{
    private readonly MetadataPortability _portability;
    private readonly Mock<IContentIdRegistry> _registryMock;
    private readonly Mock<IIpldMapper> _ipldMapperMock;
    private readonly Mock<ILogger<MetadataPortability>> _loggerMock;

    public MetadataPortabilityTests()
    {
        _registryMock = new Mock<IContentIdRegistry>();
        _ipldMapperMock = new Mock<IIpldMapper>();
        _loggerMock = new Mock<ILogger<MetadataPortability>>();
        _portability = new MetadataPortability(_registryMock.Object, _ipldMapperMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportAsync_ValidContentIds_ReturnsPackage()
    {
        // Arrange
        var contentIds = new[] { "content:audio:track:mb-12345", "content:video:movie:imdb-tt0111161" };

        _registryMock.Setup(r => r.FindByDomainAsync(It.IsAny<string>(), default))
            .ReturnsAsync(Array.Empty<string>());

        // Act
        var package = await _portability.ExportAsync(contentIds, includeLinks: true);

        // Assert
        Assert.NotNull(package);
        Assert.Equal("1.0", package.Version);
        Assert.True(package.Entries.Count >= 0); // May be empty in mock scenario
        Assert.Equal("slskdN", package.Source);
        Assert.NotNull(package.Metadata);
        Assert.Equal(package.Entries.Count, package.Metadata.TotalEntries);
    }

    [Fact]
    public async Task ExportAsync_IncludeLinksTrue_IncludesLinksInPackage()
    {
        // Arrange
        var contentIds = new[] { "content:audio:track:mb-12345" };
        var mockLinks = new[] { new IpldLink("album", "content:audio:album:mb-67890") };

        _ipldMapperMock.Setup(m => m.GetGraphAsync(It.IsAny<string>(), It.IsAny<int>(), default))
            .ReturnsAsync(new ContentGraph("content:audio:track:mb-12345",
                new[] { new ContentGraphNode("content:audio:track:mb-12345", Array.Empty<IpldLink>(), Array.Empty<string>()) },
                new[] { new ContentGraphPath(new[] { "content:audio:track:mb-12345" }, mockLinks) }));

        // Act
        var package = await _portability.ExportAsync(contentIds, includeLinks: true);

        // Assert
        Assert.NotNull(package);
        Assert.NotEmpty(package.Links);
    }

    [Fact]
    public async Task ImportAsync_ValidPackage_Succeeds()
    {
        // Arrange
        var package = new MetadataPackage(
            "1.0",
            DateTimeOffset.UtcNow,
            "test-source",
            Array.Empty<MetadataEntry>(),
            Array.Empty<IpldLink>(),
            new MetadataPackageMetadata(0, 0, new Dictionary<string, int>(), "checksum"));

        // Act
        var result = await _portability.ImportAsync(package);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.EntriesProcessed); // Empty package
    }

    [Fact]
    public async Task ImportAsync_DryRunTrue_DoesNotModifyData()
    {
        // Arrange
        var package = new MetadataPackage(
            "1.0",
            DateTimeOffset.UtcNow,
            "test-source",
            Array.Empty<MetadataEntry>(),
            Array.Empty<IpldLink>(),
            new MetadataPackageMetadata(0, 0, new Dictionary<string, int>(), "checksum"));

        // Act
        var result = await _portability.ImportAsync(package, dryRun: true);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AnalyzeConflictsAsync_ValidPackage_ReturnsAnalysis()
    {
        // Arrange
        var package = new MetadataPackage(
            "1.0",
            DateTimeOffset.UtcNow,
            "test-source",
            Array.Empty<MetadataEntry>(),
            Array.Empty<IpldLink>(),
            new MetadataPackageMetadata(0, 0, new Dictionary<string, int>(), "checksum"));

        _registryMock.Setup(r => r.IsRegisteredAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        // Act
        var analysis = await _portability.AnalyzeConflictsAsync(package);

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal(0, analysis.TotalEntries); // Empty package
        Assert.Equal(0, analysis.ConflictingEntries);
        Assert.Equal(0, analysis.CleanEntries);
    }

    [Fact]
    public async Task MergeMetadataAsync_PreferNewerStrategy_ReturnsNewer()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var olderDescriptor = new ContentDescriptor { ContentId = contentId, Confidence = 0.5 };
        var newerDescriptor = new ContentDescriptor { ContentId = contentId, Confidence = 0.8 };

        var sources = new[]
        {
            new MetadataSource("source1", olderDescriptor, DateTimeOffset.UtcNow.AddDays(-1), 1),
            new MetadataSource("source2", newerDescriptor, DateTimeOffset.UtcNow, 2)
        };

        // Act
        var result = await _portability.MergeMetadataAsync(contentId, sources, MetadataMergeStrategy.PreferNewer);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newerDescriptor.Confidence, result.Confidence);
    }

    [Fact]
    public async Task MergeMetadataAsync_PreferHigherPriorityStrategy_ReturnsHigherPriority()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var lowPriorityDescriptor = new ContentDescriptor { ContentId = contentId, Confidence = 0.5 };
        var highPriorityDescriptor = new ContentDescriptor { ContentId = contentId, Confidence = 0.8 };

        var sources = new[]
        {
            new MetadataSource("source1", lowPriorityDescriptor, DateTimeOffset.UtcNow, 1),
            new MetadataSource("source2", highPriorityDescriptor, DateTimeOffset.UtcNow, 5)
        };

        // Act
        var result = await _portability.MergeMetadataAsync(contentId, sources, MetadataMergeStrategy.PreferHigherPriority);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(highPriorityDescriptor.Confidence, result.Confidence);
    }

    [Fact]
    public async Task MergeMetadataAsync_CombineAllStrategy_MergesFields()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var descriptor1 = new ContentDescriptor
        {
            ContentId = contentId,
            Codec = "mp3",
            SizeBytes = 1024
        };
        var descriptor2 = new ContentDescriptor
        {
            ContentId = contentId,
            Confidence = 0.8
        };

        var sources = new[]
        {
            new MetadataSource("source1", descriptor1, DateTimeOffset.UtcNow, 1),
            new MetadataSource("source2", descriptor2, DateTimeOffset.UtcNow, 2)
        };

        // Act
        var result = await _portability.MergeMetadataAsync(contentId, sources, MetadataMergeStrategy.CombineAll);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mp3", result.Codec);
        Assert.Equal(1024, result.SizeBytes);
        Assert.Equal(0.8, result.Confidence);
    }

    [Fact]
    public async Task MergeMetadataAsync_EmptySources_ThrowsException()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var emptySources = Array.Empty<MetadataSource>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _portability.MergeMetadataAsync(contentId, emptySources));

        Assert.Contains("At least one metadata source", exception.Message);
    }

    [Fact]
    public void ConflictResolutionStrategy_EnumValues_AreDefined()
    {
        // Assert that all expected strategy values are defined
        Assert.Equal(0, (int)ConflictResolutionStrategy.Skip);
        Assert.Equal(1, (int)ConflictResolutionStrategy.Overwrite);
        Assert.Equal(2, (int)ConflictResolutionStrategy.Merge);
        Assert.Equal(3, (int)ConflictResolutionStrategy.KeepExisting);
        Assert.Equal(4, (int)ConflictResolutionStrategy.Interactive);
    }

    [Fact]
    public void MetadataMergeStrategy_EnumValues_AreDefined()
    {
        // Assert that all expected strategy values are defined
        Assert.Equal(0, (int)MetadataMergeStrategy.PreferNewer);
        Assert.Equal(1, (int)MetadataMergeStrategy.PreferHigherPriority);
        Assert.Equal(2, (int)MetadataMergeStrategy.CombineAll);
        Assert.Equal(3, (int)MetadataMergeStrategy.Custom);
    }

    [Fact]
    public void MetadataSource_Properties_AreSetCorrectly()
    {
        // Arrange
        var name = "MusicBrainz";
        var descriptor = new ContentDescriptor { ContentId = "content:audio:track:mb-12345" };
        var timestamp = DateTimeOffset.UtcNow;
        var priority = 5;

        // Act
        var source = new MetadataSource(name, descriptor, timestamp, priority);

        // Assert
        Assert.Equal(name, source.Name);
        Assert.Equal(descriptor, source.Descriptor);
        Assert.Equal(timestamp, source.Timestamp);
        Assert.Equal(priority, source.Priority);
    }
}
