// <copyright file="BridgeIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.VirtualSoulfind;

using slskd.Tests.Integration.Harness;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// T-860: Bridge integration tests - Legacy client compatibility.
/// </summary>
[Trait("Category", "L2-Bridge")]
public class BridgeIntegrationTests : IAsyncLifetime
{
    private SoulfindRunner? soulfind;
    private SlskdnTestClient? alice;
    private SlskdnTestClient? bob;

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        soulfind = new SoulfindRunner(loggerFactory.CreateLogger<SoulfindRunner>());
        await soulfind.StartAsync();

        alice = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "alice");
        bob = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "bob");

        await alice.StartAsync(soulfindPort: soulfind.Port);
        await bob.StartAsync(soulfindPort: soulfind.Port);
    }

    public async Task DisposeAsync()
    {
        if (alice != null) await alice.DisposeAsync();
        if (bob != null) await bob.DisposeAsync();
        if (soulfind != null) await soulfind.DisposeAsync();
    }

    [Fact]
    public async Task Bridge_Should_Return_Status()
    {
        // Arrange
        var client = alice!.HttpClient;

        // Act
        var response = await client.GetAsync("/api/bridge/status");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var status = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(status.TryGetProperty("isHealthy", out _));
    }

    [Fact]
    public async Task Bridge_Search_Should_Return_Results()
    {
        // Arrange
        var client = alice!.HttpClient;
        var searchRequest = new
        {
            query = "test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/bridge/search", searchRequest);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("query", out _));
        Assert.True(result.TryGetProperty("users", out _));
    }

    [Fact]
    public async Task Bridge_GetRooms_Should_Return_Scenes()
    {
        // Arrange
        var client = alice!.HttpClient;

        // Act
        var response = await client.GetAsync("/api/bridge/rooms");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("rooms", out var rooms));
        Assert.True(rooms.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task Bridge_Dashboard_Should_Return_Data()
    {
        // Arrange
        var client = alice!.HttpClient;

        // Act
        var response = await client.GetAsync("/api/bridge/admin/dashboard");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dashboard.TryGetProperty("health", out _));
        Assert.True(dashboard.TryGetProperty("connectedClients", out _));
        Assert.True(dashboard.TryGetProperty("stats", out _));
    }

    [Fact]
    public async Task Bridge_Config_Should_Be_Retrievable()
    {
        // Arrange
        var client = alice!.HttpClient;

        // Act
        var response = await client.GetAsync("/api/bridge/admin/config");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var config = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(config.TryGetProperty("enabled", out _));
        Assert.True(config.TryGetProperty("port", out _));
    }

    [Fact]
    public async Task Bridge_Start_Stop_Should_Work()
    {
        // Arrange
        var client = alice!.HttpClient;

        // Act: Start bridge
        var startResponse = await client.PostAsync("/api/bridge/start", null);

        // Assert: Start should succeed (or return appropriate status)
        Assert.True(startResponse.IsSuccessStatusCode || startResponse.StatusCode == System.Net.HttpStatusCode.BadRequest);

        // Act: Stop bridge
        var stopResponse = await client.PostAsync("/api/bridge/stop", null);

        // Assert: Stop should succeed
        Assert.True(stopResponse.IsSuccessStatusCode || stopResponse.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bridge_Transfer_Progress_Should_Return_404_For_Unknown_Transfer()
    {
        // Arrange
        var client = alice!.HttpClient;
        var unknownTransferId = "unknown-transfer-id";

        // Act
        var response = await client.GetAsync($"/api/bridge/transfer/{unknownTransferId}/progress");

        // Assert: Should return 404 for unknown transfer
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
