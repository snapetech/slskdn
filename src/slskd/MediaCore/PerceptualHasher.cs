namespace slskd.MediaCore;

using System;
using System.Linq;

/// <summary>
/// Perceptual hashing for audio similarity detection.
/// Generates compact fingerprints that remain similar for perceptually similar audio,
/// enabling cross-codec/bitrate deduplication and content matching.
/// </summary>
public interface IPerceptualHasher
{
    /// <summary>
    /// Generates perceptual hash from audio PCM samples.
    /// </summary>
    /// <param name="samples">Audio PCM samples (mono, normalized -1.0 to 1.0)</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <returns>64-bit perceptual hash</returns>
    ulong ComputeHash(float[] samples, int sampleRate);

    /// <summary>
    /// Computes Hamming distance between two hashes (0-64).
    /// Lower distance indicates more similar audio.
    /// </summary>
    int HammingDistance(ulong hashA, ulong hashB);

    /// <summary>
    /// Computes similarity score between two hashes (0.0 to 1.0).
    /// 1.0 = identical, 0.0 = completely different.
    /// </summary>
    double Similarity(ulong hashA, ulong hashB);
}

public class PerceptualHasher : IPerceptualHasher
{
    private const int FrameSize = 4096; // Frame size for analysis
    private const int HashBits = 64;    // 64-bit hash output

    public ulong ComputeHash(float[] samples, int sampleRate)
    {
        if (samples == null || samples.Length == 0)
            return 0;

        // Downsample to ~11kHz mono if needed (reduces computation)
        var targetRate = 11025;
        if (sampleRate > targetRate)
        {
            samples = Downsample(samples, sampleRate, targetRate);
            sampleRate = targetRate;
        }

        // Compute spectral features across 8 time windows
        var features = new double[8];
        var windowSize = samples.Length / 8;

        for (int w = 0; w < 8; w++)
        {
            var start = w * windowSize;
            var end = Math.Min(start + windowSize, samples.Length);
            var window = samples[start..end];

            // Compute energy in frequency bands (simplified spectral feature)
            features[w] = ComputeSpectralEnergy(window);
        }

        // Generate hash from feature comparisons
        return GenerateHash(features);
    }

    public int HammingDistance(ulong hashA, ulong hashB)
    {
        var xor = hashA ^ hashB;
        var distance = 0;

        // Count set bits in XOR result
        while (xor != 0)
        {
            distance += (int)(xor & 1);
            xor >>= 1;
        }

        return distance;
    }

    public double Similarity(ulong hashA, ulong hashB)
    {
        var distance = HammingDistance(hashA, hashB);
        return 1.0 - ((double)distance / HashBits);
    }

    /// <summary>
    /// Downsamples audio to target sample rate using simple decimation.
    /// </summary>
    private static float[] Downsample(float[] samples, int fromRate, int toRate)
    {
        var ratio = fromRate / (double)toRate;
        var newLength = (int)(samples.Length / ratio);
        var result = new float[newLength];

        for (int i = 0; i < newLength; i++)
        {
            var srcIndex = (int)(i * ratio);
            if (srcIndex < samples.Length)
                result[i] = samples[srcIndex];
        }

        return result;
    }

    /// <summary>
    /// Computes spectral energy for a window of samples.
    /// Simplified frequency analysis without full FFT.
    /// </summary>
    private static double ComputeSpectralEnergy(float[] window)
    {
        if (window.Length == 0) return 0;

        // Compute RMS energy as simplified spectral feature
        var sum = 0.0;
        foreach (var sample in window)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / window.Length);
    }

    /// <summary>
    /// Generates 64-bit hash from feature vector.
    /// Uses median comparison (similar to pHash algorithm).
    /// </summary>
    private static ulong GenerateHash(double[] features)
    {
        if (features.Length == 0) return 0;

        // Compute median feature value
        var sorted = features.OrderBy(x => x).ToArray();
        var median = sorted[features.Length / 2];

        // Generate hash bits: 1 if feature > median, 0 otherwise
        ulong hash = 0;
        for (int i = 0; i < Math.Min(features.Length, HashBits); i++)
        {
            if (features[i] > median)
            {
                hash |= (1UL << i);
            }
        }

        return hash;
    }
}

/// <summary>
/// Audio utility for extracting PCM samples from various formats.
/// Simplified implementation - in production, use FFmpeg/NAudio for decoding.
/// </summary>
public static class AudioUtilities
{
    /// <summary>
    /// Placeholder for PCM extraction from audio file.
    /// In production: use FFmpeg/NAudio to decode MP3/FLAC/etc to PCM.
    /// </summary>
    public static (float[] Samples, int SampleRate) ExtractPcmSamples(string audioFilePath)
    {
        // TODO: Integrate FFmpeg or NAudio for real audio decoding
        // For now, return empty (this would be called by a background job)
        throw new NotImplementedException(
            "PCM extraction requires FFmpeg/NAudio integration. " +
            "Install ffmpeg and use: ffmpeg -i input.mp3 -f f32le -ac 1 -ar 11025 output.raw");
    }

    /// <summary>
    /// Converts stereo to mono by averaging channels.
    /// </summary>
    public static float[] StereoToMono(float[] stereoSamples)
    {
        var monoSamples = new float[stereoSamples.Length / 2];
        for (int i = 0; i < monoSamples.Length; i++)
        {
            monoSamples[i] = (stereoSamples[i * 2] + stereoSamples[i * 2 + 1]) / 2.0f;
        }
        return monoSamples;
    }
}














