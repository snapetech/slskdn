namespace slskd.Tests.Unit.MediaCore;

using System;
using System.IO;
using slskd;
using slskd.MediaCore;
using Xunit;

public class PerceptualHasherTests
{
    private readonly PerceptualHasher hasher = new();

    [Fact]
    public void ComputeHash_IdenticalSamples_ReturnsSameHash()
    {
        var samples = GenerateSineWave(44100, 1.0f, 440);
        
        var hash1 = hasher.ComputeHash(samples, 44100);
        var hash2 = hasher.ComputeHash(samples, 44100);
        
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_EmptySamples_ReturnsZero()
    {
        var hash = hasher.ComputeHash(Array.Empty<float>(), 44100);
        Assert.Equal(0UL, hash);
    }

    [Fact]
    public void ComputeHash_NullSamples_ReturnsZero()
    {
        var hash = hasher.ComputeHash(null, 44100);
        Assert.Equal(0UL, hash);
    }

    [Fact]
    public void HammingDistance_IdenticalHashes_ReturnsZero()
    {
        var hash = 0x123456789ABCDEF0UL;
        var distance = hasher.HammingDistance(hash, hash);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void HammingDistance_CompletelyDifferent_ReturnsMaxDistance()
    {
        var hash1 = 0xFFFFFFFFFFFFFFFFUL;
        var hash2 = 0x0000000000000000UL;
        var distance = hasher.HammingDistance(hash1, hash2);
        Assert.Equal(64, distance);
    }

    [Fact]
    public void ComputeAudioHash_ChromaPrintAlgorithm_ReturnsValidResult()
    {
        // Arrange
        var samples = GenerateSineWave(44100, 1.0f, 440);

        // Act
        var result = hasher.ComputeAudioHash(samples, 44100, PerceptualHashAlgorithm.Chromaprint);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Chromaprint", result.Algorithm); // Matches PerceptualHashAlgorithm enum ToString
        Assert.NotNull(result.Hex);
        Assert.True(result.NumericHash.HasValue);
        Assert.Equal(16, result.Hex.Length); // 64-bit hash as hex
    }

    [Fact]
    public void ComputeAudioHash_SpectralAlgorithm_ReturnsValidResult()
    {
        // Arrange
        var samples = GenerateSineWave(44100, 1.0f, 440);

        // Act
        var result = hasher.ComputeAudioHash(samples, 44100, PerceptualHashAlgorithm.Spectral);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Spectral", result.Algorithm);
        Assert.NotNull(result.Hex);
        Assert.True(result.NumericHash.HasValue);
    }

    [Fact]
    public void ComputeImageHash_PHashAlgorithm_ReturnsValidResult()
    {
        // Arrange
        var pixels = GenerateTestImagePixels(32, 32); // 32x32 RGBA image
        var width = 32;
        var height = 32;

        // Act
        var result = hasher.ComputeImageHash(pixels, width, height, PerceptualHashAlgorithm.PHash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PHash", result.Algorithm);
        Assert.NotNull(result.Hex);
        Assert.True(result.NumericHash.HasValue);
    }

    [Fact]
    public void ComputeImageHash_EmptyPixels_ReturnsValidResult()
    {
        // Act
        var result = hasher.ComputeImageHash(Array.Empty<byte>(), 0, 0, PerceptualHashAlgorithm.PHash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PHash", result.Algorithm);
        // Should handle empty input gracefully
    }

    [Fact]
    public void AreSimilar_IdenticalHashes_ReturnsTrue()
    {
        // Arrange
        var hash = 0x123456789ABCDEF0UL;

        // Act
        var result = hasher.AreSimilar(hash, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreSimilar_CompletelyDifferentHashes_ReturnsFalse()
    {
        // Arrange
        var hash1 = 0xFFFFFFFFFFFFFFFFUL;
        var hash2 = 0x0000000000000000UL;

        // Act
        var result = hasher.AreSimilar(hash1, hash2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreSimilar_SimilarHashes_ReturnsTrue()
    {
        // Arrange - hashes that differ by only a few bits
        var hash1 = 0x123456789ABCDEF0UL;
        var hash2 = 0x123456789ABCDEF1UL; // Only 1 bit different

        // Act
        var result = hasher.AreSimilar(hash1, hash2, threshold: 0.9);

        // Assert
        Assert.True(result);
    }


    [Fact]
    public void Similarity_CompletelyDifferentHashes_ReturnsZero()
    {
        // Arrange
        var hash1 = 0xFFFFFFFFFFFFFFFFUL;
        var hash2 = 0x0000000000000000UL;

        // Act
        var similarity = hasher.Similarity(hash1, hash2);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Theory]
    [InlineData(PerceptualHashAlgorithm.Chromaprint)]
    [InlineData(PerceptualHashAlgorithm.PHash)]
    [InlineData(PerceptualHashAlgorithm.Spectral)]
    public void ComputeAudioHash_AllAlgorithms_Supported(PerceptualHashAlgorithm algorithm)
    {
        // Arrange
        var samples = GenerateSineWave(44100, 1.0f, 440);

        // Act
        var result = hasher.ComputeAudioHash(samples, 44100, algorithm);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(algorithm.ToString(), result.Algorithm);
    }

    [Theory]
    [InlineData(PerceptualHashAlgorithm.PHash)]
    public void ComputeImageHash_AllAlgorithms_Supported(PerceptualHashAlgorithm algorithm)
    {
        // Arrange
        var pixels = GenerateTestImagePixels(32, 32);

        // Act
        var result = hasher.ComputeImageHash(pixels, 32, 32, algorithm);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(algorithm.ToString(), result.Algorithm);
    }


    private static byte[] GenerateTestImagePixels(int width, int height)
    {
        var pixels = new byte[width * height * 4]; // RGBA

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)(i % 256);     // R
            pixels[i + 1] = (byte)((i + 85) % 256);  // G
            pixels[i + 2] = (byte)((i + 170) % 256); // B
            pixels[i + 3] = 255;             // A
        }

        return pixels;
    }

    [Fact]
    public void HammingDistance_OneBitDifferent_ReturnsOne()
    {
        var hash1 = 0b0000000000000000UL;
        var hash2 = 0b0000000000000001UL;
        var distance = hasher.HammingDistance(hash1, hash2);
        Assert.Equal(1, distance);
    }

    [Fact]
    public void Similarity_IdenticalHashes_ReturnsOne()
    {
        var hash = 0x123456789ABCDEF0UL;
        var similarity = hasher.Similarity(hash, hash);
        Assert.Equal(1.0, similarity);
    }

    [Fact]
    public void Similarity_CompletelyDifferent_ReturnsZero()
    {
        var hash1 = 0xFFFFFFFFFFFFFFFFUL;
        var hash2 = 0x0000000000000000UL;
        var similarity = hasher.Similarity(hash1, hash2);
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void Similarity_OneBitDifferent_ReturnsHighSimilarity()
    {
        var hash1 = 0xFFFFFFFFFFFFFFFFUL;
        var hash2 = 0xFFFFFFFFFFFFFFFEUL; // Last bit flipped
        var similarity = hasher.Similarity(hash1, hash2);
        Assert.Equal(63.0 / 64.0, similarity); // 63 matching bits out of 64
    }

    [Fact]
    public void ComputeHash_SimilarSineWaves_ProduceSimilarHashes()
    {
        // Generate two sine waves with same frequency
        var samples1 = GenerateSineWave(44100, 1.0f, 440);
        var samples2 = GenerateSineWave(44100, 1.0f, 440);
        
        var hash1 = hasher.ComputeHash(samples1, 44100);
        var hash2 = hasher.ComputeHash(samples2, 44100);
        
        var similarity = hasher.Similarity(hash1, hash2);
        Assert.True(similarity > 0.8, $"Expected similarity > 0.8, got {similarity}");
    }

    [Fact]
    public void ComputeHash_DifferentFrequencies_ProduceDifferentHashes()
    {
        // 440 vs 880 Hz: one octave (2×), same pitch class (A); spectral hasher often treats them as similar.
        // 440 vs 262 Hz (A4 vs C4): different pitch classes; better for "different freqs → different hashes".
        var samples1 = GenerateSineWave(44100, 1.0f, 440);
        var samples2 = GenerateSineWave(44100, 1.0f, 262);
        
        var hash1 = hasher.ComputeHash(samples1, 44100);
        var hash2 = hasher.ComputeHash(samples2, 44100);
        
        var similarity = hasher.Similarity(hash1, hash2);
        Assert.True(similarity < 0.95, $"Expected similarity < 0.95 for 440 vs 262 Hz, got {similarity}");
    }

    /// <summary>
    /// FFT-based Chromaprint must treat 440 Hz and 880 Hz as different (low similarity at 0.5 threshold).
    /// Aligns with CrossCodecMatchingTests.DifferentContent_LowSimilarityScores.
    /// </summary>
    [Fact]
    public void ComputeAudioHash_Chromaprint_440vs880Hz_ProducesLowSimilarity()
    {
        var samples1 = GenerateSineWave(44100, 2.0f, 440.0f);
        var samples2 = GenerateSineWave(44100, 2.0f, 880.0f);

        var r1 = hasher.ComputeAudioHash(samples1, 44100, PerceptualHashAlgorithm.Chromaprint);
        var r2 = hasher.ComputeAudioHash(samples2, 44100, PerceptualHashAlgorithm.Chromaprint);

        Assert.NotNull(r1.NumericHash);
        Assert.NotNull(r2.NumericHash);
        var similar = hasher.AreSimilar(r1.NumericHash.Value, r2.NumericHash.Value, 0.5);
        Assert.False(similar, "Chromaprint must not treat 440 Hz and 880 Hz as similar at 0.5 threshold");
    }

    [Fact]
    public void ComputeHash_DownsamplingProducesSimilarHash()
    {
        // Test that downsampling doesn't drastically change the hash
        var samples = GenerateSineWave(44100, 1.0f, 440);
        
        var hashHighRate = hasher.ComputeHash(samples, 44100);
        var hashLowRate = hasher.ComputeHash(samples, 22050); // Triggers internal downsampling
        
        // Hashes won't be identical due to downsampling, but should be similar
        var similarity = hasher.Similarity(hashHighRate, hashLowRate);
        Assert.True(similarity > 0.5, $"Expected similarity > 0.5 after downsampling, got {similarity}");
    }

    [Fact]
    public void HammingDistance_IsSymmetric()
    {
        var hash1 = 0x123456789ABCDEF0UL;
        var hash2 = 0xFEDCBA9876543210UL;
        
        var distance1 = hasher.HammingDistance(hash1, hash2);
        var distance2 = hasher.HammingDistance(hash2, hash1);
        
        Assert.Equal(distance1, distance2);
    }

    [Fact]
    public void Similarity_IsSymmetric()
    {
        var hash1 = 0x123456789ABCDEF0UL;
        var hash2 = 0xFEDCBA9876543210UL;
        
        var similarity1 = hasher.Similarity(hash1, hash2);
        var similarity2 = hasher.Similarity(hash2, hash1);
        
        Assert.Equal(similarity1, similarity2);
    }

    /// <summary>
    /// AudioUtilities.ExtractPcmSamples throws FileNotFoundException when the file does not exist.
    /// </summary>
    [Fact]
    public void ExtractPcmSamples_throws_FileNotFoundException_when_file_missing()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => AudioUtilities.ExtractPcmSamples("/nonexistent/path.wav"));
        Assert.Contains("nonexistent", ex.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.984375)] // 63/64
    [InlineData(32, 0.5)] // Half different
    [InlineData(64, 0.0)]
    public void Similarity_CorrectlyComputesFromDistance(int distance, double expected)
    {
        // Create two hashes with exactly 'distance' bits different
        var hash1 = 0UL;
        var hash2 = 0UL;
        for (int i = 0; i < distance; i++)
        {
            hash2 |= (1UL << i);
        }
        
        var similarity = hasher.Similarity(hash1, hash2);
        Assert.Equal(expected, similarity, precision: 6);
    }

    // Helper method to generate a sine wave
    private static float[] GenerateSineWave(int sampleRate, float duration, float frequency)
    {
        var sampleCount = (int)(sampleRate * duration);
        var samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            var time = i / (float)sampleRate;
            samples[i] = (float)Math.Sin(2 * Math.PI * frequency * time);
        }
        
        return samples;
    }
}
