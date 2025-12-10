namespace slskd.Tests.Unit.Audio
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Audio;
    using slskd.HashDb;
    using Xunit;

    public class CanonicalStatsServiceTests
    {
        [Fact]
        public async Task AggregateStats_Should_SelectBestVariant_ByQualityThenSeen()
        {
            // Arrange
            var variants = new List<AudioVariant>
            {
                new() { VariantId = "v1", MusicBrainzRecordingId = "rec1", Codec = "FLAC", SampleRateHz = 44100, BitDepth = 16, Channels = 2, BitrateKbps = 900, QualityScore = 0.95, SeenCount = 5, TranscodeSuspect = false },
                new() { VariantId = "v2", MusicBrainzRecordingId = "rec1", Codec = "FLAC", SampleRateHz = 44100, BitDepth = 16, Channels = 2, BitrateKbps = 800, QualityScore = 0.90, SeenCount = 20, TranscodeSuspect = false },
                new() { VariantId = "v3", MusicBrainzRecordingId = "rec1", Codec = "MP3", SampleRateHz = 44100, BitDepth = null, Channels = 2, BitrateKbps = 320, QualityScore = 0.75, SeenCount = 30, TranscodeSuspect = false },
            };

            var mockDb = new Mock<IHashDbService>();
            mockDb.Setup(m => m.GetVariantsByRecordingAndProfileAsync("rec1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string profile, CancellationToken _) =>
                {
                    if (profile.Contains("FLAC")) return variants.GetRange(0, 2);
                    if (profile.Contains("MP3")) return new List<AudioVariant> { variants[2] };
                    return new List<AudioVariant>();
                });
            mockDb.Setup(m => m.UpsertCanonicalStatsAsync(It.IsAny<CanonicalStats>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockDb.Setup(m => m.GetVariantsByRecordingAsync("rec1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(variants);
            mockDb.Setup(m => m.GetCanonicalStatsAsync("rec1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CanonicalStats)null);
            mockDb.Setup(m => m.GetRecordingIdsWithVariantsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "rec1" });
            mockDb.Setup(m => m.GetCodecProfilesForRecordingAsync("rec1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "FLAC-16bit-44100Hz-2ch", "MP3-lossy-44100Hz-2ch" });

            var svc = new CanonicalStatsService(mockDb.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<CanonicalStatsService>>());

            // Act
            var candidates = await svc.GetCanonicalVariantCandidatesAsync("rec1");

            // Assert
            Assert.Equal(3, candidates.Count);
            Assert.Equal("v3", candidates[0].VariantId); // Best canonicality (prevalence) wins
        }
    }
}
