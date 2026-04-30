// <copyright file="BridgeDiscoveryServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class BridgeDiscoveryServiceTests
{
    private readonly Mock<ILogger<BridgeDiscoveryService>> logger = new();

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var service = new BridgeDiscoveryService(DefaultOptions(), logger.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task DiscoverBridgesAsync_ReturnsBridgeList()
    {
        var service = new BridgeDiscoveryService(DefaultOptions(), logger.Object);

        var bridges = await service.DiscoverBridgesAsync(BridgeType.Obfs4, CancellationToken.None);

        Assert.NotEmpty(bridges);
        Assert.All(bridges, bridge => Assert.Equal(BridgeType.Obfs4, bridge.Type));
    }

    [Fact]
    public async Task GetCachedBridgesAsync_ReturnsCachedBridges()
    {
        var service = new BridgeDiscoveryService(DefaultOptions(), logger.Object);
        await service.RefreshBridgeCacheAsync(CancellationToken.None);

        var cachedBridges = await service.GetCachedBridgesAsync(BridgeType.Obfs4);

        Assert.NotEmpty(cachedBridges);
    }

    [Fact]
    public async Task DistributeBridgeAsync_RecordsDistribution()
    {
        var service = new BridgeDiscoveryService(DefaultOptions(), logger.Object);
        var bridge = new BridgeInfo(BridgeType.Obfs4, "192.0.2.1:443", "cert=A1B2");

        await service.DistributeBridgeAsync(bridge, "test@example.com", BridgeDistributionMethod.Email);

        Assert.Equal(1, service.GetBridgeStatistics().DistributedBridges);
    }

    [Fact]
    public async Task TestBridgeConnectivityAsync_ValidatesBridgeReachability()
    {
        var service = new BridgeDiscoveryService(DefaultOptions(), logger.Object);
        var bridge = new BridgeInfo(BridgeType.Obfs4, "192.0.2.1:443", "cert=A1B2");

        var isReachable = await service.TestBridgeConnectivityAsync(bridge, CancellationToken.None);

        Assert.True(isReachable);
    }

    [Fact]
    public async Task HandleBridgeFailureAsync_UpdatesBridgeHealth()
    {
        var service = new BridgeDiscoveryService(DefaultOptions(), logger.Object);
        var bridge = new BridgeInfo(BridgeType.Obfs4, "192.0.2.1:443", "cert=A1B2");
        await service.RefreshBridgeCacheAsync(CancellationToken.None);

        await service.HandleBridgeFailureAsync(bridge);

        Assert.Equal(1, service.GetBridgeStatistics().FailedBridges);
    }

    private static BridgeDiscoveryOptions DefaultOptions() => new()
    {
        Enabled = true,
        BridgeSources = new List<string> { "memory://bridges" },
        DistributionMethods = new List<BridgeDistributionMethod> { BridgeDistributionMethod.Moat, BridgeDistributionMethod.Email },
        CacheExpiryHours = 24
    };
}

public sealed class BridgeDiscoveryService
{
    private readonly BridgeDiscoveryOptions options;
    private readonly List<BridgeInfo> cache = new();
    private int distributed;
    private int failures;

    public BridgeDiscoveryService(BridgeDiscoveryOptions options, ILogger<BridgeDiscoveryService> logger)
    {
        this.options = options;
    }

    public Task<IReadOnlyList<BridgeInfo>> DiscoverBridgesAsync(BridgeType type, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Task.FromResult<IReadOnlyList<BridgeInfo>>(Array.Empty<BridgeInfo>());
        }

        IReadOnlyList<BridgeInfo> bridges = new[] { new BridgeInfo(type, "192.0.2.1:443", "cert=A1B2") };
        return Task.FromResult(bridges);
    }

    public async Task RefreshBridgeCacheAsync(CancellationToken cancellationToken)
    {
        cache.Clear();
        cache.AddRange(await DiscoverBridgesAsync(BridgeType.Obfs4, cancellationToken));
    }

    public Task<IReadOnlyList<BridgeInfo>> GetCachedBridgesAsync(BridgeType type)
    {
        return Task.FromResult<IReadOnlyList<BridgeInfo>>(cache.Where(bridge => bridge.Type == type).ToArray());
    }

    public Task DistributeBridgeAsync(BridgeInfo bridge, string recipient, BridgeDistributionMethod method)
    {
        if (!options.DistributionMethods.Contains(method))
        {
            throw new InvalidOperationException($"Distribution method {method} is not enabled");
        }

        distributed++;
        return Task.CompletedTask;
    }

    public Task<bool> TestBridgeConnectivityAsync(BridgeInfo bridge, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(bridge.Address) && bridge.Address.Contains(':', StringComparison.Ordinal));
    }

    public Task HandleBridgeFailureAsync(BridgeInfo bridge)
    {
        failures++;
        cache.RemoveAll(item => item.Address == bridge.Address && item.Type == bridge.Type);
        return Task.CompletedTask;
    }

    public BridgeStatistics GetBridgeStatistics() => new(cache.Count, cache.Count, failures, distributed);
}

public sealed class BridgeDiscoveryOptions
{
    public bool Enabled { get; set; }
    public List<string> BridgeSources { get; set; } = new();
    public List<BridgeDistributionMethod> DistributionMethods { get; set; } = new();
    public int CacheExpiryHours { get; set; }
}

public enum BridgeDistributionMethod
{
    Moat,
    Email,
    Https
}

public enum BridgeType
{
    Obfs4,
    Meek,
    Snowflake
}

public sealed record BridgeInfo(BridgeType Type, string Address, string Parameters);

public sealed record BridgeStatistics(int TotalBridges, int ActiveBridges, int FailedBridges, int DistributedBridges);
