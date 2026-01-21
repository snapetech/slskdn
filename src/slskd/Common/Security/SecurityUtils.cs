// <copyright file="SecurityUtils.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace slskd.Common.Security;

/// <summary>
/// Security utilities for constant-time operations and cryptographic functions.
/// </summary>
public static class SecurityUtils
{
    /// <summary>
    /// Performs a constant-time comparison of two byte arrays.
    /// This prevents timing attacks by ensuring the comparison takes the same amount of time
    /// regardless of how many bytes match or whether the arrays are equal.
    /// </summary>
    /// <param name="a">The first byte array to compare.</param>
    /// <param name="b">The second byte array to compare.</param>
    /// <returns>True if the arrays are equal, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        // Constant-time comparison prevents timing attacks
        if (a.Length != b.Length)
        {
            return false;
        }

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Performs a constant-time comparison of two strings.
    /// </summary>
    /// <param name="a">The first string to compare.</param>
    /// <param name="b">The second string to compare.</param>
    /// <returns>True if the strings are equal, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool ConstantTimeEquals(string? a, string? b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return ConstantTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }

    /// <summary>
    /// Performs a constant-time comparison of two strings with a specified encoding.
    /// </summary>
    /// <param name="a">The first string to compare.</param>
    /// <param name="b">The second string to compare.</param>
    /// <param name="encoding">The encoding to use for converting strings to bytes.</param>
    /// <returns>True if the strings are equal, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool ConstantTimeEquals(string? a, string? b, Encoding encoding)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return ConstantTimeEquals(
            encoding.GetBytes(a),
            encoding.GetBytes(b));
    }

    /// <summary>
    /// Generates a cryptographically secure random byte array.
    /// </summary>
    /// <param name="length">The length of the random byte array.</param>
    /// <returns>A cryptographically secure random byte array.</returns>
    public static byte[] GenerateSecureRandomBytes(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative");
        }

        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    /// <summary>
    /// Generates a cryptographically secure random string.
    /// </summary>
    /// <param name="length">The length of the random string.</param>
    /// <param name="allowSpecialChars">Whether to include special characters.</param>
    /// <returns>A cryptographically secure random string.</returns>
    public static string GenerateSecureRandomString(int length, bool allowSpecialChars = false)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative");
        }

        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        string charSet = allowSpecialChars ? chars + specialChars : chars;

        var bytes = GenerateSecureRandomBytes(length);
        var result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = charSet[bytes[i] % charSet.Length];
        }

        return new string(result);
    }

    /// <summary>
    /// Computes a constant-time hash of the input for comparison purposes.
    /// This can be used when you need to compare hashed values without revealing timing information.
    /// </summary>
    /// <param name="input">The input bytes to hash.</param>
    /// <param name="salt">Optional salt bytes.</param>
    /// <returns>The hashed bytes.</returns>
    public static byte[] ConstantTimeHash(ReadOnlySpan<byte> input, ReadOnlySpan<byte> salt = default)
    {
        using var sha256 = SHA256.Create();

        // Combine input and salt
        var combinedLength = input.Length + salt.Length;
        Span<byte> combined = stackalloc byte[combinedLength];
        input.CopyTo(combined);
        salt.CopyTo(combined[input.Length..]);

        return sha256.ComputeHash(combined.ToArray());
    }

    /// <summary>
    /// Verifies that a value matches an expected hash using constant-time comparison.
    /// </summary>
    /// <param name="value">The value to verify.</param>
    /// <param name="expectedHash">The expected hash bytes.</param>
    /// <param name="salt">Optional salt that was used in the original hash.</param>
    /// <returns>True if the value matches the expected hash.</returns>
    public static bool ConstantTimeVerifyHash(ReadOnlySpan<byte> value, ReadOnlySpan<byte> expectedHash, ReadOnlySpan<byte> salt = default)
    {
        var computedHash = ConstantTimeHash(value, salt);
        return ConstantTimeEquals(computedHash, expectedHash);
    }

    /// <summary>
    /// Clears sensitive data from memory to prevent it from being recovered.
    /// </summary>
    /// <param name="data">The data to clear.</param>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void SecureClear(Span<byte> data)
    {
        // Overwrite with zeros to clear sensitive data from memory
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 0;
        }
    }

    /// <summary>
    /// Clears sensitive data from memory to prevent it from being recovered.
    /// </summary>
    /// <param name="data">The data to clear.</param>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void SecureClear(char[] data)
    {
        // Overwrite with zeros to clear sensitive data from memory
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = '\0';
        }
    }

    /// <summary>
    /// Computes a double SHA-256 hash (used in some cryptographic protocols).
    /// </summary>
    /// <param name="input">The input bytes to hash.</param>
    /// <returns>The double-hashed bytes.</returns>
    public static byte[] DoubleSha256(ReadOnlySpan<byte> input)
    {
        using var sha256 = SHA256.Create();
        var firstHash = sha256.ComputeHash(input.ToArray());
        return sha256.ComputeHash(firstHash);
    }

    /// <summary>
    /// Performs a constant-time selection between two values based on a condition.
    /// This prevents branching that could be used in timing attacks.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="trueValue">The value to return if condition is true.</param>
    /// <param name="falseValue">The value to return if condition is false.</param>
    /// <returns>The selected value.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int ConstantTimeSelect(int condition, int trueValue, int falseValue)
    {
        // Convert condition to 0 or -1 (all bits set)
        int mask = condition - 1;

        // Select trueValue if condition is non-zero, falseValue if condition is zero
        return (trueValue & ~mask) | (falseValue & mask);
    }

    /// <summary>
    /// Performs a constant-time conditional move operation.
    /// </summary>
    /// <param name="condition">The condition (non-zero for true, zero for false).</param>
    /// <param name="source">The source value.</param>
    /// <param name="destination">The destination span to conditionally update.</param>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void ConstantTimeConditionalMove(int condition, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination must have the same length");
        }

        // Convert condition to mask (0 or all bits set)
        int mask = condition - 1;

        for (int i = 0; i < source.Length; i++)
        {
            // destination[i] = (source[i] & ~mask) | (destination[i] & mask)
            // This selects source[i] if condition is non-zero, destination[i] if condition is zero
            destination[i] = (byte)((source[i] & ~mask) | (destination[i] & mask));
        }
    }

    /// <summary>
    /// Generates a timing-safe random delay to prevent timing attacks.
    /// </summary>
    /// <param name="minMilliseconds">The minimum delay in milliseconds.</param>
    /// <param name="maxMilliseconds">The maximum delay in milliseconds.</param>
    /// <returns>A task that completes after the random delay.</returns>
    public static async Task RandomDelayAsync(int minMilliseconds = 10, int maxMilliseconds = 100)
    {
        if (minMilliseconds < 0 || maxMilliseconds < minMilliseconds)
        {
            throw new ArgumentException("Invalid delay range");
        }

        var delay = GenerateSecureRandomBytes(4);
        int randomValue = BitConverter.ToInt32(delay, 0) & int.MaxValue; // Make positive
        int range = maxMilliseconds - minMilliseconds;
        int actualDelay = minMilliseconds + (randomValue % (range + 1));

        await Task.Delay(actualDelay);
    }

    /// <summary>
    /// Checks if a timing attack might be possible by measuring operation time.
    /// This is primarily for testing and debugging purposes.
    /// </summary>
    /// <param name="operation">The operation to time.</param>
    /// <param name="iterations">Number of iterations to average.</param>
    /// <returns>The average time per operation in ticks.</returns>
    public static long MeasureTimingVariance(Action operation, int iterations = 1000)
    {
        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Must be at least 1");
        }

        var timings = new long[iterations];

        // Warm up
        operation();

        // Measure timing
        for (int i = 0; i < iterations; i++)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            operation();
            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            timings[i] = end - start;
        }

        // Calculate variance (simplified as max - min)
        long min = long.MaxValue;
        long max = long.MinValue;

        foreach (var timing in timings)
        {
            if (timing < min) min = timing;
            if (timing > max) max = timing;
        }

        return max - min; // Return variance range
    }
}


