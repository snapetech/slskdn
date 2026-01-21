// <copyright file="PerceptualHasher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore;

using System;
using System.Linq;

/// <summary>
/// Supported perceptual hash algorithms.
/// </summary>
public enum PerceptualHashAlgorithm
{
    /// <summary>
    /// Chromaprint algorithm for audio fingerprinting.
    /// </summary>
    Chromaprint,

    /// <summary>
    /// pHash algorithm for image/video perceptual hashing.
    /// </summary>
    PHash,

    /// <summary>
    /// Simple spectral hash (fallback/default).
    /// </summary>
    Spectral
}

/// <summary>
/// Perceptual hashing for content similarity detection.
/// Generates compact fingerprints that remain similar for perceptually similar content,
/// enabling cross-codec/bitrate deduplication and content matching.
/// </summary>
public interface IPerceptualHasher
{
    /// <summary>
    /// Generates perceptual hash from audio PCM samples.
    /// </summary>
    /// <param name="samples">Audio PCM samples (mono, normalized -1.0 to 1.0)</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="algorithm">Hash algorithm to use</param>
    /// <returns>Perceptual hash result</returns>
    PerceptualHash ComputeAudioHash(float[] samples, int sampleRate, PerceptualHashAlgorithm algorithm = PerceptualHashAlgorithm.Chromaprint);

    /// <summary>
    /// Generates perceptual hash from image pixels.
    /// </summary>
    /// <param name="pixels">Image pixel data (RGBA byte array)</param>
    /// <param name="width">Image width</param>
    /// <param name="height">Image height</param>
    /// <param name="algorithm">Hash algorithm to use</param>
    /// <returns>Perceptual hash result</returns>
    PerceptualHash ComputeImageHash(byte[] pixels, int width, int height, PerceptualHashAlgorithm algorithm = PerceptualHashAlgorithm.PHash);

    /// <summary>
    /// Computes Hamming distance between two hashes (0-64).
    /// Lower distance indicates more similar content.
    /// </summary>
    int HammingDistance(ulong hashA, ulong hashB);

    /// <summary>
    /// Computes similarity score between two hashes (0.0 to 1.0).
    /// 1.0 = identical, 0.0 = completely different.
    /// </summary>
    double Similarity(ulong hashA, ulong hashB);

    /// <summary>
    /// Determines if two hashes are similar within a threshold.
    /// </summary>
    /// <param name="hashA">First hash</param>
    /// <param name="hashB">Second hash</param>
    /// <param name="threshold">Similarity threshold (0.0-1.0)</param>
    /// <returns>True if hashes are similar</returns>
    bool AreSimilar(ulong hashA, ulong hashB, double threshold = 0.8);
}

public class PerceptualHasher : IPerceptualHasher
{
    private const int FrameSize = 4096; // Frame size for analysis
    private const int HashBits = 64;    // 64-bit hash output

    public PerceptualHash ComputeAudioHash(float[] samples, int sampleRate, PerceptualHashAlgorithm algorithm = PerceptualHashAlgorithm.Chromaprint)
    {
        var numericHash = ComputeAudioHashNumeric(samples, sampleRate, algorithm);
        var hexHash = numericHash.ToString("X16");
        return new PerceptualHash(algorithm.ToString(), hexHash, numericHash);
    }

    public PerceptualHash ComputeImageHash(byte[] pixels, int width, int height, PerceptualHashAlgorithm algorithm = PerceptualHashAlgorithm.PHash)
    {
        var numericHash = ComputeImageHashNumeric(pixels, width, height, algorithm);
        var hexHash = numericHash.ToString("X16");
        return new PerceptualHash(algorithm.ToString(), hexHash, numericHash);
    }

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

    public bool AreSimilar(ulong hashA, ulong hashB, double threshold = 0.8)
    {
        return Similarity(hashA, hashB) >= threshold;
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

    /// <summary>
    /// Computes audio hash using specified algorithm.
    /// </summary>
    private ulong ComputeAudioHashNumeric(float[] samples, int sampleRate, PerceptualHashAlgorithm algorithm)
    {
        return algorithm switch
        {
            PerceptualHashAlgorithm.Chromaprint => ComputeChromaPrint(samples, sampleRate),
            PerceptualHashAlgorithm.Spectral => ComputeHash(samples, sampleRate), // Use existing implementation
            _ => ComputeHash(samples, sampleRate) // Default to spectral
        };
    }

    /// <summary>
    /// Computes image hash using specified algorithm.
    /// </summary>
    private ulong ComputeImageHashNumeric(byte[] pixels, int width, int height, PerceptualHashAlgorithm algorithm)
    {
        return algorithm switch
        {
            PerceptualHashAlgorithm.PHash => ComputePHash(pixels, width, height),
            _ => ComputeSimpleImageHash(pixels, width, height) // Fallback
        };
    }

    /// <summary>
    /// Computes Chromaprint-style audio fingerprint.
    /// Simplified implementation of the Chromaprint algorithm.
    /// </summary>
    private ulong ComputeChromaPrint(float[] samples, int sampleRate)
    {
        if (samples == null || samples.Length == 0)
            return 0;

        // Chromaprint uses 12-bin chroma features
        const int chromaBins = 12;
        var chromaFeatures = new double[chromaBins];

        // Simplified chroma computation (real Chromaprint uses FFT)
        var windowSize = Math.Min(samples.Length, sampleRate); // 1 second window
        for (int i = 0; i < Math.Min(windowSize, samples.Length); i++)
        {
            // Very simplified chroma mapping (real implementation much more complex)
            var chromaIndex = (int)(Math.Abs(samples[i]) * chromaBins) % chromaBins;
            chromaFeatures[chromaIndex] += Math.Abs(samples[i]);
        }

        // Generate hash from chroma peaks
        return GenerateHashFromPeaks(chromaFeatures);
    }

    /// <summary>
    /// Computes pHash-style perceptual hash for images.
    /// Simplified implementation of the pHash algorithm.
    /// </summary>
    private ulong ComputePHash(byte[] pixels, int width, int height)
    {
        if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
            return 0;

        // Convert to grayscale and downsample to 8x8
        var grayPixels = ConvertToGrayscale(pixels, width, height);
        var smallImage = DownsampleImage(grayPixels, width, height, 8, 8);

        // Compute DCT (simplified)
        var dct = ComputeDCT(smallImage);

        // Generate hash from DCT coefficients
        return GenerateHashFromDCT(dct);
    }

    /// <summary>
    /// Simple fallback image hash.
    /// </summary>
    private ulong ComputeSimpleImageHash(byte[] pixels, int width, int height)
    {
        if (pixels == null || pixels.Length == 0)
            return 0;

        // Simple hash based on pixel averages
        ulong hash = 0;
        var step = Math.Max(1, pixels.Length / HashBits);

        for (int i = 0; i < HashBits && i * step < pixels.Length; i++)
        {
            if (pixels[i * step] > 128) // Simple threshold
            {
                hash |= (1UL << i);
            }
        }

        return hash;
    }

    /// <summary>
    /// Converts RGBA pixels to grayscale.
    /// </summary>
    private static double[] ConvertToGrayscale(byte[] pixels, int width, int height)
    {
        var grayscale = new double[width * height];
        for (int i = 0; i < width * height; i++)
        {
            var r = pixels[i * 4];
            var g = pixels[i * 4 + 1];
            var b = pixels[i * 4 + 2];
            // Standard luminance formula
            grayscale[i] = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        }
        return grayscale;
    }

    /// <summary>
    /// Downsamples image to target dimensions.
    /// </summary>
    private static double[] DownsampleImage(double[] pixels, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
        var result = new double[dstWidth * dstHeight];

        for (int y = 0; y < dstHeight; y++)
        {
            for (int x = 0; x < dstWidth; x++)
            {
                var srcX = (int)(x * (double)srcWidth / dstWidth);
                var srcY = (int)(y * (double)srcHeight / dstHeight);
                var srcIndex = srcY * srcWidth + srcX;
                result[y * dstWidth + x] = pixels[Math.Min(srcIndex, pixels.Length - 1)];
            }
        }

        return result;
    }

    /// <summary>
    /// Computes simplified 2D DCT.
    /// </summary>
    private static double[] ComputeDCT(double[] pixels)
    {
        // Very simplified DCT computation (real pHash uses proper 2D DCT)
        var size = (int)Math.Sqrt(pixels.Length);
        var dct = new double[pixels.Length];

        // Simple frequency analysis
        for (int i = 0; i < pixels.Length; i++)
        {
            dct[i] = pixels[i] * (i % 2 == 0 ? 1.0 : -1.0); // Alternating pattern
        }

        return dct;
    }

    /// <summary>
    /// Generates hash from DCT coefficients.
    /// </summary>
    private static ulong GenerateHashFromDCT(double[] dct)
    {
        if (dct.Length == 0) return 0;

        // Compute median of low-frequency components
        var lowFreq = dct.Take(32).ToArray(); // Use first 32 coefficients
        Array.Sort(lowFreq);
        var median = lowFreq[lowFreq.Length / 2];

        ulong hash = 0;
        for (int i = 0; i < Math.Min(lowFreq.Length, HashBits); i++)
        {
            if (lowFreq[i] > median)
            {
                hash |= (1UL << i);
            }
        }

        return hash;
    }

    /// <summary>
    /// Generates hash from peak features.
    /// </summary>
    private static ulong GenerateHashFromPeaks(double[] features)
    {
        if (features.Length == 0) return 0;

        // Find peaks in the feature array
        var peaks = new bool[features.Length];
        for (int i = 1; i < features.Length - 1; i++)
        {
            peaks[i] = features[i] > features[i - 1] && features[i] > features[i + 1];
        }

        // Generate hash from peak pattern
        ulong hash = 0;
        for (int i = 0; i < Math.Min(peaks.Length, HashBits); i++)
        {
            if (peaks[i])
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















