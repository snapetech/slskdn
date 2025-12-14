// <copyright file="TransportPolicyTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class TransportPolicyTests : IDisposable
{
    private readonly Mock<ILogger<TransportPolicyManager>> _loggerMock;
    private readonly TransportPolicyManager _policyManager;

    public TransportPolicyTests()
    {
        _loggerMock = new Mock<ILogger<TransportPolicyManager>>();
        _policyManager = new TransportPolicyManager(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act & Assert - Should not throw
        var manager = new TransportPolicyManager(_loggerMock.Object);
        Assert.NotNull(manager);
    }

    [Fact]
    public void AddOrUpdatePolicy_WithValidPolicy_AddsPolicy()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            PeerId = "peer123",
            PodId = "pod456",
            PreferPrivateTransports = true,
            DisableClearnet = false
        };

        // Act
        _policyManager.AddOrUpdatePolicy(policy);

        // Assert
        var retrieved = _policyManager.GetApplicablePolicy("peer123", "pod456");
        Assert.NotNull(retrieved);
        Assert.True(retrieved.PreferPrivateTransports);
        Assert.False(retrieved.DisableClearnet);
    }

    [Fact]
    public void AddOrUpdatePolicy_WithDuplicatePolicy_ReplacesExisting()
    {
        // Arrange
        var policy1 = new TransportPolicy
        {
            PeerId = "peer123",
            PreferPrivateTransports = false
        };

        var policy2 = new TransportPolicy
        {
            PeerId = "peer123",
            PreferPrivateTransports = true
        };

        // Act
        _policyManager.AddOrUpdatePolicy(policy1);
        _policyManager.AddOrUpdatePolicy(policy2);

        // Assert
        var retrieved = _policyManager.GetApplicablePolicy("peer123");
        Assert.NotNull(retrieved);
        Assert.True(retrieved.PreferPrivateTransports); // Should have the updated value
    }

    [Fact]
    public void GetApplicablePolicy_WithPeerAndPod_ReturnsMostSpecific()
    {
        // Arrange
        var globalPolicy = new TransportPolicy { PreferPrivateTransports = false };
        var peerPolicy = new TransportPolicy { PeerId = "peer123", PreferPrivateTransports = true };
        var specificPolicy = new TransportPolicy
        {
            PeerId = "peer123",
            PodId = "pod456",
            PreferPrivateTransports = false
        };

        _policyManager.AddOrUpdatePolicy(globalPolicy);
        _policyManager.AddOrUpdatePolicy(peerPolicy);
        _policyManager.AddOrUpdatePolicy(specificPolicy);

        // Act
        var retrieved = _policyManager.GetApplicablePolicy("peer123", "pod456");

        // Assert - Should return the most specific policy (peer + pod)
        Assert.NotNull(retrieved);
        Assert.Equal("peer123", retrieved.PeerId);
        Assert.Equal("pod456", retrieved.PodId);
        Assert.False(retrieved.PreferPrivateTransports);
    }

    [Fact]
    public void GetApplicablePolicy_WithPeerOnly_ReturnsPeerPolicy()
    {
        // Arrange
        var peerPolicy = new TransportPolicy { PeerId = "peer123", PreferPrivateTransports = true };

        _policyManager.AddOrUpdatePolicy(peerPolicy);

        // Act
        var retrieved = _policyManager.GetApplicablePolicy("peer123");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("peer123", retrieved.PeerId);
        Assert.True(retrieved.PreferPrivateTransports);
    }

    [Fact]
    public void GetApplicablePolicy_WithUnknownPeer_ReturnsNull()
    {
        // Act
        var retrieved = _policyManager.GetApplicablePolicy("unknown-peer");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void RemovePolicy_WithPeerAndPod_RemovesSpecificPolicy()
    {
        // Arrange
        var policy = new TransportPolicy { PeerId = "peer123", PodId = "pod456" };
        _policyManager.AddOrUpdatePolicy(policy);

        // Act
        _policyManager.RemovePolicy("peer123", "pod456");

        // Assert
        var retrieved = _policyManager.GetApplicablePolicy("peer123", "pod456");
        Assert.Null(retrieved);
    }

    [Fact]
    public void GetAllPolicies_ReturnsAllPolicies()
    {
        // Arrange
        var policy1 = new TransportPolicy { PeerId = "peer1" };
        var policy2 = new TransportPolicy { PeerId = "peer2" };

        _policyManager.AddOrUpdatePolicy(policy1);
        _policyManager.AddOrUpdatePolicy(policy2);

        // Act
        var allPolicies = _policyManager.GetAllPolicies();

        // Assert
        Assert.Equal(2, allPolicies.Count);
        Assert.Contains(allPolicies, p => p.PeerId == "peer1");
        Assert.Contains(allPolicies, p => p.PeerId == "peer2");
    }

    [Fact]
    public void TransportPolicy_AppliesTo_WithMatchingPeerAndPod_ReturnsTrue()
    {
        // Arrange
        var policy = new TransportPolicy { PeerId = "peer123", PodId = "pod456" };

        // Act
        var applies = policy.AppliesTo("peer123", "pod456");

        // Assert
        Assert.True(applies);
    }

    [Fact]
    public void TransportPolicy_AppliesTo_WithMismatchingPeer_ReturnsFalse()
    {
        // Arrange
        var policy = new TransportPolicy { PeerId = "peer123", PodId = "pod456" };

        // Act
        var applies = policy.AppliesTo("peer999", "pod456");

        // Assert
        Assert.False(applies);
    }

    [Fact]
    public void TransportPolicy_AppliesTo_WithDisabledPolicy_ReturnsFalse()
    {
        // Arrange
        var policy = new TransportPolicy { PeerId = "peer123", IsEnabled = false };

        // Act
        var applies = policy.AppliesTo("peer123");

        // Assert
        Assert.False(applies);
    }

    [Fact]
    public void TransportPolicy_IsTransportAllowed_WithAllowedTransport_ReturnsTrue()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            AllowedTransportTypes = new List<TransportType> { TransportType.TorOnionQuic }
        };
        var globalOptions = new MeshTransportOptions { Tor = new TorTransportOptions { Enabled = true } };

        // Act
        var allowed = policy.IsTransportAllowed(TransportType.TorOnionQuic, globalOptions);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public void TransportPolicy_IsTransportAllowed_WithDisallowedTransport_ReturnsFalse()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            AllowedTransportTypes = new List<TransportType> { TransportType.TorOnionQuic }
        };
        var globalOptions = new MeshTransportOptions { Direct = new DirectTransportOptions { Enabled = true } };

        // Act
        var allowed = policy.IsTransportAllowed(TransportType.DirectQuic, globalOptions);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TransportPolicy_IsTransportAllowed_WithDisableClearnet_ReturnsFalseForDirect()
    {
        // Arrange
        var policy = new TransportPolicy { DisableClearnet = true };
        var globalOptions = new MeshTransportOptions { Direct = new DirectTransportOptions { Enabled = true } };

        // Act
        var allowed = policy.IsTransportAllowed(TransportType.DirectQuic, globalOptions);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void TransportPolicy_GetEffectivePreferenceOrder_WithCustomOrder_ReturnsCustomOrder()
    {
        // Arrange
        var policy = new TransportPolicy
        {
            TransportPreferenceOrder = new List<TransportType> { TransportType.TorOnionQuic, TransportType.DirectQuic }
        };
        var globalOrder = new List<TransportType> { TransportType.DirectQuic, TransportType.TorOnionQuic };

        // Act
        var effectiveOrder = policy.GetEffectivePreferenceOrder(globalOrder);

        // Assert
        Assert.Equal(new[] { TransportType.TorOnionQuic, TransportType.DirectQuic }, effectiveOrder);
    }

    [Fact]
    public void TransportPolicy_GetEffectivePreferenceOrder_WithoutCustomOrder_ReturnsGlobalOrder()
    {
        // Arrange
        var policy = new TransportPolicy();
        var globalOrder = new List<TransportType> { TransportType.DirectQuic, TransportType.TorOnionQuic };

        // Act
        var effectiveOrder = policy.GetEffectivePreferenceOrder(globalOrder);

        // Assert
        Assert.Equal(globalOrder, effectiveOrder);
    }
}

