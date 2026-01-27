// <copyright file="AnalyticsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.API;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Transfers.MultiSource.API;
using slskd.Transfers.MultiSource.Analytics;
using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Unit tests for AnalyticsController.
/// </summary>
public class AnalyticsControllerTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ISwarmAnalyticsService> _analyticsServiceMock;
    private readonly AnalyticsController _controller;

    public AnalyticsControllerTests(ITestOutputHelper output)
    {
        _output = output;
        _analyticsServiceMock = new Mock<ISwarmAnalyticsService>();
        _controller = new AnalyticsController(_analyticsServiceMock.Object);

        // Set up controller context with authenticated user (required for [Authorize])
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }, "test"))
            }
        };
    }

    [Fact]
    public async Task GetPerformanceMetrics_Should_Return_Ok_With_Metrics()
    {
        // Arrange
        var expectedMetrics = new SwarmPerformanceMetrics
        {
            TotalDownloads = 100,
            SuccessfulDownloads = 95,
            FailedDownloads = 5,
            SuccessRate = 0.95,
            AverageDurationSeconds = 60.0,
            AverageSpeedBytesPerSecond = 1024 * 1024,
            TimeWindow = TimeSpan.FromHours(24),
        };

        _analyticsServiceMock
            .Setup(x => x.GetPerformanceMetricsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetPerformanceMetrics(null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var metrics = Assert.IsType<SwarmPerformanceMetrics>(okResult.Value);
        Assert.Equal(expectedMetrics.TotalDownloads, metrics.TotalDownloads);
        Assert.Equal(expectedMetrics.SuccessRate, metrics.SuccessRate);
    }

    [Fact]
    public async Task GetPerformanceMetrics_Should_Use_Custom_TimeWindow()
    {
        // Arrange
        var timeWindowHours = 6;
        var expectedTimeWindow = TimeSpan.FromHours(6);
        var expectedMetrics = new SwarmPerformanceMetrics
        {
            TimeWindow = expectedTimeWindow,
        };

        _analyticsServiceMock
            .Setup(x => x.GetPerformanceMetricsAsync(expectedTimeWindow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetPerformanceMetrics(timeWindowHours, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var metrics = Assert.IsType<SwarmPerformanceMetrics>(okResult.Value);
        Assert.Equal(expectedTimeWindow, metrics.TimeWindow);
    }

    [Fact]
    public async Task GetPeerRankings_Should_Return_Ok_With_Rankings()
    {
        // Arrange
        var limit = 10;
        var expectedRankings = new List<PeerPerformanceRanking>
        {
            new PeerPerformanceRanking
            {
                PeerId = "peer1",
                Rank = 1,
                ReputationScore = 0.9,
                AverageRttMs = 50,
            },
            new PeerPerformanceRanking
            {
                PeerId = "peer2",
                Rank = 2,
                ReputationScore = 0.8,
                AverageRttMs = 100,
            },
        };

        _analyticsServiceMock
            .Setup(x => x.GetPeerRankingsAsync(limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRankings);

        // Act
        var result = await _controller.GetPeerRankings(limit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var rankings = Assert.IsType<List<PeerPerformanceRanking>>(okResult.Value);
        Assert.Equal(expectedRankings.Count, rankings.Count);
        Assert.Equal(expectedRankings[0].PeerId, rankings[0].PeerId);
    }

    [Fact]
    public async Task GetPeerRankings_Should_Reject_Invalid_Limit_Below_Minimum()
    {
        // Arrange
        var invalidLimit = 0;

        // Act
        var result = await _controller.GetPeerRankings(invalidLimit, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Limit must be between 1 and 100", badRequestResult.Value?.ToString() ?? string.Empty);
        _analyticsServiceMock.Verify(x => x.GetPeerRankingsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPeerRankings_Should_Reject_Invalid_Limit_Above_Maximum()
    {
        // Arrange
        var invalidLimit = 101;

        // Act
        var result = await _controller.GetPeerRankings(invalidLimit, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Limit must be between 1 and 100", badRequestResult.Value?.ToString() ?? string.Empty);
        _analyticsServiceMock.Verify(x => x.GetPeerRankingsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPeerRankings_Should_Use_Default_Limit_When_Not_Specified()
    {
        // Arrange
        var defaultLimit = 20;
        var expectedRankings = new List<PeerPerformanceRanking>();

        _analyticsServiceMock
            .Setup(x => x.GetPeerRankingsAsync(defaultLimit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRankings);

        // Act
        var result = await _controller.GetPeerRankings(defaultLimit, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _analyticsServiceMock.Verify(x => x.GetPeerRankingsAsync(defaultLimit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEfficiencyMetrics_Should_Return_Ok_With_Metrics()
    {
        // Arrange
        var expectedMetrics = new SwarmEfficiencyMetrics
        {
            ChunkUtilization = 0.85,
            PeerUtilization = 0.75,
            RedundancyFactor = 2.5,
        };

        _analyticsServiceMock
            .Setup(x => x.GetEfficiencyMetricsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetEfficiencyMetrics(null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var metrics = Assert.IsType<SwarmEfficiencyMetrics>(okResult.Value);
        Assert.Equal(expectedMetrics.ChunkUtilization, metrics.ChunkUtilization);
        Assert.Equal(expectedMetrics.PeerUtilization, metrics.PeerUtilization);
    }

    [Fact]
    public async Task GetEfficiencyMetrics_Should_Use_Custom_TimeWindow()
    {
        // Arrange
        var timeWindowHours = 12;
        var expectedTimeWindow = TimeSpan.FromHours(12);
        var expectedMetrics = new SwarmEfficiencyMetrics();

        _analyticsServiceMock
            .Setup(x => x.GetEfficiencyMetricsAsync(expectedTimeWindow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetEfficiencyMetrics(timeWindowHours, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _analyticsServiceMock.Verify(x => x.GetEfficiencyMetricsAsync(expectedTimeWindow, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrends_Should_Return_Ok_With_Trends()
    {
        // Arrange
        var timeWindowHours = 24;
        var dataPoints = 24;
        var expectedTimeWindow = TimeSpan.FromHours(24);
        var expectedTrends = new SwarmTrends
        {
            TimePoints = new List<DateTime>(),
            SuccessRates = new List<double>(),
            AverageSpeeds = new List<double>(),
        };

        _analyticsServiceMock
            .Setup(x => x.GetTrendsAsync(expectedTimeWindow, dataPoints, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTrends);

        // Act
        var result = await _controller.GetTrends(timeWindowHours, dataPoints, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var trends = Assert.IsType<SwarmTrends>(okResult.Value);
        Assert.NotNull(trends.TimePoints);
        Assert.NotNull(trends.SuccessRates);
    }

    [Fact]
    public async Task GetTrends_Should_Reject_Invalid_TimeWindow_Below_Minimum()
    {
        // Arrange
        var invalidTimeWindow = 0;
        var dataPoints = 24;

        // Act
        var result = await _controller.GetTrends(invalidTimeWindow, dataPoints, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Time window must be between 1 and 168 hours", badRequestResult.Value?.ToString() ?? string.Empty);
        _analyticsServiceMock.Verify(x => x.GetTrendsAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTrends_Should_Reject_Invalid_TimeWindow_Above_Maximum()
    {
        // Arrange
        var invalidTimeWindow = 169; // > 168 hours (7 days)
        var dataPoints = 24;

        // Act
        var result = await _controller.GetTrends(invalidTimeWindow, dataPoints, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Time window must be between 1 and 168 hours", badRequestResult.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetTrends_Should_Reject_Invalid_DataPoints_Below_Minimum()
    {
        // Arrange
        var timeWindowHours = 24;
        var invalidDataPoints = 1; // < 2

        // Act
        var result = await _controller.GetTrends(timeWindowHours, invalidDataPoints, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Data points must be between 2 and 168", badRequestResult.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetTrends_Should_Reject_Invalid_DataPoints_Above_Maximum()
    {
        // Arrange
        var timeWindowHours = 24;
        var invalidDataPoints = 169; // > 168

        // Act
        var result = await _controller.GetTrends(timeWindowHours, invalidDataPoints, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Data points must be between 2 and 168", badRequestResult.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetTrends_Should_Use_Default_Values()
    {
        // Arrange
        var defaultTimeWindowHours = 24;
        var defaultDataPoints = 24;
        var expectedTimeWindow = TimeSpan.FromHours(defaultTimeWindowHours);
        var expectedTrends = new SwarmTrends();

        _analyticsServiceMock
            .Setup(x => x.GetTrendsAsync(expectedTimeWindow, defaultDataPoints, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTrends);

        // Act
        var result = await _controller.GetTrends(defaultTimeWindowHours, defaultDataPoints, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _analyticsServiceMock.Verify(x => x.GetTrendsAsync(expectedTimeWindow, defaultDataPoints, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecommendations_Should_Return_Ok_With_Recommendations()
    {
        // Arrange
        var expectedRecommendations = new List<SwarmRecommendation>
        {
            new SwarmRecommendation
            {
                Type = RecommendationType.PeerSelection,
                Priority = RecommendationPriority.High,
                Title = "Optimize Peer Selection",
                Description = "Consider using peers with higher reputation scores",
                Action = "Review peer rankings",
                EstimatedImpact = 0.15,
            },
        };

        _analyticsServiceMock
            .Setup(x => x.GetRecommendationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRecommendations);

        // Act
        var result = await _controller.GetRecommendations(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var recommendations = Assert.IsType<List<SwarmRecommendation>>(okResult.Value);
        Assert.Equal(expectedRecommendations.Count, recommendations.Count);
        Assert.Equal(expectedRecommendations[0].Title, recommendations[0].Title);
    }

    [Fact]
    public async Task GetRecommendations_Should_Return_Empty_List_When_No_Recommendations()
    {
        // Arrange
        var expectedRecommendations = new List<SwarmRecommendation>();

        _analyticsServiceMock
            .Setup(x => x.GetRecommendationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRecommendations);

        // Act
        var result = await _controller.GetRecommendations(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var recommendations = Assert.IsType<List<SwarmRecommendation>>(okResult.Value);
        Assert.Empty(recommendations);
    }
}
