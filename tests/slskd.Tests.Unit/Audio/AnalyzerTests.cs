namespace slskd.Tests.Unit.Audio
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using slskd.Audio;
    using slskd.Audio.Analyzers;
    using slskd.Transfers.MultiSource;
    using Xunit;

    public class AnalyzerTests
    {
        [Fact]
        public void FlacAnalyzer_ParsesStreamInfo_And_SetsQuality()
        {
            var temp = CreateTempFlac(sampleRate: 44100, bitsPerSample: 16, channels: 2);
            var analyzer = new FlacAnalyzer();
            var variant = new AudioVariant { BitrateKbps = 900 };

            var result = analyzer.Analyze(temp, variant);

            Assert.NotNull(result.FlacStreamInfoHash42);
            Assert.Equal("00112233445566778899aabbccddeeff", result.FlacPcmMd5);
            Assert.Equal(44100, result.SampleRateHz);
            Assert.Equal(16, result.BitDepth);
            Assert.Equal(2, result.Channels);
            Assert.False(result.TranscodeSuspect);
            Assert.InRange(result.QualityScore, 0.9, 1.0);
        }

        [Fact]
        public void FlacAnalyzer_LowBitrate_FlagsTranscode()
        {
            var temp = CreateTempFlac(sampleRate: 44100, bitsPerSample: 16, channels: 2);
            var analyzer = new FlacAnalyzer();
            var variant = new AudioVariant { BitrateKbps = 200 };

            var result = analyzer.Analyze(temp, variant);

            Assert.True(result.TranscodeSuspect);
            Assert.Contains("bitrate", result.TranscodeReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Mp3Analyzer_Strips_Id3v2_For_StreamHash()
        {
            using var dir = new TempDir();
            var filePath = Path.Combine(dir.Path, "test.mp3");

            var id3Size = 20; // syncsafe 0x00 00 00 14
            var header = Encoding.ASCII.GetBytes("ID3");
            var id3 = new byte[10];
            Array.Copy(header, id3, 3);
            id3[3] = 0x04; // version
            id3[6] = 0x00;
            id3[7] = 0x00;
            id3[8] = 0x00;
            id3[9] = 0x14; // size = 20 bytes

            var frame = Enumerable.Repeat((byte)0xAB, 16).ToArray();
            File.WriteAllBytes(filePath, id3.Concat(new byte[id3Size]).Concat(frame).ToArray());

            var expectedHash = ComputeSha256(frame);

            var analyzer = new Mp3Analyzer();
            var variant = new AudioVariant { BitrateKbps = 192 };
            var result = analyzer.Analyze(filePath, variant);

            Assert.Equal(expectedHash, result.Mp3StreamHash);
            Assert.False(result.TranscodeSuspect);
            Assert.InRange(result.QualityScore, 0.5, 1.0);
        }

        [Fact]
        public void OpusAnalyzer_Computes_StreamHash()
        {
            using var dir = new TempDir();
            var filePath = Path.Combine(dir.Path, "test.opus");
            var payload = Enumerable.Repeat((byte)0x42, 200).ToArray();
            File.WriteAllBytes(filePath, payload);

            var analyzer = new OpusAnalyzer();
            var variant = new AudioVariant { BitrateKbps = 128 };
            var result = analyzer.Analyze(filePath, variant);

            Assert.NotNull(result.OpusStreamHash);
            Assert.InRange(result.QualityScore, 0.4, 0.85);
        }

        [Fact]
        public void AacAnalyzer_Hashes_Mdat_Payload()
        {
            using var dir = new TempDir();
            var filePath = Path.Combine(dir.Path, "test.m4a");

            var mdatPayload = Enumerable.Repeat((byte)0x5A, 32).ToArray();
            var mdatSize = 8 + mdatPayload.Length;
            var ftyp = BuildAtom("ftyp", new byte[12]);
            var mdat = BuildAtom("mdat", mdatPayload, mdatSize);

            File.WriteAllBytes(filePath, ftyp.Concat(mdat).ToArray());

            var expectedHash = ComputeSha256(mdatPayload);

            var analyzer = new AacAnalyzer();
            var variant = new AudioVariant { BitrateKbps = 256 };
            var result = analyzer.Analyze(filePath, variant);

            Assert.Equal(expectedHash, result.AacStreamHash);
            Assert.InRange(result.QualityScore, 0.5, 0.85);
        }

        private static string CreateTempFlac(int sampleRate, int bitsPerSample, int channels)
        {
            var path = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".flac");
            var data = new byte[FlacStreamInfoParser.MinimumBytesNeeded];

            // magic
            data[0] = 0x66; data[1] = 0x4C; data[2] = 0x61; data[3] = 0x43;
            // header: type=0, length=34
            data[4] = 0x00;
            data[5] = 0x00; data[6] = 0x00; data[7] = 0x22;

            // streaminfo body
            var offset = 8;
            data[offset + 0] = 0x00; data[offset + 1] = 0x10; // min block
            data[offset + 2] = 0x00; data[offset + 3] = 0x20; // max block
            data[offset + 4] = 0x00; data[offset + 5] = 0x00; data[offset + 6] = 0x10; // min frame
            data[offset + 7] = 0x00; data[offset + 8] = 0x00; data[offset + 9] = 0x20; // max frame

            var sr = sampleRate;
            data[offset + 10] = (byte)((sr >> 12) & 0xFF);
            data[offset + 11] = (byte)((sr >> 4) & 0xFF);
            data[offset + 12] = (byte)(((sr & 0x0F) << 4) | ((channels - 1 & 0x07) << 1) | ((bitsPerSample - 1) >> 4));
            data[offset + 13] = (byte)((bitsPerSample - 1 & 0x0F) << 4);
            data[offset + 14] = 0x00; data[offset + 15] = 0x00; data[offset + 16] = 0x00; data[offset + 17] = 0x64; // total samples 0x64

            var md5 = Enumerable.Range(0, 16).Select(i => (byte)((i * 0x11) & 0xFF)).ToArray();
            Array.Copy(md5, 0, data, offset + 18, 16);

            File.WriteAllBytes(path, data);
            return path;
        }

        private static byte[] BuildAtom(string type, byte[] payload, int? sizeOverride = null)
        {
            var size = sizeOverride ?? (8 + payload.Length);
            var buffer = new byte[size];
            buffer[0] = (byte)((size >> 24) & 0xFF);
            buffer[1] = (byte)((size >> 16) & 0xFF);
            buffer[2] = (byte)((size >> 8) & 0xFF);
            buffer[3] = (byte)(size & 0xFF);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            Array.Copy(typeBytes, 0, buffer, 4, 4);
            Array.Copy(payload, 0, buffer, 8, Math.Min(payload.Length, buffer.Length - 8));
            return buffer;
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class TempDir : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));

            public TempDir()
            {
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
















