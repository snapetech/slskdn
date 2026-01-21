// <copyright file="MeshCircuitBuilderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class MeshCircuitBuilderTests : IDisposable
{
    private readonly Mock<ILogger<MeshCircuitBuilder>> _loggerMock;
    private readonly Mock<IMeshPeerManager> _peerManagerMock;
    private readonly Mock<IAnonymityTransportSelector> _transportSelectorMock;
    private readonly MeshOptions _defaultOptions;

    public MeshCircuitBuilderTests()
    {
        _loggerMock = new Mock<ILogger<MeshCircuitBuilder>>();
        _peerManagerMock = new Mock<IMeshPeerManager>();
        _transportSelectorMock = new Mock<IAnonymityTransportSelector>();

        _defaultOptions = new MeshOptions
        {
            SelfPeerId = "self-peer"
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act & Assert - Should not throw
        using var builder = new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object);
        Assert.NotNull(builder);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MeshCircuitBuilder(null!, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MeshCircuitBuilder(_defaultOptions, null!, _peerManagerMock.Object, _transportSelectorMock.Object));
    }

    [Fact]
    public void Constructor_WithNullPeerManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, null!, _transportSelectorMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTransportSelector_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, null!));
    }

    [Fact]
    public async Task BuildCircuitAsync_WithNullTargetPeerId_ThrowsArgumentException()
    {
        // Arrange
        using var builder = new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildCircuitAsync(null!));
        Assert.Contains("Target peer ID cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task BuildCircuitAsync_WithEmptyTargetPeerId_ThrowsArgumentException()
    {
        // Arrange
        using var builder = new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildCircuitAsync(string.Empty));
        Assert.Contains("Target peer ID cannot be null or empty", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task BuildCircuitAsync_WithInvalidCircuitLength_ThrowsArgumentException(int circuitLength)
    {
        // Arrange
        using var builder = new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildCircuitAsync("target-peer", circuitLength));
        Assert.Contains("Circuit length must be between 2 and 6 hops", exception.Message);
    }

    [Fact]
    public void GetCircuit_WithNonExistentCircuitId_ReturnsNull()
    {
        // Arrange
        using var builder = new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object);

        // Act
        var circuit = builder.GetCircuit("non-existent-id");

        // Assert
        Assert.Null(circuit);
    }

    [Fact]
    public void GetStatistics_WithNoCircuits_ReturnsEmptyStatistics()
    {
        // Arrange
        using var builder = new MeshCircuitBuilder(_defaultOptions, _loggerMock.Object, _peerManagerMock.Object, _transportSelectorMock.Object);

        // Act
        var stats = builder.GetStatistics();

        // Assert
        Assert.Equal(0, stats.ActiveCircuits);
        Assert.Equal(0, stats.TotalCircuitsBuilt);
        Assert.Equal(0, stats.AverageCircuitLength);
        Assert.Empty(stats.CircuitLengths);
    }
}
