namespace slskd.Tests.Unit.Transfers.MultiSource.Metrics
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using slskd.HashDb;
    using slskd.Telemetry;
    using slskd.Transfers.MultiSource.Metrics;
    using Xunit;

    public class PeerMetricsServiceTests
    {
        [Fact]
        public async Task GetMetricsAsync_WhenHashDbHasNoRow_ReturnsNewDefaultMetrics()
        {
            var hashDb = new Mock<IHashDbService>();
            hashDb.Setup(m => m.GetPeerMetricsAsync("peer-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(PeerPerformanceMetrics));

            var service = new PeerMetricsService(hashDb.Object, NullLogger<PeerMetricsService>.Instance);

            var metrics = await service.GetMetricsAsync("peer-1", PeerSource.Overlay);

            Assert.NotNull(metrics);
            Assert.Equal("peer-1", metrics.PeerId);
            Assert.Equal(PeerSource.Overlay, metrics.Source);
            Assert.Equal(0.5, metrics.ReputationScore);
        }
    }
}
