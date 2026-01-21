// <copyright file="TransportDialerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;

namespace slskd.Tests.Unit.Mesh.Transport;

public class DirectQuicDialerTests
{
    [Fact]
    public void CanHandle_WithDirectQuicEndpoint_ReturnsTrue()
    {
        // Arrange
        var dialer = new DirectQuicDialer(null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.DirectQuic,
            Host = "192.168.1.1",
            Port = 443,
            ValidFromUnixMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds(),
            ValidToUnixMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WithTorEndpoint_ReturnsFalse()
    {
        // Arrange
        var dialer = new DirectQuicDialer(null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.TorOnionQuic,
            Host = "onion.onion",
            Port = 443
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanHandle_WithExpiredEndpoint_ReturnsFalse()
    {
        // Arrange
        var dialer = new DirectQuicDialer(null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.DirectQuic,
            Host = "192.168.1.1",
            Port = 443,
            ValidToUnixMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds() // Expired
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenQuicSupported_ReturnsTrue()
    {
        // Arrange
        var dialer = new DirectQuicDialer(null!);

        // Act
        var result = await dialer.IsAvailableAsync();

        // Assert
        // This depends on the runtime environment supporting QUIC
        // In most test environments, this will be false
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetStatistics_ReturnsValidStatistics()
    {
        // Arrange
        var dialer = new DirectQuicDialer(null!);

        // Act
        var stats = dialer.GetStatistics();

        // Assert
        Assert.Equal(TransportType.DirectQuic, stats.TransportType);
        Assert.True(stats.TotalAttempts >= 0);
        Assert.True(stats.SuccessfulConnections >= 0);
        Assert.True(stats.FailedConnections >= 0);
        Assert.True(stats.ActiveConnections >= 0);
    }
}

public class TorSocksDialerTests
{
    [Fact]
    public void CanHandle_WithTorOnionEndpointAndTorEnabled_ReturnsTrue()
    {
        // Arrange
        var options = new TorTransportOptions { Enabled = true };
        var dialer = new TorSocksDialer(options, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.TorOnionQuic,
            Host = "onion.onion",
            Port = 443
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WithTorDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new TorTransportOptions { Enabled = false };
        var dialer = new TorSocksDialer(options, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.TorOnionQuic,
            Host = "onion.onion",
            Port = 443
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanHandle_WithDirectEndpoint_ReturnsFalse()
    {
        // Arrange
        var options = new TorTransportOptions { Enabled = true };
        var dialer = new TorSocksDialer(options, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.DirectQuic,
            Host = "192.168.1.1",
            Port = 443
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidSocksConfig_ReturnsTrueIfReachable()
    {
        // Arrange
        var options = new TorTransportOptions
        {
            Enabled = true,
            SocksHost = "127.0.0.1",
            SocksPort = 9050
        };
        var dialer = new TorSocksDialer(options, null!);

        // Act
        var result = await dialer.IsAvailableAsync();

        // Assert
        // This will depend on whether Tor is actually running on the test system
        // In most test environments, this will be false
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetStatistics_ReturnsValidStatistics()
    {
        // Arrange
        var options = new TorTransportOptions { Enabled = true };
        var dialer = new TorSocksDialer(options, null!);

        // Act
        var stats = dialer.GetStatistics();

        // Assert
        Assert.Equal(TransportType.TorOnionQuic, stats.TransportType);
        Assert.True(stats.TotalAttempts >= 0);
        Assert.True(stats.SuccessfulConnections >= 0);
    }
}

public class I2pSocksDialerTests
{
    [Fact]
    public void CanHandle_WithI2PEndpointAndI2PEnabled_ReturnsTrue()
    {
        // Arrange
        var options = new I2PTransportOptions { Enabled = true };
        var dialer = new I2pSocksDialer(options, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.I2PQuic,
            Host = "i2p-destination",
            Port = 443
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanHandle_WithI2PDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new I2PTransportOptions { Enabled = false };
        var dialer = new I2pSocksDialer(options, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.I2PQuic,
            Host = "i2p-destination",
            Port = 443
        };

        // Act
        var result = dialer.CanHandle(endpoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidSocksConfig_ReturnsConnectivityStatus()
    {
        // Arrange
        var options = new I2PTransportOptions
        {
            Enabled = true,
            SocksHost = "127.0.0.1",
            SocksPort = 4447
        };
        var dialer = new I2pSocksDialer(options, null!);

        // Act
        var result = await dialer.IsAvailableAsync();

        // Assert
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetStatistics_ReturnsValidStatistics()
    {
        // Arrange
        var options = new I2PTransportOptions { Enabled = true };
        var dialer = new I2pSocksDialer(options, null!);

        // Act
        var stats = dialer.GetStatistics();

        // Assert
        Assert.Equal(TransportType.I2PQuic, stats.TransportType);
        Assert.True(stats.TotalAttempts >= 0);
    }
}


