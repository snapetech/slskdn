// <copyright file="AutoReplaceServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.AutoReplace;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Search;
using slskd.Transfers;
using slskd.Transfers.AutoReplace;
using slskd.Transfers.Downloads;
using slskd.Transfers.Ranking;
using Soulseek;
using Xunit;
using SearchFile = slskd.Search.File;
using SearchModel = slskd.Search.Search;
using SlskdTransfer = slskd.Transfers.Transfer;
using SlskdOptions = slskd.Options;

public class AutoReplaceServiceTests
{
    [Fact]
    public async Task FindAlternativesAsync_WaitsForPersistedCompletedSearchResponses()
    {
        var searchService = new Mock<ISearchService>();
        searchService
            .Setup(service => service.StartAsync(
                It.IsAny<Guid>(),
                It.IsAny<SearchQuery>(),
                SearchScope.Network,
                It.IsAny<SearchOptions>(),
                It.IsAny<List<string>>()))
            .ReturnsAsync((Guid id, SearchQuery _, SearchScope _, SearchOptions _, List<string> _) => new SearchModel
            {
                Id = id,
                State = SearchStates.Requested,
            });

        searchService
            .SetupSequence(service => service.FindAsync(
                It.IsAny<Expression<Func<SearchModel, bool>>>(),
                true))
            .ReturnsAsync(new SearchModel { State = SearchStates.Requested })
            .ReturnsAsync(new SearchModel
            {
                State = SearchStates.Completed,
                Responses = new[]
                {
                    new Response
                    {
                        Username = "candidate",
                        HasFreeUploadSlot = true,
                        QueueLength = 2,
                        UploadSpeed = 1234,
                        Files = new[]
                        {
                            new SearchFile
                            {
                                Filename = "Artist - Track.flac",
                                Extension = "flac",
                                Size = 1000,
                            },
                        },
                    },
                },
            });

        var rankingService = new Mock<ISourceRankingService>();
        rankingService
            .Setup(service => service.RankSourcesAsync(
                It.IsAny<IEnumerable<SourceCandidate>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<SourceCandidate> candidates, CancellationToken _) => candidates.Select(candidate => new RankedSource
            {
                Username = candidate.Username,
                Filename = candidate.Filename,
                Size = candidate.Size,
                HasFreeUploadSlot = candidate.HasFreeUploadSlot,
                QueueLength = candidate.QueueLength,
                UploadSpeed = candidate.UploadSpeed,
                SizeDiffPercent = candidate.SizeDiffPercent,
                SmartScore = 100,
            }));

        using var service = new AutoReplaceService(
            Mock.Of<ITransferService>(),
            searchService.Object,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IOptionsMonitor<SlskdOptions>>(),
            rankingService.Object,
            searchCompletionTimeout: TimeSpan.FromMilliseconds(50),
            searchPollInterval: TimeSpan.FromMilliseconds(1),
            minimumSearchInterval: TimeSpan.Zero);

        var alternatives = await service.FindAlternativesAsync(new FindAlternativeRequest
        {
            Username = "original",
            Filename = "Artist - Track.flac",
            Size = 1000,
        });

        var alternative = Assert.Single(alternatives);
        Assert.Equal("candidate", alternative.Username);
        Assert.Equal("Artist - Track.flac", alternative.Filename);
        searchService.Verify(
            service => service.FindAsync(It.IsAny<Expression<Func<SearchModel, bool>>>(), true),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessStuckDownloadsAsync_WhenSearchBudgetExceeded_SkipsAndStopsCycle()
    {
        var downloads = new List<SlskdTransfer>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "source-one",
                Filename = "Artist - Track One.flac",
                Size = 1000,
                State = TransferStates.Completed | TransferStates.TimedOut,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Username = "source-two",
                Filename = "Artist - Track Two.flac",
                Size = 1000,
                State = TransferStates.Completed | TransferStates.TimedOut,
            },
        };

        var downloadService = new Mock<IDownloadService>();
        downloadService
            .Setup(service => service.List(It.IsAny<Expression<Func<SlskdTransfer, bool>>>(), false))
            .Returns((Expression<Func<SlskdTransfer, bool>> expression, bool _) => downloads.Where(expression.Compile()).ToList());

        var transferService = new Mock<ITransferService>();
        transferService
            .SetupGet(service => service.Downloads)
            .Returns(downloadService.Object);

        using var searchService = new RateLimitedSearchService();

        var rankingService = new Mock<ISourceRankingService>();
        rankingService
            .Setup(service => service.RecordFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var service = new AutoReplaceService(
            transferService.Object,
            searchService,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IOptionsMonitor<SlskdOptions>>(),
            rankingService.Object,
            searchCompletionTimeout: TimeSpan.FromMilliseconds(1),
            searchPollInterval: TimeSpan.FromMilliseconds(1),
            minimumSearchInterval: TimeSpan.Zero);

        var result = await service.ProcessStuckDownloadsAsync(new AutoReplaceRequest());

        Assert.Equal(0, result.Failed);
        Assert.Equal(1, result.Skipped);
        Assert.Contains("Search safety budget exhausted", Assert.Single(result.Details).Error);
        Assert.Equal(1, searchService.StartCount);
    }

    private sealed class RateLimitedSearchService : ISearchService
    {
        public int StartCount { get; private set; }

        public Task DeleteAsync(SearchModel search)
        {
            return Task.CompletedTask;
        }

        public Task<SearchModel> StartAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null, List<string> requestedProviders = null)
        {
            StartCount++;
            throw new InvalidOperationException("Search rate limit exceeded. See Soulseek safety configuration.");
        }

        public Task<SearchModel> FindAsync(Expression<Func<SearchModel, bool>> expression, bool includeResponses = false)
        {
            return Task.FromResult<SearchModel>(null);
        }

        public Task<List<SearchModel>> ListAsync(Expression<Func<SearchModel, bool>> expression = null, int limit = 0, int offset = 0)
        {
            return Task.FromResult(new List<SearchModel>());
        }

        public void Update(SearchModel search)
        {
        }

        public bool TryCancel(Guid id)
        {
            return false;
        }

        public Task<int> PruneAsync(int age)
        {
            return Task.FromResult(0);
        }

        public Task<int> DeleteAllAsync()
        {
            return Task.FromResult(0);
        }

        public void Dispose()
        {
        }
    }
}
