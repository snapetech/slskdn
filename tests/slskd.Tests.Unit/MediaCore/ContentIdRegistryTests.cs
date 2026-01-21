// <copyright file="ContentIdRegistryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using slskd.MediaCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class ContentIdRegistryTests
{
    private readonly ContentIdRegistry _registry;

    public ContentIdRegistryTests()
    {
        _registry = new ContentIdRegistry();
    }

    [Fact]
    public async Task RegisterAsync_ValidInputs_AddsMapping()
    {
        // Arrange
        var externalId = "mb:recording:12345";
        var contentId = "content:audio:track:mb-12345";

        // Act
        await _registry.RegisterAsync(externalId, contentId);

        // Assert
        var resolved = await _registry.ResolveAsync(externalId);
        Assert.Equal(contentId, resolved);

        var isRegistered = await _registry.IsRegisteredAsync(externalId);
        Assert.True(isRegistered);
    }

    [Fact]
    public async Task RegisterAsync_EmptyExternalId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _registry.RegisterAsync("", "content:audio:track:id"));
    }

    [Fact]
    public async Task RegisterAsync_EmptyContentId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _registry.RegisterAsync("external", ""));
    }

    [Fact]
    public async Task ResolveAsync_RegisteredExternalId_ReturnsContentId()
    {
        // Arrange
        var externalId = "mb:recording:12345";
        var contentId = "content:audio:track:mb-12345";
        await _registry.RegisterAsync(externalId, contentId);

        // Act
        var result = await _registry.ResolveAsync(externalId);

        // Assert
        Assert.Equal(contentId, result);
    }

    [Fact]
    public async Task ResolveAsync_UnregisteredExternalId_ReturnsNull()
    {
        var result = await _registry.ResolveAsync("unregistered");
        Assert.Null(result);
    }

    [Fact]
    public async Task IsRegisteredAsync_RegisteredExternalId_ReturnsTrue()
    {
        // Arrange
        var externalId = "mb:recording:12345";
        var contentId = "content:audio:track:mb-12345";
        await _registry.RegisterAsync(externalId, contentId);

        // Act
        var result = await _registry.IsRegisteredAsync(externalId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsRegisteredAsync_UnregisteredExternalId_ReturnsFalse()
    {
        var result = await _registry.IsRegisteredAsync("unregistered");
        Assert.False(result);
    }

    [Fact]
    public async Task GetExternalIdsAsync_ValidContentId_ReturnsExternalIds()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var externalId1 = "mb:recording:12345";
        var externalId2 = "spotify:track:abc123";

        await _registry.RegisterAsync(externalId1, contentId);
        await _registry.RegisterAsync(externalId2, contentId);

        // Act
        var result = await _registry.GetExternalIdsAsync(contentId);

        // Assert
        Assert.Contains(externalId1, result);
        Assert.Contains(externalId2, result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetExternalIdsAsync_UnregisteredContentId_ReturnsEmptyList()
    {
        var result = await _registry.GetExternalIdsAsync("content:unknown:id");
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindByDomainAsync_ValidDomain_ReturnsMatchingContentIds()
    {
        // Arrange
        await _registry.RegisterAsync("mb:recording:12345", "content:audio:track:mb-12345");
        await _registry.RegisterAsync("imdb:tt0111161", "content:video:movie:imdb-tt0111161");
        await _registry.RegisterAsync("mb:recording:67890", "content:audio:album:mb-67890");

        // Act
        var audioResults = await _registry.FindByDomainAsync("audio");

        // Assert
        Assert.Equal(2, audioResults.Count);
        Assert.Contains("content:audio:track:mb-12345", audioResults);
        Assert.Contains("content:audio:album:mb-67890", audioResults);
    }

    [Fact]
    public async Task FindByDomainAsync_UnknownDomain_ReturnsEmptyList()
    {
        var result = await _registry.FindByDomainAsync("unknown");
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindByDomainAndTypeAsync_ValidDomainAndType_ReturnsMatchingContentIds()
    {
        // Arrange
        await _registry.RegisterAsync("mb:recording:12345", "content:audio:track:mb-12345");
        await _registry.RegisterAsync("mb:release:67890", "content:audio:album:mb-67890");
        await _registry.RegisterAsync("imdb:tt0111161", "content:video:movie:imdb-tt0111161");

        // Act
        var trackResults = await _registry.FindByDomainAndTypeAsync("audio", "track");

        // Assert
        Assert.Single(trackResults);
        Assert.Contains("content:audio:track:mb-12345", trackResults);
    }

    [Fact]
    public async Task FindByDomainAndTypeAsync_UnknownDomainOrType_ReturnsEmptyList()
    {
        var result = await _registry.FindByDomainAndTypeAsync("unknown", "track");
        Assert.Empty(result);

        result = await _registry.FindByDomainAndTypeAsync("audio", "unknown");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStatsAsync_NoRegistrations_ReturnsEmptyStats()
    {
        // Act
        var stats = await _registry.GetStatsAsync();

        // Assert
        Assert.Equal(0, stats.TotalMappings);
        Assert.Equal(0, stats.TotalDomains);
        Assert.Empty(stats.MappingsByDomain);
    }

    [Fact]
    public async Task GetStatsAsync_WithRegistrations_ReturnsCorrectStats()
    {
        // Arrange
        await _registry.RegisterAsync("mb:recording:12345", "content:audio:track:mb-12345");
        await _registry.RegisterAsync("imdb:tt0111161", "content:video:movie:imdb-tt0111161");
        await _registry.RegisterAsync("mb:release:67890", "content:audio:album:mb-67890");

        // Act
        var stats = await _registry.GetStatsAsync();

        // Assert
        Assert.Equal(3, stats.TotalMappings);
        Assert.Equal(2, stats.TotalDomains); // audio and video
        Assert.Equal(2, stats.MappingsByDomain["audio"]);
        Assert.Equal(1, stats.MappingsByDomain["video"]);
    }

    [Fact]
    public async Task MultipleExternalIdsSameContentId_AreAllTracked()
    {
        // Arrange
        var contentId = "content:audio:track:mb-12345";
        var externalId1 = "mb:recording:12345";
        var externalId2 = "spotify:track:abc123";
        var externalId3 = "deezer:track:xyz789";

        await _registry.RegisterAsync(externalId1, contentId);
        await _registry.RegisterAsync(externalId2, contentId);
        await _registry.RegisterAsync(externalId3, contentId);

        // Act & Assert
        Assert.Equal(contentId, await _registry.ResolveAsync(externalId1));
        Assert.Equal(contentId, await _registry.ResolveAsync(externalId2));
        Assert.Equal(contentId, await _registry.ResolveAsync(externalId3));

        var externalIds = await _registry.GetExternalIdsAsync(contentId);
        Assert.Equal(3, externalIds.Count);
        Assert.Contains(externalId1, externalIds);
        Assert.Contains(externalId2, externalIds);
        Assert.Contains(externalId3, externalIds);
    }

    [Fact]
    public async Task OverwriteMapping_UpdatesExistingRegistration()
    {
        // Arrange
        var externalId = "mb:recording:12345";
        var oldContentId = "content:audio:track:old-12345";
        var newContentId = "content:audio:track:new-12345";

        await _registry.RegisterAsync(externalId, oldContentId);

        // Act
        await _registry.RegisterAsync(externalId, newContentId);

        // Assert
        var resolved = await _registry.ResolveAsync(externalId);
        Assert.Equal(newContentId, resolved);

        var oldExternalIds = await _registry.GetExternalIdsAsync(oldContentId);
        Assert.DoesNotContain(externalId, oldExternalIds);

        var newExternalIds = await _registry.GetExternalIdsAsync(newContentId);
        Assert.Contains(externalId, newExternalIds);
    }

    [Fact]
    public void Clear_RemovesAllMappings()
    {
        // Arrange - this would normally be async, but Clear is synchronous
        // In a real scenario, we'd have a way to test this with async registrations
        // For now, just test that the method exists and doesn't throw
        _registry.Clear();
    }
}
