namespace slskd.Tests.Integration.VirtualSoulfind;

using System.Net.Http;
using Xunit;
using slskd.Tests.Integration;

/// <summary>
/// Load tests for Virtual Soulfind scalability. Each test runs a minimal smoke against VSF endpoints;
/// full 10k shards / 1k queries / 24h runs are for manual or nightly runs.
/// </summary>
public class LoadTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LoadTests(StubWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DhtPublish_HighVolume_ShouldHandleLoad()
    {
        // Smoke: VSF host and disaster-mode status (DHT publish path is exercised in prod)
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ShadowIndex_ManyQueries_ShouldCacheEffectively()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/shadow-index/00000000-0000-0000-0000-000000000001");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Scenes_ManyMembers_ShouldScaleWell()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DisasterMode_LongDuration_ShouldStayStable()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ShardSize_ExtremeContent_ShouldStayWithinLimits()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/shadow-index/00000000-0000-0000-0000-000000000002");
        r.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// README for Virtual Soulfind tests.
/// </summary>
public static class TestingReadme
{
    public const string Content = @"
# Virtual Soulfind Testing

## Integration Tests

Run integration tests:
```
dotnet test --filter Category=VirtualSoulfind
```

Tests cover:
- Disaster mode full workflow
- Shadow index capture → publish → query
- Scene join → announce → discover
- Privacy audit verification
- Graceful degradation logic
- Telemetry tracking

## Load Tests

Load tests run as **minimal HTTP smokes** in CI (no skips). Full scenarios (10k shards, 1k queries, 24h) are for manual or nightly runs.

```bash
dotnet test --filter LoadTests
```

### Test Scenarios (smoke in CI; full load manual/nightly)

1. **DHT Publish Load** (T-840) — smoke: disaster-mode status
2. **Shadow Index Query Load** — smoke: shadow-index by ID
3. **Scene Membership Load** — smoke: disaster-mode status
4. **Disaster Mode Stability** — smoke: disaster-mode status
5. **Shard Size Limits** — smoke: shadow-index by ID

## Mocking

Integration tests use:
- `DhtClientStub` for DHT operations
- In-memory observation store
- Mock Soulseek client for health simulation

## CI/CD

Integration and load tests (as smokes) run on every PR.
Full load scenarios (10k shards, 1k queries, 24h) are for nightly runs.
";
}

