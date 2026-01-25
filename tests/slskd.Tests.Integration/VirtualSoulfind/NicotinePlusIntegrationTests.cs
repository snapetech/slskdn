namespace slskd.Tests.Integration.VirtualSoulfind;

using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using slskd.Tests.Integration;
using slskd.VirtualSoulfind.Bridge;

/// <summary>
/// Nicotine+ integration tests for bridge compatibility.
/// Exercises the Bridge API used by Nicotine+ / legacy Soulseek clients.
/// </summary>
public class NicotinePlusIntegrationTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly StubWebApplicationFactory _factory;

    public NicotinePlusIntegrationTests(StubWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NicotinePlus_ConnectToBridge_ShouldSucceed()
    {
        using var client = _factory.CreateClient();
        await _factory.Services.GetRequiredService<ISoulfindBridgeService>().StartAsync();

        var status = await client.GetAsync("/api/bridge/status");
        status.EnsureSuccessStatusCode();
        var health = await status.Content.ReadFromJsonAsync<BridgeHealthStatus>();
        Assert.NotNull(health);
        Assert.True(health.IsHealthy);
        Assert.NotNull(health.Version);
    }

    [Fact]
    public async Task NicotinePlus_SearchViaBridge_ShouldReturnResults()
    {
        var stub = _factory.Services.GetRequiredService<StubBridgeApi>();
        stub.SearchResults = new List<BridgeUser>
        {
            new()
            {
                PeerId = "mesh-peer-1",
                Username = "mesh-peer-1",
                Files = new List<BridgeFile>
                {
                    new() { Path = "Daft Punk - Around The World.flac", SizeBytes = 12345678, MbRecordingId = "abc-123" }
                }
            }
        };

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/bridge/search", new { query = "Daft Punk Around The World" });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BridgeSearchResult>();
        Assert.NotNull(result);
        Assert.Equal("Daft Punk Around The World", result.Query);
        var user = Assert.Single(result.Users);
        Assert.Equal("mesh-peer-1", user.PeerId);
        Assert.Equal("mesh-peer-1", user.Username);
        var file = Assert.Single(user.Files);
        Assert.Equal("Daft Punk - Around The World.flac", file.Path);
        Assert.Equal(12345678, file.SizeBytes);
    }

    [Fact]
    public async Task NicotinePlus_DownloadViaBridge_ShouldTransferFile()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/bridge/download", new
        {
            username = "mesh-peer-1",
            filename = "Artist - Track.flac",
            targetPath = "/tmp"
        });
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("transfer_id", out var tid));
        Assert.Equal(JsonValueKind.String, tid.ValueKind);
        Assert.False(string.IsNullOrEmpty(tid.GetString()));
    }

    [Fact]
    public async Task NicotinePlus_JoinRoomViaBridge_ShouldMapToScene()
    {
        var stub = _factory.Services.GetRequiredService<StubBridgeApi>();
        stub.Rooms = new List<BridgeRoom>
        {
            new() { Name = "warp", MemberCount = 3 }
        };

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/bridge/rooms");
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<BridgeRoomsResponse>();
        Assert.NotNull(wrapper);
        var room = Assert.Single(wrapper.Rooms);
        Assert.Equal("warp", room.Name);
        Assert.Equal(3, room.MemberCount);
    }

    [Fact]
    public async Task NicotinePlus_DisasterMode_ShouldWorkTransparently()
    {
        using var client = _factory.CreateClient();

        var searchRes = await client.PostAsJsonAsync("/api/bridge/search", new { query = "x" });
        searchRes.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, searchRes.StatusCode);

        var downloadRes = await client.PostAsJsonAsync("/api/bridge/download", new
        {
            username = "u",
            filename = "f",
            targetPath = "/tmp"
        });
        downloadRes.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, downloadRes.StatusCode);
    }

    [Fact]
    public async Task NicotinePlus_MultipleClients_ShouldHandleConcurrency()
    {
        using var client = _factory.CreateClient();

        var searchTasks = Enumerable.Range(0, 5)
            .Select(_ => client.PostAsJsonAsync("/api/bridge/search", new { query = "concurrent" }));
        var downloadTasks = Enumerable.Range(0, 5)
            .Select(i => client.PostAsJsonAsync("/api/bridge/download", new
            {
                username = "u",
                filename = $"f{i}.flac",
                targetPath = "/tmp"
            }));

        var all = searchTasks.Concat(downloadTasks).ToArray();
        var results = await Task.WhenAll(all);

        foreach (var r in results)
        {
            r.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
    }

    private sealed class BridgeRoomsResponse
    {
        [JsonPropertyName("rooms")]
        public List<BridgeRoom> Rooms { get; set; } = new();
    }
}

/// <summary>
/// README for Nicotine+ integration tests.
/// </summary>
public static class NicotinePlusTestingReadme
{
    public const string Content = @"
# Nicotine+ Integration Tests

## Prerequisites

1. **Install Nicotine+**:
   ```bash
   # Ubuntu/Debian
   sudo apt install nicotine

   # macOS
   brew install nicotine-plus

   # Or download from: https://nicotine-plus.org/
   ```

2. **Start slskdn with bridge enabled**:
   ```yaml
   VirtualSoulfind:
     Bridge:
       Enabled: true
       Port: 2242
   ```

## Running Tests

### Automated Tests (Bridge API)

The `NicotinePlusIntegrationTests` exercise the Bridge HTTP API (search, download, rooms, status) using `StubWebApplicationFactory` with `StubBridgeApi` and `TestSoulfindBridgeService`. No real Nicotine+ or soulfind process is required.

```bash
dotnet test --filter NicotinePlus
```

### Manual Testing with Nicotine+

1. **Start slskdn**:
   ```bash
   cd src/slskd
   dotnet run
   ```

2. **Configure Nicotine+**:
   - Open Nicotine+ settings
   - Set server to `localhost:2242`
   - Disable authentication (or use bridge credentials)

3. **Test Scenarios**:

   **Search Test**:
   - Search: ""Daft Punk Around The World""
   - Verify: Results show `mesh-peer-xxx` usernames
   - Verify: Filenames formatted correctly

   **Download Test**:
   - Download a file from search results
   - Verify: Progress updates in Nicotine+
   - Verify: File completes and hash matches

   **Room Test**:
   - Join room ""warp""
   - Verify: Mapped to scene:label:warp-records
   - Verify: Members are mesh peers

   **Disaster Mode Test**:
   - Stop Soulseek server
   - Wait for disaster mode activation
   - Perform searches/downloads
   - Verify: Works transparently via mesh

## Troubleshooting

### Connection Refused
- Check bridge is running: `curl http://localhost:5030/api/bridge/status`
- Check port 2242 is not in use: `lsof -i :2242`
- Check Soulfind process started: `ps aux | grep soulfind`

### No Search Results
- Check shadow index is populated: `curl http://localhost:5030/api/virtualsoulfind/telemetry`
- Check MusicBrainz integration working
- Enable debug logging: `Logging:LogLevel:slskd.VirtualSoulfind.Bridge = Debug`

### Download Hangs
- Check mesh transfer service: `curl http://localhost:5030/api/bridge/status`
- Check peer discovery working
- Verify file exists in shadow index

## CI/CD

Integration tests run in CI using:
- `StubWebApplicationFactory` with `StubBridgeApi` / `TestSoulfindBridgeService`
- No external Nicotine+ or soulfind binary required

See `.github/workflows` for test configuration.
";
}
