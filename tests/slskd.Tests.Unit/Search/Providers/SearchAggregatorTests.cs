// <copyright file="SearchAggregatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Search.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using slskd.Search;
using slskd.Search.Providers;
using Xunit;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<slskd.Search.Providers.SearchAggregator>;

public class SearchAggregatorTests
{
    private readonly ILogger<SearchAggregator> _logger = NullLogger.Instance;

    private SearchAggregator CreateAggregator(string preferredPrimarySource = "pod")
    {
        var serilogLogger = Log.ForContext<SearchAggregator>();
        return new SearchAggregator(serilogLogger, preferredPrimarySource);
    }

    [Fact]
    public async Task AggregateAsync_MergesPodAndSceneResults_WithDeduplication()
    {
        // Arrange
        var aggregator = CreateAggregator();
        // Use same username for both to ensure deduplication works
        var providers = new List<ISearchProvider>
        {
            CreateMockProvider("pod", new List<SearchResult>
            {
                CreateSearchResult("pod", "test.flac", 1000, "pod", "same-user")
            }),
            CreateMockProvider("scene", new List<SearchResult>
            {
                CreateSearchResult("scene", "test.flac", 1000, "scene", "same-user")
            })
        };
        var request = new SearchRequest
        {
            SearchText = "test",
            TimeoutSeconds = 5,
            ResponseLimit = 100,
            FileLimit = 10000
        };

        // Act
        var results = await aggregator.AggregateAsync(providers, request, CancellationToken.None);

        // Assert
        Assert.Single(results);
        var result = results.First();
        Assert.Contains("pod", result.SourceProviders);
        Assert.Contains("scene", result.SourceProviders);
        Assert.Equal(2, result.SourceProviders.Count);
        Assert.Equal("pod", result.PrimarySource); // Preferred source
    }

    [Fact]
    public async Task AggregateAsync_KeepsSeparateResults_WhenNoDeduplicationMatch()
    {
        // Arrange
        var aggregator = CreateAggregator();
        var providers = new List<ISearchProvider>
        {
            CreateMockProvider("pod", new List<SearchResult>
            {
                CreateSearchResult("pod", "file1.flac", 1000, "pod")
            }),
            CreateMockProvider("scene", new List<SearchResult>
            {
                CreateSearchResult("scene", "file2.flac", 2000, "scene")
            })
        };
        var request = new SearchRequest
        {
            SearchText = "test",
            TimeoutSeconds = 5,
            ResponseLimit = 100,
            FileLimit = 10000
        };

        // Act
        var results = await aggregator.AggregateAsync(providers, request, CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Single(r.SourceProviders));
    }

    [Fact]
    public async Task AggregateAsync_PrefersPod_WhenBothAvailable()
    {
        // Arrange
        var aggregator = CreateAggregator("pod");
        // Use same username for both to ensure deduplication works
        var providers = new List<ISearchProvider>
        {
            CreateMockProvider("pod", new List<SearchResult>
            {
                CreateSearchResult("pod", "test.flac", 1000, "pod", "same-user")
            }),
            CreateMockProvider("scene", new List<SearchResult>
            {
                CreateSearchResult("scene", "test.flac", 1000, "scene", "same-user")
            })
        };
        var request = new SearchRequest
        {
            SearchText = "test",
            TimeoutSeconds = 5,
            ResponseLimit = 100,
            FileLimit = 10000
        };

        // Act
        var results = await aggregator.AggregateAsync(providers, request, CancellationToken.None);

        // Assert
        Assert.Single(results); // Should be merged
        var result = results.First();
        Assert.Equal("pod", result.PrimarySource);
    }

    [Fact]
    public async Task AggregateAsync_PrefersScene_WhenConfigured()
    {
        // Arrange
        var aggregator = CreateAggregator("scene");
        // Use same username for both to ensure deduplication works
        var providers = new List<ISearchProvider>
        {
            CreateMockProvider("pod", new List<SearchResult>
            {
                CreateSearchResult("pod", "test.flac", 1000, "pod", "same-user")
            }),
            CreateMockProvider("scene", new List<SearchResult>
            {
                CreateSearchResult("scene", "test.flac", 1000, "scene", "same-user")
            })
        };
        var request = new SearchRequest
        {
            SearchText = "test",
            TimeoutSeconds = 5,
            ResponseLimit = 100,
            FileLimit = 10000
        };

        // Act
        var results = await aggregator.AggregateAsync(providers, request, CancellationToken.None);

        // Assert
        Assert.Single(results); // Should be merged
        var result = results.First();
        Assert.Equal("scene", result.PrimarySource);
    }

    private ISearchProvider CreateMockProvider(string name, List<SearchResult> results)
    {
        var mock = new Mock<ISearchProvider>();
        mock.Setup(p => p.Name).Returns(name);
        mock.Setup(p => p.StartSearchAsync(
                It.IsAny<SearchRequest>(),
                It.IsAny<ISearchResultSink>(),
                It.IsAny<CancellationToken>()))
            .Returns<SearchRequest, ISearchResultSink, CancellationToken>((req, sink, ct) =>
            {
                foreach (var result in results)
                {
                    sink.AddResult(result);
                }
                return Task.CompletedTask;
            });
        return mock.Object;
    }

    private SearchResult CreateSearchResult(string provider, string filename, long size, string primarySource, string username = null)
    {
        var response = new Response
        {
            Username = username ?? (provider == "pod" ? "pod-peer" : "scene-user"),
            Files = new List<File>
            {
                new File { Filename = filename, Size = size }
            },
            FileCount = 1,
            SourceProviders = new List<string> { provider },
            PrimarySource = primarySource
        };

        PodContentRef? podRef = null;
        SceneContentRef? sceneRef = null;

        if (provider == "pod")
        {
            podRef = new PodContentRef
            {
                ContentId = $"content:{filename}",
                Hash = null
            };
            response.PodContentRef = podRef;
        }
        else
        {
            sceneRef = new SceneContentRef
            {
                Username = "scene-user",
                Filename = filename,
                Size = size
            };
            response.SceneContentRef = sceneRef;
        }

        return new SearchResult
        {
            Provider = provider,
            SourceProviders = new List<string> { provider },
            PrimarySource = primarySource,
            Response = response,
            PodContentRef = podRef,
            SceneContentRef = sceneRef
        };
    }
}
