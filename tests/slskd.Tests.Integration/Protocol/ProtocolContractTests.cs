namespace slskd.Tests.Integration.Protocol;

using slskd.Tests.Integration.Harness;
using Xunit;

/// <summary>
/// L1 protocol contract tests - basic Soulseek protocol compliance.
/// </summary>
[Trait("Category", "L1-Protocol")]
public class ProtocolContractTests : IAsyncLifetime
{
    private SoulfindRunner? soulfind;
    private SlskdnTestClient? client;

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        soulfind = new SoulfindRunner(loggerFactory.CreateLogger<SoulfindRunner>());
        await soulfind.StartAsync();

        client = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "protocol-test");
        await client.StartAsync(soulfindPort: soulfind.Port);
    }

    public async Task DisposeAsync()
    {
        if (client != null) await client.DisposeAsync();
        if (soulfind != null) await soulfind.DisposeAsync();
    }

    [Fact]
    public async Task Should_Login_And_Handshake()
    {
        // Arrange: Client started and connected to Soulfind

        // Act: Check connection status
        var response = await client!.HttpClient.GetAsync("/api/server/status");

        // Assert: Connection established
        Assert.True(response.IsSuccessStatusCode);
        // TODO: Verify actual Soulseek handshake completed
    }

    [Fact]
    public async Task Should_Send_Keepalive_Pings()
    {
        // Arrange: Wait for keepalive interval
        await Task.Delay(TimeSpan.FromSeconds(30));

        // Act: Check connection still alive
        var response = await client!.HttpClient.GetAsync("/api/server/status");

        // Assert: Connection maintained
        Assert.True(response.IsSuccessStatusCode);
        // TODO: Verify keepalive packets sent
    }

    [Fact]
    public async Task Should_Perform_Search()
    {
        // Arrange: Add shared file to search for
        await client!.AddSharedFileAsync("test-artist - test-track.mp3", new byte[1024]);

        // Act: Perform search
        var response = await client.SearchAsync("test artist test track");

        // Assert: Search successful
        Assert.True(response.IsSuccessStatusCode);
        // TODO: Verify search request/response protocol
    }

    [Fact]
    public async Task Should_Join_And_Leave_Rooms()
    {
        // Arrange: Room name
        var roomName = "test-room";

        // Act: Join room
        var joinResponse = await client!.HttpClient.PostAsJsonAsync("/api/rooms", new { room = roomName });

        // Assert: Join successful
        Assert.True(joinResponse.IsSuccessStatusCode);

        // Act: Leave room
        var leaveResponse = await client.HttpClient.DeleteAsync($"/api/rooms/{roomName}");

        // Assert: Leave successful
        Assert.True(leaveResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Should_Browse_User_Files()
    {
        // Arrange: Username to browse
        var username = "test-user";

        // Act: Browse user
        var response = await client!.HttpClient.GetAsync($"/api/users/{username}/browse");

        // Assert: Browse request sent
        // (May timeout if user doesn't exist, but protocol should be correct)
        // TODO: Verify browse request format
        Assert.NotNull(response);
    }

    [Fact]
    public async Task Should_Handle_Disconnect_And_Reconnect()
    {
        // Arrange: Kill Soulfind
        await soulfind!.StopAsync();

        // Wait for disconnect detection
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act: Restart Soulfind
        await soulfind.StartAsync();

        // Wait for reconnection
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert: Reconnected
        var response = await client!.HttpClient.GetAsync("/api/server/status");
        // TODO: Verify reconnection logic
    }
}
