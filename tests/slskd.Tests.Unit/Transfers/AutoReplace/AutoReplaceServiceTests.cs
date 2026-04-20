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
using slskd.Transfers.Ranking;
using Soulseek;
using Xunit;
using SearchFile = slskd.Search.File;
using SearchModel = slskd.Search.Search;
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
                null))
            .ReturnsAsync((Guid id, SearchQuery _, SearchScope _, SearchOptions? _, List<string>? _) => new SearchModel
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

        var service = new AutoReplaceService(
            Mock.Of<ITransferService>(),
            searchService.Object,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IOptionsMonitor<SlskdOptions>>(),
            rankingService.Object,
            searchCompletionTimeout: TimeSpan.FromMilliseconds(50),
            searchPollInterval: TimeSpan.FromMilliseconds(1));

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
}
