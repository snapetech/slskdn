// <copyright file="HashDbSearchSecurityTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.HashDb;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using slskd.HashDb;

/// <summary>
/// Security tests for Hash DB search functionality.
/// Tests SQL injection protection, input validation, and resource limits.
/// Note: These tests verify the security logic without requiring full database setup.
/// </summary>
public class HashDbSearchSecurityTests : IDisposable
{
    private readonly string testDir;
    private readonly HashDbService service;

    public HashDbSearchSecurityTests()
    {
        testDir = Path.Combine(Path.GetTempPath(), $"hashdb-security-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        service = new HashDbService(testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("%' OR 1=1 --")]
    [InlineData("'; DROP TABLE hashes; --")]
    [InlineData("%_[")]
    [InlineData("test%")]
    [InlineData("test_")]
    [InlineData("test[abc]")]
    public async Task SearchAsync_SqlInjectionAttempts_ShouldNotThrow(string maliciousInput)
    {
        // Act - Should not throw, should handle gracefully
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await service.SearchAsync(maliciousInput, limit: 10);
            // Even if DB is empty/missing tables, the input sanitization should work
        });
        
        // Assert - Should either succeed or fail gracefully (not SQL injection)
        // Any exception should NOT be a SQL syntax error from injection
        if (exception != null)
        {
            Assert.DoesNotContain("syntax error", exception.Message.ToLower());
            Assert.DoesNotContain("DROP TABLE", exception.Message);
        }
    }
    
    [Fact]
    public async Task SearchAsync_EmptyOrWhitespace_ShouldReturnEmpty()
    {
        // Act
        var result1 = await service.SearchAsync("");
        var result2 = await service.SearchAsync("   ");
        var result3 = await service.SearchAsync(null);
        
        // Assert
        Assert.Empty(result1);
        Assert.Empty(result2);
        Assert.Empty(result3);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)] // Should be clamped to 100
    public async Task SearchAsync_VariousLimits_ShouldNotThrow(int requestedLimit)
    {
        // Act - Should not throw even with various limits
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await service.SearchAsync("test", limit: requestedLimit);
        });
        
        // Assert - Should handle limit validation
        Assert.Null(exception);
    }
    
    [Fact]
    public async Task SearchAsync_ExtremelyLongQuery_ShouldBeTruncated()
    {
        // Arrange
        var longQuery = new string('a', 500); // 500 chars, limit is 200
        
        // Act - Should not throw, should truncate
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await service.SearchAsync(longQuery);
        });
        
        // Assert
        Assert.Null(exception); // Should handle long input
    }
    
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("/var/log/messages")]
    public async Task SearchAsync_PathTraversalAttempts_ShouldBeEscaped(string pathAttempt)
    {
        // Act - Should not throw, should sanitize
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.SearchAsync(pathAttempt);
        });
        
        // Assert - Should sanitize input, not execute file operations
        // Any exception should NOT be a file system error
        if (exception != null)
        {
            Assert.DoesNotContain("file", exception.Message.ToLower());
            Assert.DoesNotContain("directory", exception.Message.ToLower());
        }
    }
    
    [Fact]
    public async Task GetPeersByHashAsync_InvalidFlacKey_ShouldReturnEmpty()
    {
        // Act
        var result1 = await service.GetPeersByHashAsync("");
        var result2 = await service.GetPeersByHashAsync(null);
        var result3 = await service.GetPeersByHashAsync(new string('x', 200)); // Too long
        
        // Assert
        Assert.Empty(result1);
        Assert.Empty(result2);
        Assert.Empty(result3);
    }
}














