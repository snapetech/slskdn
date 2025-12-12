// <copyright file="MeshChunkRequestHandlerTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using slskd.Mesh;
using slskd.Mesh.Identity;

/// <summary>
/// Security tests for mesh chunk request handler.
/// Tests path traversal protection, rate limiting, and authorization.
/// </summary>
public class MeshChunkRequestHandlerTests : IDisposable
{
    private readonly string _testShareDir;
    private readonly MeshChunkRequestHandler _handler;
    
    public MeshChunkRequestHandlerTests()
    {
        _testShareDir = Path.Combine(Path.GetTempPath(), $"slskdn-test-share-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testShareDir);
        _handler = new MeshChunkRequestHandler(NullLogger<MeshChunkRequestHandler>.Instance, _testShareDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testShareDir))
        {
            Directory.Delete(_testShareDir, recursive: true);
        }
    }
    
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\sam")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config\\sam")]
    [InlineData("../secret.txt")]
    [InlineData("folder/../../../secret.txt")]
    public async Task HandleRequestAsync_PathTraversal_ShouldBeRejected(string maliciousPath)
    {
        // Arrange
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = maliciousPath,
            Offset = 0,
            Length = 1024,
        };
        var peerId = CreateTestPeerId();
        
        // Act
        var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
        
        // Assert
        Assert.False(response.Success);
        Assert.Contains("path", response.Error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task HandleRequestAsync_NullCharacterInFilename_ShouldBeRejected()
    {
        // Arrange - Null character is invalid on all systems
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = "test\0file.txt",
            Offset = 0,
            Length = 1024,
        };
        var peerId = CreateTestPeerId();
        
        // Act
        var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
        
        // Assert
        Assert.False(response.Success);
        Assert.Contains("invalid", response.Error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Theory]
    [InlineData(-1, 1024)]
    [InlineData(0, -1)]
    [InlineData(0, 0)]
    [InlineData(0, 1024 * 1024 + 1)] // Over 1MB limit
    [InlineData(-100, -100)]
    public async Task HandleRequestAsync_InvalidRange_ShouldBeRejected(long offset, int length)
    {
        // Arrange
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = "test.txt",
            Offset = offset,
            Length = length,
        };
        var peerId = CreateTestPeerId();
        
        // Act
        var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
        
        // Assert
        Assert.False(response.Success);
        Assert.Contains("invalid", response.Error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task HandleRequestAsync_RateLimitExceeded_ShouldBeRejected()
    {
        // Arrange
        var peerId = CreateTestPeerId();
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = "test.txt",
            Offset = 0,
            Length = 1024,
        };
        
        // Act - Send 70 requests (limit is 60/min)
        var responses = new System.Collections.Generic.List<MeshChunkResponseMessage>();
        for (int i = 0; i < 70; i++)
        {
            var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
            responses.Add(response);
        }
        
        // Assert - Should have at least 10 rejections due to rate limit
        var rejectedCount = responses.Where(r => !r.Success && r.Error?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true).Count();
        Assert.True(rejectedCount >= 10, $"Expected at least 10 requests to be rate-limited, but only {rejectedCount} were rejected");
    }
    
    [Fact]
    public async Task HandleRequestAsync_FileNotFound_ShouldReturnError()
    {
        // Arrange
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = "nonexistent.txt",
            Offset = 0,
            Length = 1024,
        };
        var peerId = CreateTestPeerId();
        
        // Act
        var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
        
        // Assert
        Assert.False(response.Success);
        Assert.Contains("not found", response.Error, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task HandleRequestAsync_ValidRequest_ShouldSucceed()
    {
        // Arrange
        var testFile = Path.Combine(_testShareDir, "test.txt");
        var testContent = "Hello, World! This is test data.";
        File.WriteAllText(testFile, testContent);
        
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = "test.txt",
            Offset = 0,
            Length = 13, // "Hello, World!"
        };
        var peerId = CreateTestPeerId();
        
        // Act
        var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
        
        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(13, response.Data.Length);
        Assert.Equal("Hello, World!", System.Text.Encoding.UTF8.GetString(response.Data));
    }
    
    [Fact]
    public async Task HandleRequestAsync_EmptyFilename_ShouldBeRejected()
    {
        // Arrange
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = "",
            Offset = 0,
            Length = 1024,
        };
        var peerId = CreateTestPeerId();
        
        // Act
        var response = await _handler.HandleRequestAsync(request, peerId, CancellationToken.None);
        
        // Assert
        Assert.False(response.Success);
        Assert.Contains("required", response.Error, StringComparison.OrdinalIgnoreCase);
    }
    
    private MeshPeerId CreateTestPeerId()
    {
        // Create a test peer ID
        var testKey = new byte[32];
        new Random().NextBytes(testKey);
        return MeshPeerId.FromPublicKey(testKey);
    }
}
