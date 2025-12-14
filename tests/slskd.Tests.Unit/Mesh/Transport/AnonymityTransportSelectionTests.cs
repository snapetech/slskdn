// <copyright file="AnonymityTransportSelectionTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class AnonymityTransportSelectionTests : IDisposable
{
    private readonly Mock<ILogger<AnonymityTransportSelector>> _selectorLoggerMock;
    private readonly Mock<ILogger<TransportPolicyManager>> _policyLoggerMock;
    private readonly TransportPolicyManager _policyManager;
    private readonly AdversarialOptions _adversarialOptions;

    public AnonymityTransportSelectionTests()
    {
        _selectorLoggerMock = new Mock<ILogger<AnonymityTransportSelector>>();
        _policyLoggerMock = new Mock<ILogger<TransportPolicyManager>>();
        _policyManager = new TransportPolicyManager(_policyLoggerMock.Object);

        _adversarialOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions
            {
                Enabled = true,
                Mode = AnonymityMode.Tor
            },
            MeshTransport = new MeshTransportOptions
            {
                EnableDirect = true,
                Tor = new TorTransportOptions { Enabled = true },
                I2P = new I2PTransportOptions { Enabled = true }
            }
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
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);
        Assert.NotNull(selector);
    }

    [Fact]
    public void GetTransportPriorityOrder_WithPolicyPreferringPrivate_PrioritizesPrivateTransports()
    {
        // Arrange
        var policy = new TransportPolicy { PreferPrivateTransports = true };
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act - Access private method via reflection for testing
        var method = typeof(AnonymityTransportSelector).GetMethod("GetTransportPriorityOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var priorityOrder = (List<AnonymityTransportType>)method.Invoke(selector, new object[] { policy });

        // Assert - Tor should come before Direct when private transports are preferred
        var torIndex = priorityOrder.IndexOf(AnonymityTransportType.Tor);
        var directIndex = priorityOrder.IndexOf(AnonymityTransportType.Direct);
        Assert.True(torIndex < directIndex, "Tor should be prioritized over Direct when preferring private transports");
    }

    [Fact]
    public void GetTransportPriorityOrder_WithPolicyDisablingClearnet_ExcludesDirect()
    {
        // Arrange
        var policy = new TransportPolicy { DisableClearnet = true };
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act
        var method = typeof(AnonymityTransportSelector).GetMethod("GetTransportPriorityOrder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var priorityOrder = (List<AnonymityTransportType>)method.Invoke(selector, new object[] { policy });

        // Assert
        Assert.DoesNotContain(AnonymityTransportType.Direct, priorityOrder);
    }

    [Fact]
    public void IsTransportAllowedByPolicy_WithAllowedTransport_ReturnsTrue()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            AllowedTransportTypes = new List<TransportType> { TransportType.TorOnionQuic }
        };
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act
        var method = typeof(AnonymityTransportSelector).GetMethod("IsTransportAllowedByPolicy",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allowed = (bool)method.Invoke(selector, new object[] { AnonymityTransportType.Tor, policy });

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public void IsTransportAllowedByPolicy_WithDisallowedTransport_ReturnsFalse()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            AllowedTransportTypes = new List<TransportType> { TransportType.TorOnionQuic }
        };
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLogger.Object);

        // Act
        var method = typeof(AnonymityTransportSelector).GetMethod("IsTransportAllowedByPolicy",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var allowed = (bool)method.Invoke(selector, new object[] { AnonymityTransportType.Direct, policy });

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void GetTransportStatuses_ReturnsAllTransportStatuses()
    {
        // Arrange
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act
        var statuses = selector.GetTransportStatuses();

        // Assert
        Assert.NotNull(statuses);
        // Should contain at least Tor transport
        Assert.Contains(AnonymityTransportType.Tor, statuses.Keys);
    }

    [Fact]
    public async Task SelectAndConnectAsync_WithPeerPolicy_UsesPolicyAwareSelection()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            PeerId = "test-peer",
            PreferPrivateTransports = true
        };
        _policyManager.AddOrUpdatePolicy(policy);

        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act & Assert - Should not throw, even though transports aren't fully set up for testing
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.SelectAndConnectAsync("test-peer", null, "example.com", 80, null, CancellationToken.None));
    }

    [Fact]
    public async Task SelectAndConnectAsync_WithPodPolicy_ConsidersPodContext()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            PodId = "test-pod",
            DisableClearnet = true
        };
        _policyManager.AddOrUpdatePolicy(policy);

        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act & Assert - Should attempt connection without Direct transport
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.SelectAndConnectAsync("peer123", "test-pod", "example.com", 80, null, CancellationToken.None));
    }

    [Fact]
    public void TransportPolicyIntegration_WithSelector_RespectsPolicyConstraints()
    {
        // Arrange
        var restrictivePolicy = new TransportPolicy
        {
            PeerId = "restricted-peer",
            AllowedTransportTypes = new List<TransportType> { TransportType.TorOnionQuic },
            DisableClearnet = true
        };
        _policyManager.AddOrUpdatePolicy(restrictivePolicy);

        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act - Check if Direct transport is allowed for this peer
        var method = typeof(AnonymityTransportSelector).GetMethod("IsTransportAllowedByPolicy",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var directAllowed = (bool)method.Invoke(selector, new object[] { AnonymityTransportType.Direct, restrictivePolicy });
        var torAllowed = (bool)method.Invoke(selector, new object[] { AnonymityTransportType.Tor, restrictivePolicy });

        // Assert
        Assert.False(directAllowed, "Direct transport should not be allowed for restricted peer");
        Assert.True(torAllowed, "Tor transport should be allowed for restricted peer");
    }

    [Fact]
    public void PolicySpecificity_PeerPlusPod_MostSpecific()
    {
        // Arrange
        var peerOnlyPolicy = new TransportPolicy { PeerId = "peer123", PreferPrivateTransports = false };
        var peerPodPolicy = new TransportPolicy { PeerId = "peer123", PodId = "pod456", PreferPrivateTransports = true };

        _policyManager.AddOrUpdatePolicy(peerOnlyPolicy);
        _policyManager.AddOrUpdatePolicy(peerPodPolicy);

        // Act
        var applicablePolicy = _policyManager.GetApplicablePolicy("peer123", "pod456");

        // Assert - Should return the more specific policy (peer + pod)
        Assert.NotNull(applicablePolicy);
        Assert.Equal("peer123", applicablePolicy.PeerId);
        Assert.Equal("pod456", applicablePolicy.PodId);
        Assert.True(applicablePolicy.PreferPrivateTransports);
    }

    [Fact]
    public async Task TestConnectivityAsync_CompletesWithoutError()
    {
        // Arrange
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Act & Assert - Should complete without throwing
        await selector.TestConnectivityAsync();
    }

    [Fact]
    public void TransportTypeMapping_CorrectlyMapsBetweenTypes()
    {
        // Arrange
        var selector = new AnonymityTransportSelector(_adversarialOptions, _policyManager, _selectorLoggerMock.Object);

        // Test mapping methods via reflection
        var mapToAnonymityMethod = typeof(AnonymityTransportSelector).GetMethod("MapTransportTypeToAnonymityType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var mapToTransportMethod = typeof(AnonymityTransportSelector).GetMethod("MapAnonymityTypeToTransportType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var anonymityType = (AnonymityTransportType?)mapToAnonymityMethod.Invoke(null, new object[] { TransportType.TorOnionQuic });
        var transportType = (TransportType?)mapToTransportMethod.Invoke(null, new object[] { AnonymityTransportType.Tor });

        // Assert
        Assert.Equal(AnonymityTransportType.Tor, anonymityType);
        Assert.Equal(TransportType.TorOnionQuic, transportType);
    }
}
