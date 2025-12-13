// <copyright file="MeshDataPlaneSecurityTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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
        var neighborRegistry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var dataPlane = new MeshDataPlane(NullLogger<MeshDataPlane>.Instance, neighborRegistry);
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
        var neighborRegistry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var dataPlane = new MeshDataPlane(NullLogger<MeshDataPlane>.Instance, neighborRegistry);
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
        var neighborRegistry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var dataPlane = new MeshDataPlane(NullLogger<MeshDataPlane>.Instance, neighborRegistry);
        var peerId = CreateTestPeerId();
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await dataPlane.DownloadChunkAsync(peerId, "test.txt", 0, 1024);
        });
        
        Assert.Contains("No active connection", ex.Message);
    }
    
    private MeshPeerId CreateTestPeerId()
    {
        var testKey = new byte[32];
        new Random().NextBytes(testKey);
        return MeshPeerId.FromPublicKey(testKey);
    }
}














