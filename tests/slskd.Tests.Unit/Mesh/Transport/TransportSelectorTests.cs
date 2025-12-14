// <copyright file="TransportSelectorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using slskd.Mesh.Dht;

namespace slskd.Tests.Unit.Mesh.Transport;

public class TransportSelectorTests
{
    private readonly MeshTransportOptions _transportOptions;
    private readonly List<ITransportDialer> _dialers;
    private readonly TransportSelector _selector;

    public TransportSelectorTests()
    {
        _transportOptions = new MeshTransportOptions
        {
            EnableDirect = true,
            Tor = new TorTransportOptions { Enabled = true },
            I2P = new I2PTransportOptions { Enabled = true }
        };

        _dialers = new List<ITransportDialer>
        {
            new MockTransportDialer(TransportType.DirectQuic, true),
            new MockTransportDialer(TransportType.TorOnionQuic, true),
            new MockTransportDialer(TransportType.I2PQuic, false) // Unavailable
        };

        _selector = new TransportSelector(_transportOptions, _dialers, null!);
    }

    [Fact]
    public void GetCandidateEndpoints_WithDirectEnabled_ReturnsDirectEndpoint()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = "192.168.1.1",
                    Port = 443,
                    Scope = TransportScope.Control
                }
            }
        };

        // Act
        var candidates = _selector.GetCandidateEndpoints(descriptor, TransportScope.Control);

        // Assert
        Assert.Single(candidates);
        Assert.Equal(TransportType.DirectQuic, candidates[0].TransportType);
    }

    [Fact]
    public void GetCandidateEndpoints_WithTorDisabled_ExcludesTorEndpoints()
    {
        // Arrange
        var options = new MeshTransportOptions { Tor = new TorTransportOptions { Enabled = false } };
        var selector = new TransportSelector(options, _dialers, null!);

        var descriptor = new MeshPeerDescriptor
        {
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint { TransportType = TransportType.DirectQuic, Host = "192.168.1.1", Port = 443 },
                new TransportEndpoint { TransportType = TransportType.TorOnionQuic, Host = "onion.onion", Port = 443 }
            }
        };

        // Act
        var candidates = selector.GetCandidateEndpoints(descriptor, TransportScope.Control);

        // Assert
        Assert.Single(candidates);
        Assert.Equal(TransportType.DirectQuic, candidates[0].TransportType);
    }

    [Fact]
    public void GetCandidateEndpoints_WithExpiredEndpoint_ExcludesExpired()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = "192.168.1.1",
                    Port = 443,
                    ValidToUnixMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds() // Expired
                }
            }
        };

        // Act
        var candidates = _selector.GetCandidateEndpoints(descriptor, TransportScope.Control);

        // Assert
        Assert.Empty(candidates);
    }

    [Fact]
    public void GetCandidateEndpoints_WithWrongScope_ExcludesMismatchedScope()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = "192.168.1.1",
                    Port = 443,
                    Scope = TransportScope.Data // Data only
                }
            }
        };

        // Act
        var candidates = _selector.GetCandidateEndpoints(descriptor, TransportScope.Control);

        // Assert
        Assert.Empty(candidates);
    }

    [Fact]
    public void GetBestEndpoint_ReturnsLowestPreference()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint { TransportType = TransportType.DirectQuic, Host = "host1", Port = 443, Preference = 5 },
                new TransportEndpoint { TransportType = TransportType.TorOnionQuic, Host = "host2", Port = 443, Preference = 1 }, // Best
                new TransportEndpoint { TransportType = TransportType.I2PQuic, Host = "host3", Port = 443, Preference = 3 }
            }
        };

        // Act
        var best = _selector.GetBestEndpoint(descriptor, TransportScope.Control);

        // Assert
        Assert.NotNull(best);
        Assert.Equal(TransportType.TorOnionQuic, best.TransportType);
        Assert.Equal(1, best.Preference);
    }

    [Fact]
    public void IsEndpointCompatible_WithDisabledTransport_ReturnsFalse()
    {
        // Arrange
        var options = new MeshTransportOptions { Tor = new TorTransportOptions { Enabled = false } };
        var selector = new TransportSelector(options, _dialers, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.TorOnionQuic,
            Host = "onion.onion",
            Port = 443
        };

        // Act
        var result = selector.IsEndpointCompatible(endpoint, TransportScope.Control);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEndpointCompatible_WithDataScopeAndNoDataAllowed_ReturnsFalse()
    {
        // Arrange
        var options = new MeshTransportOptions
        {
            Tor = new TorTransportOptions { Enabled = true, AllowDataOverTor = false }
        };
        var selector = new TransportSelector(options, _dialers, null!);

        var endpoint = new TransportEndpoint
        {
            TransportType = TransportType.TorOnionQuic,
            Host = "onion.onion",
            Port = 443,
            Scope = TransportScope.Data
        };

        // Act
        var result = selector.IsEndpointCompatible(endpoint, TransportScope.Data);

        // Assert
        Assert.False(result);
    }

    // Mock transport dialer for testing
    private class MockTransportDialer : ITransportDialer
    {
        private readonly bool _isAvailable;

        public MockTransportDialer(TransportType transportType, bool isAvailable)
        {
            TransportType = transportType;
            _isAvailable = isAvailable;
        }

        public TransportType TransportType { get; }

        public bool CanHandle(TransportEndpoint endpoint) => endpoint.TransportType == TransportType;

        public Task<Stream> DialAsync(TransportEndpoint endpoint, string? isolationKey = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> DialWithPinsAsync(TransportEndpoint endpoint, IEnumerable<string> certificatePins, string? isolationKey = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_isAvailable);

        public DialerStatistics GetStatistics() => new DialerStatistics { TransportType = TransportType };
    }
}

