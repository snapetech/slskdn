// <copyright file="MeshDataPlaneSecurityTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using slskd.Mesh;
using slskd.Mesh.Identity;
using slskd.DhtRendezvous;

/// <summary>
/// Security tests for mesh data plane.
/// Tests timeout protection, input validation, and error handling.
/// </summary>
public class MeshDataPlaneSecurityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(1024 * 1024 + 1)] // Over 1MB limit
    [InlineData(int.MaxValue)]
    public async Task DownloadChunkAsync_InvalidLength_ShouldThrow(int invalidLength)
    {
        // Arrange
        var dataPlane = CreateMockDataPlane();
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, invalidLength);
        });
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(long.MinValue)]
    public async Task DownloadChunkAsync_InvalidOffset_ShouldThrow(long invalidOffset)
    {
        // Arrange
        var dataPlane = CreateMockDataPlane();
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", invalidOffset, 1024);
        });
    }
    
    [Fact]
    public async Task DownloadChunkAsync_NoConnection_ShouldThrow()
    {
        // Arrange
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        neighborRegistry
            .Setup(r => r.GetConnectionByMeshPeerId(It.IsAny<string>()))
            .Returns((MeshOverlayConnection)null);
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        var dataPlane = new MeshDataPlane(logger, neighborRegistry.Object);
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        });
        
        Assert.Contains("No active connection", ex.Message);
        Assert.Contains("Available connections", ex.Message); // Should include context
    }
    
    [Fact]
    public async Task DownloadChunkAsync_Timeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var mockConnection = new Mock<MeshOverlayConnection>();
        mockConnection.Setup(c => c.IsConnected).Returns(true);
        mockConnection
            .Setup(c => c.WriteMessageAsync(It.IsAny<MeshChunkRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(async (MeshChunkRequestMessage msg, CancellationToken ct) =>
            {
                // Simulate slow response that times out
                await Task.Delay(35000, ct); // Longer than 30s timeout
            });
        
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        neighborRegistry
            .Setup(r => r.GetConnectionByMeshPeerId(It.IsAny<string>()))
            .Returns(mockConnection.Object);
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        var dataPlane = new MeshDataPlane(logger, neighborRegistry.Object);
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        });
    }
    
    [Fact]
    public async Task DownloadChunkAsync_NullResponse_ShouldThrowIOException()
    {
        // Arrange
        var mockConnection = new Mock<MeshOverlayConnection>();
        mockConnection.Setup(c => c.IsConnected).Returns(true);
        mockConnection
            .Setup(c => c.WriteMessageAsync(It.IsAny<MeshChunkRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockConnection
            .Setup(c => c.ReadMessageAsync<MeshChunkResponseMessage>(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeshChunkResponseMessage)null);
        
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        neighborRegistry
            .Setup(r => r.GetConnectionByMeshPeerId(It.IsAny<string>()))
            .Returns(mockConnection.Object);
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        var dataPlane = new MeshDataPlane(logger, neighborRegistry.Object);
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<System.IO.IOException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        });
        
        Assert.Contains("null response", ex.Message);
    }
    
    [Fact]
    public async Task DownloadChunkAsync_FailedResponse_ShouldThrowIOException()
    {
        // Arrange
        var mockConnection = new Mock<MeshOverlayConnection>();
        mockConnection.Setup(c => c.IsConnected).Returns(true);
        mockConnection
            .Setup(c => c.WriteMessageAsync(It.IsAny<MeshChunkRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockConnection
            .Setup(c => c.ReadMessageAsync<MeshChunkResponseMessage>(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeshChunkResponseMessage
            {
                RequestId = "test",
                Success = false,
                Error = "File not found",
            });
        
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        neighborRegistry
            .Setup(r => r.GetConnectionByMeshPeerId(It.IsAny<string>()))
            .Returns(mockConnection.Object);
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        var dataPlane = new MeshDataPlane(logger, neighborRegistry.Object);
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<System.IO.IOException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        });
        
        Assert.Contains("File not found", ex.Message);
    }
    
    [Fact]
    public async Task DownloadChunkAsync_WrongDataLength_ShouldThrowIOException()
    {
        // Arrange
        var mockConnection = new Mock<MeshOverlayConnection>();
        mockConnection.Setup(c => c.IsConnected).Returns(true);
        mockConnection
            .Setup(c => c.WriteMessageAsync(It.IsAny<MeshChunkRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockConnection
            .Setup(c => c.ReadMessageAsync<MeshChunkResponseMessage>(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeshChunkResponseMessage
            {
                RequestId = "test",
                Success = true,
                Data = new byte[512], // Expected 1024
            });
        
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        neighborRegistry
            .Setup(r => r.GetConnectionByMeshPeerId(It.IsAny<string>()))
            .Returns(mockConnection.Object);
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        var dataPlane = new MeshDataPlane(logger, neighborRegistry.Object);
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<System.IO.IOException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        });
        
        Assert.Contains("512 bytes", ex.Message);
        Assert.Contains("expected 1024", ex.Message);
    }
    
    [Fact]
    public async Task DownloadChunkAsync_ValidRequest_ShouldSucceed()
    {
        // Arrange
        var expectedData = new byte[1024];
        new Random().NextBytes(expectedData);
        
        var mockConnection = new Mock<MeshOverlayConnection>();
        mockConnection.Setup(c => c.IsConnected).Returns(true);
        mockConnection
            .Setup(c => c.WriteMessageAsync(It.IsAny<MeshChunkRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockConnection
            .Setup(c => c.ReadMessageAsync<MeshChunkResponseMessage>(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeshChunkResponseMessage
            {
                RequestId = "test",
                Success = true,
                Data = expectedData,
            });
        
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        neighborRegistry
            .Setup(r => r.GetConnectionByMeshPeerId(It.IsAny<string>()))
            .Returns(mockConnection.Object);
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        var dataPlane = new MeshDataPlane(logger, neighborRegistry.Object);
        var peerId = CreateTestPeerId();
        
        // Act
        var result = await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(1024, result.Length);
        Assert.Equal(expectedData, result);
    }
    
    private MeshDataPlane CreateMockDataPlane()
    {
        var neighborRegistry = new Mock<MeshNeighborRegistry>(
            Mock.Of<ILogger<MeshNeighborRegistry>>());
        
        var logger = Mock.Of<ILogger<MeshDataPlane>>();
        return new MeshDataPlane(logger, neighborRegistry.Object);
    }
    
    private MeshPeerId CreateTestPeerId()
    {
        var testKey = new byte[32];
        new Random().NextBytes(testKey);
        return MeshPeerId.FromPublicKey(testKey);
    }
}
