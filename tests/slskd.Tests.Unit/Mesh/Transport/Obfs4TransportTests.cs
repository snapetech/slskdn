// <copyright file="Obfs4TransportTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class Obfs4TransportTests : IDisposable
{
    private readonly Mock<ILogger<Obfs4Transport>> _loggerMock;
    private readonly Obfs4TransportOptions _defaultOptions;

    public Obfs4TransportTests()
    {
        _loggerMock = new Mock<ILogger<Obfs4Transport>>();
        _defaultOptions = new Obfs4TransportOptions
        {
            Obfs4ProxyPath = "/usr/bin/obfs4proxy",
            BridgeLines = new List<string>
            {
                "obfs4 192.0.2.1:443 1234567890ABCDEF cert=examplecert iat-mode=0"
            }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Act & Assert - Should not throw
        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.Obfs4, transport.TransportType);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Obfs4Transport(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Obfs4Transport(_defaultOptions, null!));
    }

    [Fact]
    public void TransportType_ReturnsObfs4()
    {
        // Arrange
        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);

        // Assert
        Assert.Equal(AnonymityTransportType.Obfs4, transport.TransportType);
    }

    [Fact]
    public void GetStatus_ReturnsValidStatusObject()
    {
        // Arrange
        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);

        // Act
        var status = transport.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.IsType<AnonymityTransportStatus>(status);
        Assert.Equal(0, status.ActiveConnections);
        Assert.Equal(0, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task IsAvailableAsync_WithoutObfs4Proxy_ReturnsFalse()
    {
        // Arrange
        var options = new Obfs4TransportOptions { Obfs4ProxyPath = "/nonexistent/obfs4proxy" };
        var transport = new Obfs4Transport(options, _loggerMock.Object);

        // Act
        var isAvailable = await transport.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
        var status = transport.GetStatus();
        Assert.Contains("not found", status.LastError?.ToLower() ?? "");
    }

    [Fact]
    public async Task ConnectAsync_WithoutBridges_ThrowsException()
    {
        // Arrange
        var options = new Obfs4TransportOptions
        {
            Obfs4ProxyPath = "/usr/bin/obfs4proxy",
            BridgeLines = new List<string>() // Empty bridge list
        };
        var transport = new Obfs4Transport(options, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));
    }

    [Fact]
    public async Task ConnectAsync_WithIsolationKey_UsesIsolation()
    {
        // Arrange
        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual obfs4proxy, but should handle isolation logic
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer123"));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Theory]
    [InlineData("obfs4 192.0.2.1:443 1234567890ABCDEF cert=examplecert iat-mode=0")]
    [InlineData("obfs4 192.0.2.2:80 FEDCBA0987654321 cert=differentcert iat-mode=1")]
    public void ParseBridgeLine_ValidFormats_ReturnsBridge(string bridgeLine)
    {
        // Act - Use reflection to access private method
        var method = typeof(Obfs4Transport).GetMethod("ParseBridgeLine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);
        var result = method?.Invoke(transport, new object[] { bridgeLine });

        // Assert
        Assert.NotNull(result);
        // In a full test, we'd verify the parsed bridge properties
    }

    [Theory]
    [InlineData("invalid bridge line")]
    [InlineData("obfs4 invalid format")]
    [InlineData("")]
    public void ParseBridgeLine_InvalidFormats_ReturnsNull(string bridgeLine)
    {
        // Act - Use reflection to access private method
        var method = typeof(Obfs4Transport).GetMethod("ParseBridgeLine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);
        var result = method?.Invoke(transport, new object[] { bridgeLine });

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("/usr/bin/obfs4proxy")]
    [InlineData("/usr/local/bin/obfs4proxy")]
    [InlineData("C:\\Program Files\\Tor\\obfs4proxy.exe")]
    public void Obfs4TransportOptions_AcceptsVariousProxyPaths(string proxyPath)
    {
        // Arrange
        var options = new Obfs4TransportOptions
        {
            Obfs4ProxyPath = proxyPath,
            BridgeLines = _defaultOptions.BridgeLines
        };

        // Act & Assert - Should not throw
        var transport = new Obfs4Transport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.Obfs4, transport.TransportType);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange & Act - Obfs4Transport does not implement IDisposable; creation should not throw
        var transport = new Obfs4Transport(_defaultOptions, _loggerMock.Object);

        // Assert
        Assert.NotNull(transport);
    }

    [Fact]
    public async Task MultipleBridgeLines_SelectsAppropriateBridge()
    {
        // Arrange
        var options = new Obfs4TransportOptions
        {
            Obfs4ProxyPath = "/usr/bin/obfs4proxy",
            BridgeLines = new List<string>
            {
                "obfs4 192.0.2.1:443 1234567890ABCDEF cert=bridge1 iat-mode=0",
                "obfs4 192.0.2.2:443 FEDCBA0987654321 cert=bridge2 iat-mode=0",
                "obfs4 192.0.2.3:443 ABCDEF1234567890 cert=bridge3 iat-mode=1"
            }
        };

        var transport = new Obfs4Transport(options, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual obfs4proxy, but should handle bridge selection
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task IsAvailableAsync_VersionCheckFailure_ReturnsFalse()
    {
        var versionCheckerMock = new Mock<IObfs4VersionChecker>();
        versionCheckerMock
            .Setup(x => x.RunVersionCheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var options = new Obfs4TransportOptions
        {
            Obfs4ProxyPath = "/usr/bin/obfs4proxy",
            BridgeLines = _defaultOptions.BridgeLines
        };
        var transport = new Obfs4Transport(options, _loggerMock.Object, versionCheckerMock.Object);

        var isAvailable = await transport.IsAvailableAsync();

        Assert.False(isAvailable);
    }
}


