// Tests for QualityScorer
namespace slskd.Tests.Unit.VirtualSoulfind.v2.Matching
{
    using slskd.VirtualSoulfind.v2.Matching;
    using Xunit;

    public class QualityScorerTests
    {
        [Fact]
        public void FLAC_HighestQuality()
        {
            var score = QualityScorer.ScoreMusicQuality(".flac", 40_000_000);
            Assert.True(score >= 80);
        }

        [Fact]
        public void MP3_320_GoodQuality()
        {
            var score = QualityScorer.ScoreMusicQuality(".mp3", 10_000_000, 320);
            Assert.True(score >= 70);
        }

        [Fact]
        public void MP3_128_LowerQuality()
        {
            var score = QualityScorer.ScoreMusicQuality(".mp3", 5_000_000, 128);
            Assert.True(score < 70);
        }
    }
}
