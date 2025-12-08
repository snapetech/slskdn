// <copyright file="NetworkGuardTests.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Security;

using System;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Common.Security;
using Xunit;

public class NetworkGuardTests : IDisposable
{
    private readonly NetworkGuard _guard;
    private readonly IPAddress _testIp = IPAddress.Parse("192.168.1.100");

    public NetworkGuardTests()
    {
        _guard = new NetworkGuard(NullLogger<NetworkGuard>.Instance)
        {
            MaxConnectionsPerIp = 5,
            MaxGlobalConnections = 100,
            MaxMessagesPerMinute = 60,
            MaxMessageSize = 1024 * 1024,
        };
    }

    public void Dispose()
    {
        _guard.Dispose();
    }

    [Fact]
    public void AllowConnection_FirstConnection_ReturnsTrue()
    {
        var allowed = _guard.AllowConnection(_testIp);
        Assert.True(allowed);
    }

    [Fact]
    public void AllowConnection_UnderLimit_ReturnsTrue()
    {
        for (int i = 0; i < 4; i++)
        {
            _guard.RegisterConnection(_testIp);
        }
        
        var allowed = _guard.AllowConnection(_testIp);
        Assert.True(allowed);
    }

    [Fact]
    public void AllowConnection_AtLimit_ReturnsFalse()
    {
        for (int i = 0; i < 5; i++)
        {
            _guard.RegisterConnection(_testIp);
        }
        
        var allowed = _guard.AllowConnection(_testIp);
        Assert.False(allowed);
    }

    [Fact]
    public void RegisterConnection_ReturnsConnectionId()
    {
        var id = _guard.RegisterConnection(_testIp);
        Assert.NotNull(id);
        Assert.NotEmpty(id);
    }

    [Fact]
    public void UnregisterConnection_DecreasesCount()
    {
        var id1 = _guard.RegisterConnection(_testIp);
        _guard.RegisterConnection(_testIp);
        
        _guard.UnregisterConnection(_testIp, id1);
        
        // Should now be able to register more
        for (int i = 0; i < 3; i++)
        {
            _guard.RegisterConnection(_testIp);
        }
        
        Assert.True(_guard.AllowConnection(_testIp));
    }

    [Fact]
    public void AllowMessage_UnderLimit_ReturnsTrue()
    {
        var allowed = _guard.AllowMessage(_testIp, 100);
        Assert.True(allowed);
    }

    [Fact]
    public void AllowMessage_OverSizeLimit_ReturnsFalse()
    {
        var allowed = _guard.AllowMessage(_testIp, _guard.MaxMessageSize + 1);
        Assert.False(allowed);
    }

    [Fact]
    public void GetConnectionInfo_ReturnsCorrectCount()
    {
        _guard.RegisterConnection(_testIp);
        _guard.RegisterConnection(_testIp);
        
        var info = _guard.GetConnectionInfo(_testIp);
        
        Assert.NotNull(info);
        Assert.Equal(2, info.ActiveConnections);
    }

    [Fact]
    public void GetConnectionInfo_UnknownIp_ReturnsNull()
    {
        var info = _guard.GetConnectionInfo(IPAddress.Parse("10.0.0.1"));
        Assert.Null(info);
    }

    [Fact]
    public void GetStats_ReturnsCorrectStats()
    {
        _guard.RegisterConnection(_testIp);
        _guard.RegisterConnection(_testIp);
        
        var stats = _guard.GetStats();
        
        Assert.Equal(2, stats.TotalConnections);
        Assert.Equal(1, stats.TrackedIps);
    }

    [Fact]
    public void AllowConnection_GlobalLimit_ReturnsFalse()
    {
        using var guard = new NetworkGuard(NullLogger<NetworkGuard>.Instance)
        {
            MaxGlobalConnections = 3,
            MaxConnectionsPerIp = 100,
        };
        
        guard.RegisterConnection(IPAddress.Parse("10.0.0.1"));
        guard.RegisterConnection(IPAddress.Parse("10.0.0.2"));
        guard.RegisterConnection(IPAddress.Parse("10.0.0.3"));
        
        var allowed = guard.AllowConnection(IPAddress.Parse("10.0.0.4"));
        Assert.False(allowed);
    }

    [Fact]
    public void AllowRequest_UnderLimit_ReturnsTrue()
    {
        var allowed = _guard.AllowRequest(_testIp);
        Assert.True(allowed);
    }

    [Fact]
    public void AllowRequest_AtLimit_ReturnsFalse()
    {
        using var guard = new NetworkGuard(NullLogger<NetworkGuard>.Instance)
        {
            MaxPendingRequestsPerIp = 2,
        };
        
        guard.AllowRequest(_testIp);
        guard.AllowRequest(_testIp);
        
        var allowed = guard.AllowRequest(_testIp);
        Assert.False(allowed);
    }

    [Fact]
    public void CompleteRequest_AllowsNewRequests()
    {
        using var guard = new NetworkGuard(NullLogger<NetworkGuard>.Instance)
        {
            MaxPendingRequestsPerIp = 2,
        };
        
        guard.AllowRequest(_testIp);
        guard.AllowRequest(_testIp);
        
        // At limit
        Assert.False(guard.AllowRequest(_testIp));
        
        // Complete one
        guard.CompleteRequest(_testIp);
        
        // Should allow again
        Assert.True(guard.AllowRequest(_testIp));
    }

    [Fact]
    public void GetTopConnectors_ReturnsTopByCount()
    {
        // Register multiple connections from different IPs
        for (int i = 0; i < 3; i++)
        {
            _guard.RegisterConnection(_testIp);
        }
        
        var otherIp = IPAddress.Parse("10.0.0.1");
        _guard.RegisterConnection(otherIp);
        
        var top = _guard.GetTopConnectors(10);
        
        Assert.NotEmpty(top);
        Assert.Equal(_testIp, top[0].Ip);
        Assert.Equal(3, top[0].ActiveConnections);
    }

    [Fact]
    public void AllowMessage_RateLimit_ReturnsFalse()
    {
        using var guard = new NetworkGuard(NullLogger<NetworkGuard>.Instance)
        {
            MaxMessagesPerMinute = 5,
        };
        
        // Send 5 messages (at limit)
        for (int i = 0; i < 5; i++)
        {
            Assert.True(guard.AllowMessage(_testIp, 100));
        }
        
        // 6th should be blocked
        Assert.False(guard.AllowMessage(_testIp, 100));
    }
}
