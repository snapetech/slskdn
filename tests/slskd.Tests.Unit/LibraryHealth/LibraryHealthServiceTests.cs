namespace slskd.Tests.Unit.LibraryHealth
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Audio;
    using slskd.HashDb;
    using slskd.Integrations.MetadataFacade;
    using slskd.Integrations.MusicBrainz;
    using slskd.LibraryHealth;
    using slskd.LibraryHealth.Remediation;
    using Xunit;

    public class LibraryHealthServiceTests
    {
        [Fact]
        public async Task GetScanStatusAsync_WhenScanMissing_ReturnsNull()
        {
            var hashDb = new Mock<IHashDbService>();
            hashDb.Setup(m => m.GetLibraryHealthScanAsync("missing-scan", It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(LibraryHealthScan));

            var service = new LibraryHealthService(
                hashDb.Object,
                Mock.Of<ILibraryHealthRemediationService>(),
                Mock.Of<IMetadataFacade>(),
                Mock.Of<ICanonicalStatsService>(),
                Mock.Of<IMusicBrainzClient>(),
                Mock.Of<IOptionsMonitor<slskd.Options>>(),
                NullLogger<LibraryHealthService>.Instance);

            var scan = await service.GetScanStatusAsync("missing-scan");

            Assert.Null(scan);
        }
    }
}
