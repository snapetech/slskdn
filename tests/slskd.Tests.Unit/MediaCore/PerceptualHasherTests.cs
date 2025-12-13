namespace slskd.Tests.Unit.MediaCore;

using System;
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
        // 440 Hz (A4) vs 880 Hz (A5) - one octave apart
        var samples1 = GenerateSineWave(44100, 1.0f, 440);
        var samples2 = GenerateSineWave(44100, 1.0f, 880);
        
        var hash1 = hasher.ComputeHash(samples1, 44100);
        var hash2 = hasher.ComputeHash(samples2, 44100);
        
        var similarity = hasher.Similarity(hash1, hash2);
        Assert.True(similarity < 0.8, $"Expected similarity < 0.8, got {similarity}");
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















