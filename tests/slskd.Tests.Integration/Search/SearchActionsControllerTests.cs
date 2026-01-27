// <copyright file="SearchActionsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.Search;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using slskd.Mesh;
using slskd.Search;
using slskd.Search.Providers;
using slskd.Streaming;
using Xunit;
using File = slskd.Search.File;

/// <summary>
/// Integration tests for SearchActionsController (Scene ↔ Pod Bridging action routing).
/// </summary>
public class SearchActionsControllerTests : IClassFixture<slskd.Tests.Integration.StubWebApplicationFactory>
{
    private readonly slskd.Tests.Integration.StubWebApplicationFactory factory;
    private readonly HttpClient client;

    public SearchActionsControllerTests(slskd.Tests.Integration.StubWebApplicationFactory factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task DownloadItem_InvalidItemId_ReturnsBadRequest()
    {
        // Arrange: Create a search first so we can test itemId parsing
        var searchId = Guid.NewGuid();
        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = "test-user",
                    Files = new List<slskd.Search.File>
                    {
                        new slskd.Search.File { Filename = "test.mp3", Size = 1024 }
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Act: Try to download with invalid item ID format
        var downloadResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/invalid/download",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, downloadResponse.StatusCode);
        
        var problemDetails = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(problemDetails);
        Assert.Equal("invalid_item_id", problemDetails.Type);
    }


    [Fact]
    public async Task DownloadItem_SearchNotFound_ReturnsNotFound()
    {
        // Arrange: Use a non-existent search ID
        var searchId = Guid.NewGuid();

        // Act
        var downloadResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/download",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
        
        var problemDetails = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(problemDetails);
        Assert.Equal("search_not_found", problemDetails.Type);
    }

    [Fact]
    public async Task DownloadItem_PodResult_RemoteDownload_Success()
    {
        // Arrange
        var searchId = Guid.NewGuid();
        var contentId = "content:mb:recording:test123";
        var peerId = "peer:test-peer-123";
        var filename = "test-file.mp3";
        var fileSize = 1024L;
        var testContent = new byte[fileSize];
        new Random().NextBytes(testContent);

        // Set up search service with a pod result
        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = peerId,
                    Files = new List<slskd.Search.File>
                    {
                        new slskd.Search.File
                        {
                            Filename = filename,
                            Size = fileSize
                        }
                    },
                    PrimarySource = "pod",
                    SourceProviders = new List<string> { "pod" },
                    PodContentRef = new PodContentRef
                    {
                        ContentId = contentId
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Set up mesh content fetcher with test content
        var meshContentFetcher = factory.Services.GetRequiredService<IMeshContentFetcher>() as StubMeshContentFetcher;
        meshContentFetcher?.SeedContent(peerId, contentId, testContent);

        // Act
        var downloadResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/download",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        
        var result = await downloadResponse.Content.ReadFromJsonAsync<DownloadResultResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("pod", result.Source);
        Assert.False(result.Local);
        Assert.NotNull(result.Path);
        
        // Verify file was written
        Assert.True(System.IO.File.Exists(result.Path));
        var downloadedContent = await System.IO.File.ReadAllBytesAsync(result.Path);
        Assert.Equal(testContent, downloadedContent);
        
        // Cleanup
        if (System.IO.File.Exists(result.Path))
        {
            System.IO.File.Delete(result.Path);
        }
    }

    [Fact]
    public async Task DownloadItem_PodResult_FallbackToMeshDirectory_Success()
    {
        // Arrange
        var searchId = Guid.NewGuid();
        var contentId = "content:mb:recording:test456";
        var peerId = "peer:test-peer-456";
        var filename = "test-file-2.mp3";
        var fileSize = 2048L;
        var testContent = new byte[fileSize];
        new Random().NextBytes(testContent);

        // Set up search service with a pod result (no peerId in username)
        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = "", // Empty username - should trigger mesh directory lookup
                    Files = new List<slskd.Search.File>
                    {
                        new slskd.Search.File
                        {
                            Filename = filename,
                            Size = fileSize
                        }
                    },
                    PrimarySource = "pod",
                    SourceProviders = new List<string> { "pod" },
                    PodContentRef = new PodContentRef
                    {
                        ContentId = contentId
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Set up mesh directory with peer for this content
        var meshDirectory = factory.Services.GetRequiredService<IMeshDirectory>() as StubMeshDirectory;
        meshDirectory?.SeedPeers(contentId, new MeshPeerDescriptor(peerId));

        // Set up mesh content fetcher with test content
        var meshContentFetcher = factory.Services.GetRequiredService<IMeshContentFetcher>() as StubMeshContentFetcher;
        meshContentFetcher?.SeedContent(peerId, contentId, testContent);

        // Act
        var downloadResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/download",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        
        var result = await downloadResponse.Content.ReadFromJsonAsync<DownloadResultResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("pod", result.Source);
        Assert.False(result.Local);
        
        // Cleanup
        if (result.Path != null && System.IO.File.Exists(result.Path))
        {
            System.IO.File.Delete(result.Path);
        }
    }

    [Fact]
    public async Task DownloadItem_PodResult_PeerNotFound_ReturnsNotFound()
    {
        // Arrange
        var searchId = Guid.NewGuid();
        var contentId = "content:mb:recording:test789";
        var filename = "test-file-3.mp3";
        var fileSize = 1024L;

        // Set up search service with a pod result (no peerId)
        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = "", // Empty username
                    Files = new List<slskd.Search.File>
                    {
                        new slskd.Search.File
                        {
                            Filename = filename,
                            Size = fileSize
                        }
                    },
                    PrimarySource = "pod",
                    SourceProviders = new List<string> { "pod" },
                    PodContentRef = new PodContentRef
                    {
                        ContentId = contentId
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Don't seed any peers in mesh directory - should return NotFound

        // Act
        var downloadResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/download",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
        
        var problemDetails = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(problemDetails);
        Assert.Equal("pod_peer_not_found", problemDetails.Type);
    }

    [Fact]
    public async Task DownloadItem_PodResult_FetchFailed_ReturnsBadGateway()
    {
        // Arrange
        var searchId = Guid.NewGuid();
        var contentId = "content:mb:recording:test999";
        var peerId = "peer:test-peer-999";
        var filename = "test-file-4.mp3";
        var fileSize = 1024L;

        // Set up search service with a pod result
        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = peerId,
                    Files = new List<slskd.Search.File>
                    {
                        new slskd.Search.File
                        {
                            Filename = filename,
                            Size = fileSize
                        }
                    },
                    PrimarySource = "pod",
                    SourceProviders = new List<string> { "pod" },
                    PodContentRef = new PodContentRef
                    {
                        ContentId = contentId
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Don't seed content in mesh content fetcher - should return fetch error

        // Act
        var downloadResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/download",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, downloadResponse.StatusCode);
        
        var problemDetails = await downloadResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(problemDetails);
        Assert.Equal("pod_fetch_failed", problemDetails.Type);
    }

    [Fact]
    public async Task StreamItem_PodResult_ReturnsStreamUrl()
    {
        // Arrange
        var searchId = Guid.NewGuid();
        var contentId = "content:mb:recording:stream-test";

        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = "peer:stream-peer",
                    Files = new List<File>
                    {
                        new File
                        {
                            Filename = "stream-test.mp3",
                            Size = 1024
                        }
                    },
                    PrimarySource = "pod",
                    SourceProviders = new List<string> { "pod" },
                    PodContentRef = new PodContentRef
                    {
                        ContentId = contentId
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Act
        var streamResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/stream",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
        
        var result = await streamResponse.Content.ReadFromJsonAsync<StreamResultResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.stream_url);
        Assert.Equal(contentId, result.content_id);
        Assert.Equal("pod", result.source);
        Assert.Contains("/api/v0/streams/", result.stream_url);
    }

    [Fact]
    public async Task StreamItem_SceneResult_ReturnsBadRequest()
    {
        // Arrange
        var searchId = Guid.NewGuid();

        var searchService = factory.Services.GetRequiredService<ISearchService>() as StubSearchService;
        var search = new Search
        {
            Id = searchId,
            SearchText = "test",
            State = Soulseek.SearchStates.Completed,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            Token = 0,
            Responses = new List<Response>
            {
                new Response
                {
                    Username = "scene-user",
                    Files = new List<File>
                    {
                        new File
                        {
                            Filename = "scene-file.mp3",
                            Size = 1024
                        }
                    },
                    PrimarySource = "scene",
                    SourceProviders = new List<string> { "scene" },
                    SceneContentRef = new SceneContentRef
                    {
                        Username = "scene-user",
                        Filename = "scene-file.mp3"
                    }
                }
            }
        };
        searchService?.SeedSearch(search);

        // Act
        var streamResponse = await client.PostAsync(
            $"/api/v0/searches/{searchId}/items/0:0/stream",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, streamResponse.StatusCode);
        
        var problemDetails = await streamResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.NotNull(problemDetails);
        Assert.Equal("scene_streaming_not_supported", problemDetails.Type);
    }
}

// Response models for tests
public record ProblemDetailsResponse(
    string Type,
    string Title,
    string Detail,
    int? Status = null
);

public record DownloadResultResponse(
    bool Success,
    string ContentId,
    string Source,
    bool Local,
    string Path,
    string Message
);

public record StreamResultResponse(
    string stream_url,
    string content_id,
    string source
);
