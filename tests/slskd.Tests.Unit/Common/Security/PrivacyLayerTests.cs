// <copyright file="PrivacyLayerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Common.Security;

public class PrivacyLayerTests
{
    [Fact]
    public async Task GetStatisticsAsync_AfterFlushingQueuedBatch_ReportsBatchCreated()
    {
        using var privacyLayer = new PrivacyLayer(
            new PrivacyLayerOptions
            {
                Batching = new MessageBatchingOptions
                {
                    Enabled = true,
                    BatchWindowMs = 1000,
                    MaxBatchSize = 10,
                },
            },
            Mock.Of<ILogger<PrivacyLayer>>(),
            NullLoggerFactory.Instance);

        await privacyLayer.TransformOutboundAsync(new byte[] { 1, 2, 3 });
        var batch = await privacyLayer.FlushBatchAsync();
        var statistics = await privacyLayer.GetStatisticsAsync();

        Assert.Single(batch);
        Assert.Equal(1, statistics.BatchesCreated);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterEmptyFlush_DoesNotReportBatchCreated()
    {
        using var privacyLayer = new PrivacyLayer(
            new PrivacyLayerOptions
            {
                Batching = new MessageBatchingOptions
                {
                    Enabled = true,
                    BatchWindowMs = 1000,
                    MaxBatchSize = 10,
                },
            },
            Mock.Of<ILogger<PrivacyLayer>>(),
            NullLoggerFactory.Instance);

        var batch = await privacyLayer.FlushBatchAsync();
        var statistics = await privacyLayer.GetStatisticsAsync();

        Assert.Empty(batch);
        Assert.Equal(0, statistics.BatchesCreated);
    }
}
