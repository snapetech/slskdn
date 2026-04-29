// <copyright file="LibraryHealthServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.LibraryHealth
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
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
    using slskd.Tests.Unit;
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

        [Fact]
        public async Task StartScanAsync_WhenBackgroundScanFails_ReturnsSanitizedErrorMessage()
        {
            var shareRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(shareRoot);

            try
            {
                var persistedScans = new ConcurrentDictionary<string, LibraryHealthScan>();
                var hashDb = new Mock<IHashDbService>();
                hashDb
                    .Setup(m => m.UpsertLibraryHealthScanAsync(It.IsAny<LibraryHealthScan>(), It.IsAny<CancellationToken>()))
                    .Returns<LibraryHealthScan, CancellationToken>((scan, _) =>
                    {
                        persistedScans[scan.ScanId] = Clone(scan);
                        return Task.CompletedTask;
                    });
                hashDb
                    .Setup(m => m.GetLibraryHealthScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns<string, CancellationToken>((scanId, _) =>
                    {
                        persistedScans.TryGetValue(scanId, out var scan);
                        return Task.FromResult(scan);
                    });

                var options = new slskd.Options
                {
                    Shares = new slskd.Options.SharesOptions
                    {
                        Directories = new[] { shareRoot },
                    },
                };

                var service = new LibraryHealthService(
                    hashDb.Object,
                    Mock.Of<ILibraryHealthRemediationService>(),
                    Mock.Of<IMetadataFacade>(),
                    Mock.Of<ICanonicalStatsService>(),
                    Mock.Of<IMusicBrainzClient>(),
                    new TestOptionsMonitor<slskd.Options>(options),
                    NullLogger<LibraryHealthService>.Instance);

                var scanId = await service.StartScanAsync(
                    new LibraryHealthScanRequest
                    {
                        LibraryPath = Path.Combine(shareRoot, "missing"),
                    },
                    CancellationToken.None);

                LibraryHealthScan? status = null;
                for (var attempt = 0; attempt < 50; attempt++)
                {
                    status = await service.GetScanStatusAsync(scanId, CancellationToken.None);
                    if (status?.Status == ScanStatus.Failed)
                    {
                        break;
                    }

                    await Task.Delay(20);
                }

                Assert.NotNull(status);
                Assert.Equal(ScanStatus.Failed, status!.Status);
                Assert.Equal("Library health scan failed", status.ErrorMessage);
                Assert.DoesNotContain("missing", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (Directory.Exists(shareRoot))
                {
                    Directory.Delete(shareRoot, true);
                }
            }
        }

        private static LibraryHealthScan Clone(LibraryHealthScan scan)
        {
            return new LibraryHealthScan
            {
                ScanId = scan.ScanId,
                LibraryPath = scan.LibraryPath,
                StartedAt = scan.StartedAt,
                CompletedAt = scan.CompletedAt,
                Status = scan.Status,
                FilesScanned = scan.FilesScanned,
                IssuesDetected = scan.IssuesDetected,
                ErrorMessage = scan.ErrorMessage,
            };
        }
    }
}
