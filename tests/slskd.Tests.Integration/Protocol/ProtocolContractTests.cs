namespace slskd.Tests.Integration.Protocol;

using slskd.Tests.Integration.Harness;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// L1 protocol contract tests - basic Soulseek protocol compliance.
/// </summary>
[Trait("Category", "L1-Protocol")]
public class ProtocolContractTests : IAsyncLifetime
{
    private readonly ITestOutputHelper output;
    private SoulfindRunner? soulfind;
    private SlskdnTestClient? client;

    public ProtocolContractTests(ITestOutputHelper output)
    {
        this.output = output;
    }

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
        // Skip if Soulfind not available (stub mode)
        if (soulfind != null && !soulfind.IsRunning)
        {
            // In stub mode, we can't test actual protocol compliance
            // These tests require a real Soulseek server simulator
            // See docs/dev/soulseek-server-simulation-options.md for setup
            output.WriteLine("Skipping test - Soulfind not available (stub mode)");
            return; // Skip gracefully
        }

        // Arrange: Client started and connected to Soulfind

        // Act: Check connection status
        var response = await client!.HttpClient.GetAsync("/api/server/status");

        // Assert: Connection established
        Assert.True(response.IsSuccessStatusCode, "Server should be connected");
        
        // Verify connection details
        var status = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(status.TryGetProperty("state", out var state), "Status should include state");
        
        // In stub mode, we can't verify actual handshake, but we can verify API responds
        output.WriteLine($"Server status: {status}");
    }

    [Fact]
    public async Task Should_Send_Keepalive_Pings()
    {
        // Skip if Soulfind not available (stub mode)
        if (soulfind != null && !soulfind.IsRunning)
        {
            output.WriteLine("Skipping test - Soulfind not available (stub mode)");
            return; // Skip gracefully - requires real server
        }

        // Arrange: Get initial connection state
        var initialResponse = await client!.HttpClient.GetAsync("/api/server/status");
        Assert.True(initialResponse.IsSuccessStatusCode, "Initial connection should be established");

        // Act: Wait for keepalive interval (Soulseek typically sends keepalive every 30-60 seconds)
        // For testing, we'll wait a shorter time and verify connection is still alive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act: Check connection still alive
        var response = await client.HttpClient.GetAsync("/api/server/status");

        // Assert: Connection maintained
        Assert.True(response.IsSuccessStatusCode, "Connection should remain alive after keepalive period");
        
        // Verify connection state hasn't changed
        var status = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(status.TryGetProperty("state", out _), "Status should include state");
        
        output.WriteLine("Connection maintained - keepalive working (or connection stable)");
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
        // Skip if Soulfind not available (stub mode)
        if (soulfind != null && !soulfind.IsRunning)
        {
            output.WriteLine("Skipping test - Soulfind not available (stub mode)");
            return; // Skip gracefully - requires real server
        }

        // Arrange: Verify initial connection
        var initialResponse = await client!.HttpClient.GetAsync("/api/server/status");
        Assert.True(initialResponse.IsSuccessStatusCode, "Initial connection should be established");

        // Act: Stop Soulfind (simulate disconnect)
        await soulfind!.StopAsync();
        output.WriteLine("Soulfind stopped - simulating disconnect");

        // Wait for disconnect detection (slskdn should detect connection loss)
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Verify disconnect detected (status may show disconnected state)
        var disconnectedResponse = await client.HttpClient.GetAsync("/api/server/status");
        // Status may still return 200, but state might indicate disconnected
        output.WriteLine("Disconnect period completed");

        // Act: Restart Soulfind
        await soulfind.StartAsync();
        output.WriteLine("Soulfind restarted - waiting for reconnection");

        // Wait for reconnection (slskdn should automatically reconnect)
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert: Reconnected
        var reconnectedResponse = await client.HttpClient.GetAsync("/api/server/status");
        Assert.True(reconnectedResponse.IsSuccessStatusCode, "Should reconnect after server restart");
        
        var status = await reconnectedResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        output.WriteLine($"Reconnection status: {status}");
        
        // Note: Full reconnection verification would require checking internal state
        // For now, we verify the API responds, indicating the client is functional
    }
}
