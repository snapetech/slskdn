// <copyright file="EntropyMonitor.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Monitors cryptographic entropy and random number generator health.
/// SECURITY: Detects potential issues with randomness that could compromise security.
/// </summary>
public sealed class EntropyMonitor : IDisposable
{
    private readonly ILogger<EntropyMonitor> _logger;
    private readonly Timer _checkTimer;
    private readonly ConcurrentQueue<EntropyCheck> _history = new();

    /// <summary>
    /// Number of bytes to sample for entropy testing.
    /// </summary>
    public const int SampleSize = 256;

    /// <summary>
    /// Maximum history entries to keep.
    /// </summary>
    public const int MaxHistorySize = 100;

    /// <summary>
    /// Minimum acceptable entropy (bits per byte, max is 8).
    /// </summary>
    public const double MinAcceptableEntropy = 7.0;

    /// <summary>
    /// Warning threshold for entropy.
    /// </summary>
    public const double WarningEntropy = 7.5;

    /// <summary>
    /// Gets the last entropy check result.
    /// </summary>
    public EntropyCheck? LastCheck { get; private set; }

    /// <summary>
    /// Event raised when entropy issues are detected.
    /// </summary>
    public event EventHandler<EntropyAlertEventArgs>? EntropyAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntropyMonitor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="checkInterval">Interval between checks (default: 5 minutes).</param>
    public EntropyMonitor(ILogger<EntropyMonitor> logger, TimeSpan? checkInterval = null)
    {
        _logger = logger;
        var interval = checkInterval ?? TimeSpan.FromMinutes(5);
        _checkTimer = new Timer(PerformCheck, null, TimeSpan.Zero, interval);
    }

    /// <summary>
    /// Perform an immediate entropy check.
    /// </summary>
    /// <returns>The check result.</returns>
    public EntropyCheck CheckNow()
    {
        var sample = RandomNumberGenerator.GetBytes(SampleSize);
        var entropy = CalculateShannonEntropy(sample);
        var distribution = AnalyzeDistribution(sample);

        var check = new EntropyCheck
        {
            Timestamp = DateTimeOffset.UtcNow,
            Entropy = entropy,
            SampleSize = SampleSize,
            ByteDistribution = distribution,
            Status = GetStatus(entropy),
        };

        LastCheck = check;

        // Add to history
        _history.Enqueue(check);
        while (_history.Count > MaxHistorySize)
        {
            _history.TryDequeue(out _);
        }

        // Log and alert if issues
        if (check.Status == EntropyStatus.Critical)
        {
            _logger.LogCritical(
                "CRITICAL: Low entropy detected! Entropy: {Entropy:F3} bits/byte (min: {Min}). Cryptographic security may be compromised!",
                entropy, MinAcceptableEntropy);
            EntropyAlert?.Invoke(this, new EntropyAlertEventArgs(check));
        }
        else if (check.Status == EntropyStatus.Warning)
        {
            _logger.LogWarning(
                "Warning: Entropy below optimal level. Entropy: {Entropy:F3} bits/byte (warning: {Warning})",
                entropy, WarningEntropy);
            EntropyAlert?.Invoke(this, new EntropyAlertEventArgs(check));
        }
        else
        {
            _logger.LogDebug("Entropy check passed: {Entropy:F3} bits/byte", entropy);
        }

        return check;
    }

    /// <summary>
    /// Get entropy history.
    /// </summary>
    public EntropyCheck[] GetHistory()
    {
        return _history.ToArray();
    }

    /// <summary>
    /// Get statistics about entropy checks.
    /// </summary>
    public EntropyStats GetStats()
    {
        var history = _history.ToArray();
        if (history.Length == 0)
        {
            return new EntropyStats();
        }

        return new EntropyStats
        {
            CheckCount = history.Length,
            AverageEntropy = history.Average(h => h.Entropy),
            MinEntropy = history.Min(h => h.Entropy),
            MaxEntropy = history.Max(h => h.Entropy),
            CriticalCount = history.Count(h => h.Status == EntropyStatus.Critical),
            WarningCount = history.Count(h => h.Status == EntropyStatus.Warning),
            LastCheck = LastCheck?.Timestamp,
        };
    }

    /// <summary>
    /// Test if the RNG appears to be working correctly.
    /// Performs multiple statistical tests.
    /// </summary>
    /// <returns>Test results.</returns>
    public RngHealthCheck TestRngHealth()
    {
        var issues = new List<string>();

        // Test 1: Generate multiple samples and check they're different
        var sample1 = RandomNumberGenerator.GetBytes(32);
        var sample2 = RandomNumberGenerator.GetBytes(32);

        if (sample1.SequenceEqual(sample2))
        {
            issues.Add("RNG produced identical samples - catastrophic failure!");
        }

        // Test 2: Check entropy
        var largeSample = RandomNumberGenerator.GetBytes(1024);
        var entropy = CalculateShannonEntropy(largeSample);

        if (entropy < MinAcceptableEntropy)
        {
            issues.Add($"Low entropy: {entropy:F3} bits/byte");
        }

        // Test 3: Check for obvious patterns (all zeros, all ones)
        if (largeSample.All(b => b == 0))
        {
            issues.Add("RNG produced all zeros!");
        }

        if (largeSample.All(b => b == 255))
        {
            issues.Add("RNG produced all 255s!");
        }

        // Test 4: Chi-square test for uniform distribution
        var chiSquare = CalculateChiSquare(largeSample);
        // For 255 degrees of freedom, critical value at p=0.01 is ~310
        // and at p=0.99 is ~198. Values outside this range suggest non-randomness.
        if (chiSquare < 180 || chiSquare > 330)
        {
            issues.Add($"Chi-square test failed: {chiSquare:F2} (expected ~255)");
        }

        // Test 5: Runs test (check for too many/few runs)
        var runs = CountRuns(largeSample);
        var expectedRuns = (largeSample.Length - 1) / 2.0; // Approximate
        if (runs < expectedRuns * 0.8 || runs > expectedRuns * 1.2)
        {
            issues.Add($"Runs test suspicious: {runs} runs (expected ~{expectedRuns:F0})");
        }

        return new RngHealthCheck
        {
            Timestamp = DateTimeOffset.UtcNow,
            IsHealthy = issues.Count == 0,
            Entropy = entropy,
            ChiSquare = chiSquare,
            Runs = runs,
            Issues = issues,
        };
    }

    private void PerformCheck(object? state)
    {
        try
        {
            CheckNow();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entropy check failed");
        }
    }

    /// <summary>
    /// Calculate Shannon entropy in bits per byte.
    /// </summary>
    private static double CalculateShannonEntropy(byte[] data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        var frequency = new int[256];
        foreach (var b in data)
        {
            frequency[b]++;
        }

        double entropy = 0;
        var len = (double)data.Length;

        foreach (var count in frequency)
        {
            if (count > 0)
            {
                var p = count / len;
                entropy -= p * Math.Log2(p);
            }
        }

        return entropy;
    }

    /// <summary>
    /// Analyze byte distribution.
    /// </summary>
    private static ByteDistribution AnalyzeDistribution(byte[] data)
    {
        var frequency = new int[256];
        foreach (var b in data)
        {
            frequency[b]++;
        }

        var expected = data.Length / 256.0;

        return new ByteDistribution
        {
            Frequencies = frequency,
            ExpectedFrequency = expected,
            MaxFrequency = frequency.Max(),
            MinFrequency = frequency.Min(),
        };
    }

    /// <summary>
    /// Calculate chi-square statistic.
    /// </summary>
    private static double CalculateChiSquare(byte[] data)
    {
        var frequency = new int[256];
        foreach (var b in data)
        {
            frequency[b]++;
        }

        var expected = data.Length / 256.0;
        double chiSquare = 0;

        foreach (var count in frequency)
        {
            var diff = count - expected;
            chiSquare += (diff * diff) / expected;
        }

        return chiSquare;
    }

    /// <summary>
    /// Count runs (sequences of consecutive increasing or decreasing values).
    /// </summary>
    private static int CountRuns(byte[] data)
    {
        if (data.Length < 2)
        {
            return 0;
        }

        var runs = 1;
        var increasing = data[1] > data[0];

        for (var i = 2; i < data.Length; i++)
        {
            var nowIncreasing = data[i] > data[i - 1];
            if (nowIncreasing != increasing)
            {
                runs++;
                increasing = nowIncreasing;
            }
        }

        return runs;
    }

    private static EntropyStatus GetStatus(double entropy)
    {
        if (entropy < MinAcceptableEntropy)
        {
            return EntropyStatus.Critical;
        }

        if (entropy < WarningEntropy)
        {
            return EntropyStatus.Warning;
        }

        return EntropyStatus.Healthy;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _checkTimer.Dispose();
    }
}

/// <summary>
/// Result of an entropy check.
/// </summary>
public sealed class EntropyCheck
{
    /// <summary>Gets when the check was performed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the measured entropy in bits per byte.</summary>
    public required double Entropy { get; init; }

    /// <summary>Gets the sample size used.</summary>
    public required int SampleSize { get; init; }

    /// <summary>Gets the byte distribution analysis.</summary>
    public required ByteDistribution ByteDistribution { get; init; }

    /// <summary>Gets the status.</summary>
    public required EntropyStatus Status { get; init; }
}

/// <summary>
/// Byte frequency distribution.
/// </summary>
public sealed class ByteDistribution
{
    /// <summary>Gets the frequency of each byte value.</summary>
    public required int[] Frequencies { get; init; }

    /// <summary>Gets the expected frequency per byte value.</summary>
    public required double ExpectedFrequency { get; init; }

    /// <summary>Gets the maximum frequency observed.</summary>
    public required int MaxFrequency { get; init; }

    /// <summary>Gets the minimum frequency observed.</summary>
    public required int MinFrequency { get; init; }
}

/// <summary>
/// Entropy status.
/// </summary>
public enum EntropyStatus
{
    /// <summary>Entropy is healthy.</summary>
    Healthy,

    /// <summary>Entropy is below optimal but acceptable.</summary>
    Warning,

    /// <summary>Entropy is critically low.</summary>
    Critical,
}

/// <summary>
/// Event args for entropy alerts.
/// </summary>
public sealed class EntropyAlertEventArgs : EventArgs
{
    /// <summary>Gets the check result.</summary>
    public EntropyCheck Check { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntropyAlertEventArgs"/> class.
    /// </summary>
    public EntropyAlertEventArgs(EntropyCheck check)
    {
        Check = check;
    }
}

/// <summary>
/// Statistics about entropy checks.
/// </summary>
public sealed class EntropyStats
{
    /// <summary>Gets the number of checks performed.</summary>
    public int CheckCount { get; init; }

    /// <summary>Gets the average entropy.</summary>
    public double AverageEntropy { get; init; }

    /// <summary>Gets the minimum entropy observed.</summary>
    public double MinEntropy { get; init; }

    /// <summary>Gets the maximum entropy observed.</summary>
    public double MaxEntropy { get; init; }

    /// <summary>Gets count of critical alerts.</summary>
    public int CriticalCount { get; init; }

    /// <summary>Gets count of warnings.</summary>
    public int WarningCount { get; init; }

    /// <summary>Gets when the last check was performed.</summary>
    public DateTimeOffset? LastCheck { get; init; }
}

/// <summary>
/// Result of RNG health check.
/// </summary>
public sealed class RngHealthCheck
{
    /// <summary>Gets when the check was performed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets whether the RNG appears healthy.</summary>
    public required bool IsHealthy { get; init; }

    /// <summary>Gets the measured entropy.</summary>
    public required double Entropy { get; init; }

    /// <summary>Gets the chi-square statistic.</summary>
    public required double ChiSquare { get; init; }

    /// <summary>Gets the number of runs detected.</summary>
    public required int Runs { get; init; }

    /// <summary>Gets any issues detected.</summary>
    public required List<string> Issues { get; init; }
}

