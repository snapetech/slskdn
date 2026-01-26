// <copyright file="OverlayRateLimiterMeshSearchTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
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
