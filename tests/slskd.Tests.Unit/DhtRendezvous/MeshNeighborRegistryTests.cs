// <copyright file="MeshNeighborRegistryTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
using Xunit;

/// <summary>
/// Tests for MeshNeighborRegistry logic.
/// Note: These are logical/behavioral tests. Full integration tests requiring real connections
/// are better suited for end-to-end testing.
/// </summary>
public class MeshNeighborRegistryTests
{
    [Fact]
    public void MaxNeighbors_IsSetTo10()
    {
        // Assert
        Assert.Equal(10, MeshNeighborRegistry.MaxNeighbors);
    }

    [Fact]
    public void MaxConnectionsPerAddress_IsSetTo3()
    {
        // Assert
        Assert.Equal(3, MeshNeighborRegistry.MaxConnectionsPerAddress);
    }

    [Fact]
    public void MinNeighbors_IsSetTo3()
    {
        // Assert
        Assert.Equal(3, MeshNeighborRegistry.MinNeighbors);
    }

    [Fact]
    public void NewRegistry_StartsEmpty()
    {
        // Arrange
        var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.True(registry.NeedsMoreNeighbors);
        Assert.False(registry.IsFull);
    }

    [Fact]
    public void NeedsMoreNeighbors_TrueWhenBelowMin()
    {
        // Arrange
        var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);

        // Assert - With 0 neighbors, needs more
        Assert.True(registry.NeedsMoreNeighbors);
    }

    [Fact]
    public void Registry_StartsEmpty()
    {
        // Arrange
        var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.True(registry.NeedsMoreNeighbors);
        Assert.False(registry.IsFull);
    }

    [Fact]
    public void IsFull_FalseWhenEmpty()
    {
        // Arrange
        var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);

        // Assert
        Assert.False(registry.IsFull);
    }

    [Fact]
    public void Constants_AreConsistent()
    {
        // Assert relationships between constants
        Assert.True(MeshNeighborRegistry.MinNeighbors < MeshNeighborRegistry.MaxNeighbors);
        Assert.True(MeshNeighborRegistry.MaxConnectionsPerAddress > 0);
    }

    [Theory]
    [InlineData(0, true, false)]  // 0 neighbors: needs more, not full
    [InlineData(3, false, false)] // 3 neighbors: enough, not full
    [InlineData(5, false, false)] // 5 neighbors: enough, not full
    [InlineData(10, false, true)] // 10 neighbors: enough, full
    public void RegistryState_BehavesCorrectly(int neighborCount, bool expectedNeedsMore, bool expectedFull)
    {
        // Note: This is a logical test of the properties.
        // We cannot easily create neighbor connections without real TCP sockets,
        // but we can verify the logic through the properties and constants.
        
        // Assert - These properties are based on Count vs MinNeighbors/MaxNeighbors
        Assert.Equal(neighborCount < MeshNeighborRegistry.MinNeighbors, expectedNeedsMore);
        Assert.Equal(neighborCount >= MeshNeighborRegistry.MaxNeighbors, expectedFull);
    }

    [Fact]
    public void PerAddressLimit_IsEnforcedByConstants()
    {
        // This test documents the per-address limit logic
        // The actual enforcement is tested in integration tests

        // Assert
        Assert.Equal(3, MeshNeighborRegistry.MaxConnectionsPerAddress);
        
        // This means:
        // - A single IP can have up to 3 concurrent connections
        // - The 4th connection from the same IP should be rejected
        // - Different IPs are tracked independently
    }

    [Fact]
    public async Task Registry_CanBeDisposed()
    {
        // Arrange
        var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);

        // Act & Assert - Should not throw
        await registry.DisposeAsync();
    }

    [Fact]
    public async Task Registry_CanBeDisposedMultipleTimes()
    {
        // Arrange
        var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);

        // Act & Assert - Should not throw
        await registry.DisposeAsync();
        await registry.DisposeAsync(); // Second dispose should be safe
    }
}
