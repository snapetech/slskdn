// <copyright file="OverlayRateLimiterMeshSearchTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System;
using System.Threading;
using slskd.DhtRendezvous.Security;
using Xunit;

/// <summary>
/// Unit tests for OverlayRateLimiter.CheckMeshSearchRequest.
/// </summary>
public class OverlayRateLimiterMeshSearchTests
{
    [Fact]
    public void CheckMeshSearchRequest_UnderLimit_Allows()
    {
        using var limiter = new OverlayRateLimiter();
        var id = Guid.NewGuid().ToString();

        for (int i = 0; i < 5; i++)
        {
            var r = limiter.CheckMeshSearchRequest(id);
            Assert.True(r.IsAllowed, $"Request {i + 1} should be allowed");
        }
    }

    [Fact]
    public void CheckMeshSearchRequest_OverLimit_RateLimits()
    {
        using var limiter = new OverlayRateLimiter();
        var id = Guid.NewGuid().ToString();

        for (int i = 0; i < OverlayRateLimiter.MaxMeshSearchRequestsPerMinute; i++)
        {
            limiter.CheckMeshSearchRequest(id);
        }

        var r = limiter.CheckMeshSearchRequest(id);
        Assert.False(r.IsAllowed);
        Assert.NotNull(r.Reason);
        Assert.Contains("Mesh search", r.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
