// <copyright file="SearchServiceLifecycleTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Search;

using System;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.Common.Security;
using slskd.Search;
using slskd.Search.API;
using Soulseek;
using Xunit;

public class SearchServiceLifecycleTests
{
    [Fact]
    public void TryCancel_RemovesAndDisposesTrackedCancellationToken()
    {
        using var service = CreateService();
        var searchId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        GetCancellationTokens(service)[searchId] = cts;

        Assert.True(service.TryCancel(searchId));
        Assert.False(GetCancellationTokens(service).ContainsKey(searchId));
        Assert.True(cts.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => _ = cts.Token.WaitHandle);
    }

    [Fact]
    public void Dispose_CancelsAndDisposesAllTrackedCancellationTokens()
    {
        var service = CreateService();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        using var firstCts = new CancellationTokenSource();
        using var secondCts = new CancellationTokenSource();
        var tracked = GetCancellationTokens(service);
        tracked[firstId] = firstCts;
        tracked[secondId] = secondCts;

        service.Dispose();

        Assert.Empty(tracked);
        Assert.True(firstCts.IsCancellationRequested);
        Assert.True(secondCts.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => _ = firstCts.Token.WaitHandle);
        Assert.Throws<ObjectDisposedException>(() => _ = secondCts.Token.WaitHandle);
    }

    private static SearchService CreateService()
    {
        return new SearchService(
            Mock.Of<IHubContext<SearchHub>>(),
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            Mock.Of<ISoulseekSafetyLimiter>());
    }

    private static ConcurrentDictionary<Guid, CancellationTokenSource> GetCancellationTokens(SearchService service)
    {
        var property = typeof(SearchService).GetProperty(
            "CancellationTokens",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return Assert.IsType<ConcurrentDictionary<Guid, CancellationTokenSource>>(property?.GetValue(service));
    }
}
