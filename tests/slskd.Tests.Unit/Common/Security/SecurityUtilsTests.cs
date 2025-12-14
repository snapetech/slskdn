// <copyright file="SecurityUtilsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.Common.Security;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.Common.Security;

public class SecurityUtilsTests
{
    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, true)]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }, false)]
    [InlineData(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }, false)]
    [InlineData(new byte[] { }, new byte[] { }, true)]
    [InlineData(new byte[] { 1 }, new byte[] { }, false)]
    public void ConstantTimeEquals_Bytes_ReturnsCorrectResult(byte[] a, byte[] b, bool expected)
    {
        // Act
        var result = SecurityUtils.ConstantTimeEquals(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hello", "hello", true)]
    [InlineData("hello", "world", false)]
    [InlineData("hello", "hell", false)]
    [InlineData("", "", true)]
    [InlineData("test", "", false)]
    [InlineData(null, null, true)]
    [InlineData("test", null, false)]
    [InlineData(null, "test", false)]
    public void ConstantTimeEquals_Strings_ReturnsCorrectResult(string? a, string? b, bool expected)
    {
        // Act
        var result = SecurityUtils.ConstantTimeEquals(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConstantTimeEquals_UnicodeStrings_HandlesEncodingCorrectly()
    {
        // Arrange
        var a = "héllo";
        var b = "héllo";
        var c = "hello";

        // Act & Assert
        Assert.True(SecurityUtils.ConstantTimeEquals(a, b));
        Assert.False(SecurityUtils.ConstantTimeEquals(a, c));
    }

    [Fact]
    public void ConstantTimeEquals_TimingAttackResistance()
    {
        // Arrange - Test that timing is consistent regardless of match position
        var baseString = "abcdefghijklmnopqrstuvwxy";
        var timingResults = new long[26];

        // Measure timing for strings that match at different positions
        for (int i = 1; i <= 26; i++)
        {
            var testString = baseString.Substring(0, i);
            timingResults[i - 1] = SecurityUtils.MeasureTimingVariance(() =>
                SecurityUtils.ConstantTimeEquals(baseString, testString), 100);
        }

        // The timing variance should be minimal (within reasonable bounds)
        // This is a statistical test - in practice, constant-time operations
        // should have very low variance compared to regular string comparison
        var averageVariance = timingResults.Average();
        var maxVariance = timingResults.Max();
        var minVariance = timingResults.Min();

        // Assert that variance is reasonably low (this is a heuristic test)
        // In a real security audit, this would be tested more rigorously
        Assert.True(maxVariance - minVariance < averageVariance * 2,
            $"Timing variance too high: min={minVariance}, max={maxVariance}, avg={averageVariance}");
    }

    [Fact]
    public void GenerateSecureRandomBytes_ValidLength_ReturnsCorrectSize()
    {
        // Arrange
        var length = 32;

        // Act
        var result = SecurityUtils.GenerateSecureRandomBytes(length);

        // Assert
        Assert.Equal(length, result.Length);
        Assert.NotEqual(new byte[length], result); // Should not be all zeros
    }

    [Fact]
    public void GenerateSecureRandomBytes_ZeroLength_ReturnsEmptyArray()
    {
        // Act
        var result = SecurityUtils.GenerateSecureRandomBytes(0);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GenerateSecureRandomBytes_NegativeLength_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SecurityUtils.GenerateSecureRandomBytes(-1));
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(16, true)]
    [InlineData(32, false)]
    public void GenerateSecureRandomString_ValidParameters_ReturnsCorrectLength(int length, bool allowSpecial)
    {
        // Act
        var result = SecurityUtils.GenerateSecureRandomString(length, allowSpecial);

        // Assert
        Assert.Equal(length, result.Length);

        if (allowSpecial)
        {
            // Should contain only valid characters including special chars
            Assert.All(result, c =>
                char.IsLetterOrDigit(c) || "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c));
        }
        else
        {
            // Should contain only alphanumeric characters
            Assert.All(result, char.IsLetterOrDigit);
        }
    }

    [Fact]
    public void GenerateSecureRandomString_ZeroLength_ReturnsEmptyString()
    {
        // Act
        var result = SecurityUtils.GenerateSecureRandomString(0);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GenerateSecureRandomString_NegativeLength_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SecurityUtils.GenerateSecureRandomString(-1));
    }

    [Fact]
    public void ConstantTimeHash_ValidInput_ReturnsConsistentHash()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("test input");
        var salt = Encoding.UTF8.GetBytes("salt");

        // Act
        var hash1 = SecurityUtils.ConstantTimeHash(input, salt);
        var hash2 = SecurityUtils.ConstantTimeHash(input, salt);

        // Assert
        Assert.Equal(hash1, hash2); // Same input should produce same hash
        Assert.Equal(32, hash1.Length); // SHA256 produces 32 bytes
    }

    [Fact]
    public void ConstantTimeHash_DifferentInputs_ProduceDifferentHashes()
    {
        // Arrange
        var input1 = Encoding.UTF8.GetBytes("input1");
        var input2 = Encoding.UTF8.GetBytes("input2");

        // Act
        var hash1 = SecurityUtils.ConstantTimeHash(input1);
        var hash2 = SecurityUtils.ConstantTimeHash(input2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ConstantTimeHash_SaltChangesHash()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("test input");
        var salt1 = Encoding.UTF8.GetBytes("salt1");
        var salt2 = Encoding.UTF8.GetBytes("salt2");

        // Act
        var hash1 = SecurityUtils.ConstantTimeHash(input, salt1);
        var hash2 = SecurityUtils.ConstantTimeHash(input, salt2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ConstantTimeVerifyHash_ValidHash_ReturnsTrue()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("test input");
        var salt = Encoding.UTF8.GetBytes("salt");
        var hash = SecurityUtils.ConstantTimeHash(input, salt);

        // Act
        var result = SecurityUtils.ConstantTimeVerifyHash(input, hash, salt);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ConstantTimeVerifyHash_InvalidHash_ReturnsFalse()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("test input");
        var salt = Encoding.UTF8.GetBytes("salt");
        var wrongHash = SecurityUtils.ConstantTimeHash(Encoding.UTF8.GetBytes("wrong input"), salt);

        // Act
        var result = SecurityUtils.ConstantTimeVerifyHash(input, wrongHash, salt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ConstantTimeVerifyHash_WrongSalt_ReturnsFalse()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("test input");
        var correctSalt = Encoding.UTF8.GetBytes("salt");
        var wrongSalt = Encoding.UTF8.GetBytes("wrong salt");
        var hash = SecurityUtils.ConstantTimeHash(input, correctSalt);

        // Act
        var result = SecurityUtils.ConstantTimeVerifyHash(input, hash, wrongSalt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SecureClear_Bytes_ClearsData()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var originalData = (byte[])data.Clone();

        // Act
        SecurityUtils.SecureClear(data);

        // Assert
        Assert.All(data, b => Assert.Equal(0, b));
        Assert.NotEqual(originalData, data);
    }

    [Fact]
    public void SecureClear_Chars_ClearsData()
    {
        // Arrange
        var data = "secret".ToCharArray();
        var originalData = (char[])data.Clone();

        // Act
        SecurityUtils.SecureClear(data);

        // Assert
        Assert.All(data, c => Assert.Equal('\0', c));
        Assert.NotEqual(originalData, data);
    }

    [Fact]
    public void DoubleSha256_ValidInput_ReturnsCorrectHash()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("test");

        // Act
        var result = SecurityUtils.DoubleSha256(input);

        // Assert
        Assert.Equal(32, result.Length); // SHA256 produces 32 bytes

        // Verify it's actually a double hash by comparing with single hash
        using var sha256 = SHA256.Create();
        var singleHash = sha256.ComputeHash(input);
        Assert.NotEqual(singleHash, result);
    }

    [Theory]
    [InlineData(0, 10, 20)] // false condition
    [InlineData(1, 10, 20)] // true condition
    [InlineData(-1, 10, 20)] // true condition (non-zero)
    public void ConstantTimeSelect_ValidConditions_ReturnsCorrectValue(int condition, int trueValue, int falseValue)
    {
        // Act
        var result = SecurityUtils.ConstantTimeSelect(condition, trueValue, falseValue);

        // Assert
        var expected = condition != 0 ? trueValue : falseValue;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConstantTimeConditionalMove_ValidInputs_UpdatesDestination()
    {
        // Arrange
        var source = new byte[] { 1, 2, 3, 4 };
        var destination = new byte[] { 5, 6, 7, 8 };

        // Act - condition = 1 (true), should copy source to destination
        SecurityUtils.ConstantTimeConditionalMove(1, source, destination);

        // Assert
        Assert.Equal(source, destination);
    }

    [Fact]
    public void ConstantTimeConditionalMove_FalseCondition_KeepsDestination()
    {
        // Arrange
        var source = new byte[] { 1, 2, 3, 4 };
        var destination = new byte[] { 5, 6, 7, 8 };
        var originalDestination = (byte[])destination.Clone();

        // Act - condition = 0 (false), should keep destination unchanged
        SecurityUtils.ConstantTimeConditionalMove(0, source, destination);

        // Assert
        Assert.Equal(originalDestination, destination);
        Assert.NotEqual(source, destination);
    }

    [Fact]
    public void ConstantTimeConditionalMove_DifferentLengths_ThrowsException()
    {
        // Arrange
        var source = new byte[] { 1, 2, 3 };
        var destination = new byte[] { 5, 6, 7, 8 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SecurityUtils.ConstantTimeConditionalMove(1, source, destination));
    }

    [Fact]
    public async Task RandomDelayAsync_ValidRange_CompletesWithinExpectedTime()
    {
        // Arrange
        var minDelay = 10;
        var maxDelay = 50;
        var startTime = DateTimeOffset.UtcNow;

        // Act
        await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
        var endTime = DateTimeOffset.UtcNow;

        // Assert
        var actualDelay = (endTime - startTime).TotalMilliseconds;
        Assert.True(actualDelay >= minDelay - 5, $"Delay too short: {actualDelay}ms"); // Allow some tolerance
        Assert.True(actualDelay <= maxDelay + 20, $"Delay too long: {actualDelay}ms"); // Allow some tolerance
    }

    [Fact]
    public async Task RandomDelayAsync_InvalidRange_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            SecurityUtils.RandomDelayAsync(50, 10)); // min > max

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SecurityUtils.RandomDelayAsync(-1, 10)); // negative min
    }

    [Fact]
    public void MeasureTimingVariance_ValidOperation_ReturnsVariance()
    {
        // Arrange
        var operation = () => { /* Simple operation */ };

        // Act
        var variance = SecurityUtils.MeasureTimingVariance(operation, 10);

        // Assert
        Assert.True(variance >= 0);
        // Variance should be relatively small for a simple operation
        Assert.True(variance < 1000000); // Reasonable upper bound
    }

    [Fact]
    public void MeasureTimingVariance_InvalidIterations_ThrowsException()
    {
        // Arrange
        var operation = () => { /* Simple operation */ };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SecurityUtils.MeasureTimingVariance(operation, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SecurityUtils.MeasureTimingVariance(operation, -1));
    }

    [Fact]
    public void ConstantTimeEquals_LargeArrays_PerformsConstantTime()
    {
        // Arrange - Test with larger arrays to ensure constant-time behavior
        var size = 1000;
        var a = SecurityUtils.GenerateSecureRandomBytes(size);
        var b = (byte[])a.Clone();

        // Make one byte different
        if (b.Length > 0) b[b.Length - 1] ^= 0xFF;

        // Act - Both should take roughly the same time
        var timingEqual = SecurityUtils.MeasureTimingVariance(() =>
            SecurityUtils.ConstantTimeEquals(a, a), 100);

        var timingUnequal = SecurityUtils.MeasureTimingVariance(() =>
            SecurityUtils.ConstantTimeEquals(a, b), 100);

        // Assert - Timing should be similar (within reasonable bounds)
        // This is a statistical test and may occasionally fail due to system noise
        var ratio = (double)timingUnequal / timingEqual;
        Assert.True(ratio < 2.0, $"Timing ratio too high: {ratio} (equal: {timingEqual}, unequal: {timingUnequal})");
    }

    [Fact]
    public void ConstantTimeOperations_AreMarkedWithNoInlining()
    {
        // This test ensures that our security-critical methods are properly marked
        // to prevent optimization that could introduce timing vulnerabilities

        // Arrange - Get method info for constant-time operations
        var constantTimeEqualsMethod = typeof(SecurityUtils).GetMethod("ConstantTimeEquals", new[] { typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>) });
        var secureClearMethod = typeof(SecurityUtils).GetMethod("SecureClear", new[] { typeof(Span<byte>) });

        // Act & Assert
        Assert.NotNull(constantTimeEqualsMethod);
        Assert.NotNull(secureClearMethod);

        // Check for NoInlining and NoOptimization attributes
        var constantTimeEqualsAttrs = constantTimeEqualsMethod.GetCustomAttributes(typeof(MethodImplAttribute), false);
        var secureClearAttrs = secureClearMethod.GetCustomAttributes(typeof(MethodImplAttribute), false);

        Assert.NotEmpty(constantTimeEqualsAttrs);
        Assert.NotEmpty(secureClearAttrs);

        // Verify the attributes include NoInlining
        var constantTimeEqualsAttr = (MethodImplAttribute)constantTimeEqualsAttrs[0];
        var secureClearAttr = (MethodImplAttribute)secureClearAttrs[0];

        Assert.True((constantTimeEqualsAttr.Value & MethodImplOptions.NoInlining) != 0);
        Assert.True((secureClearAttr.Value & MethodImplOptions.NoInlining) != 0);
    }
}

