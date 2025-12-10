namespace slskd.Tests.Integration.MultiClient;

using slskd.Tests.Integration.Harness;
using slskd.Tests.Integration.Fixtures;
using Xunit;

/// <summary>
/// L2 multi-client integration tests - Alice/Bob/Carol topology.
/// </summary>
[Trait("Category", "L2-MultiClient")]
public class MultiClientIntegrationTests : IAsyncLifetime
{
    private SoulfindRunner? soulfind;
    private SlskdnTestClient? alice;
    private SlskdnTestClient? bob;
    private SlskdnTestClient? carol;

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        soulfind = new SoulfindRunner(loggerFactory.CreateLogger<SoulfindRunner>());
        await soulfind.StartAsync();

        alice = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "alice");
        bob = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "bob");
        carol = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "carol");

        await alice.StartAsync(soulfindPort: soulfind.Port);
        await bob.StartAsync(soulfindPort: soulfind.Port);
        await carol.StartAsync(soulfindPort: soulfind.Port);
    }

    public async Task DisposeAsync()
    {
        if (alice != null) await alice.DisposeAsync();
        if (bob != null) await bob.DisposeAsync();
        if (carol != null) await carol.DisposeAsync();
        if (soulfind != null) await soulfind.DisposeAsync();
    }

    [Fact]
    public async Task Alice_Searches_Bob_Finds_Carol_Downloads()
    {
        // Arrange: Bob shares a file
        var testFile = AudioFixtures.GetTestFile("flac-lossless");
        await bob!.AddSharedFileAsync(testFile.Filename, testFile.Content);

        // Wait for share indexing
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act 1: Alice searches
        var searchResponse = await alice!.SearchAsync("test track lossless");
        Assert.True(searchResponse.IsSuccessStatusCode);

        // TODO: Parse search results, verify Bob's file appears

        // Act 2: Carol downloads from Bob
        var downloadResponse = await carol!.DownloadAsync("test-bob", testFile.Filename);
        Assert.True(downloadResponse.IsSuccessStatusCode);

        // Wait for download to complete
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert: File transferred
        // TODO: Verify download completed successfully
    }

    [Fact]
    public async Task Multi_Client_MBID_Mapping()
    {
        // Arrange: Multiple variants of same recording
        var flac = AudioFixtures.GetTestFile("flac-lossless");
        var mp3 = AudioFixtures.GetTestFile("mp3-320");

        await alice!.AddSharedFileAsync(flac.Filename, flac.Content);
        await bob!.AddSharedFileAsync(mp3.Filename, mp3.Content);

        // Wait for capture and normalization
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Act: Query shadow index
        // TODO: Query /api/virtualsoulfind/shadow-index/{mbid}

        // Assert: Both variants mapped to same MBID
        // Assert: Quality scores reflect codec differences
        // TODO: Implement assertions
    }

    [Fact]
    public async Task Multi_Client_Room_Coordination()
    {
        // Arrange: All clients join same room
        var roomName = "test-coordination";

        await alice!.HttpClient.PostAsJsonAsync("/api/rooms", new { room = roomName });
        await bob!.HttpClient.PostAsJsonAsync("/api/rooms", new { room = roomName });
        await carol!.HttpClient.PostAsJsonAsync("/api/rooms", new { room = roomName });

        // Wait for room join propagation
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act: Alice sends message
        // TODO: Implement chat message API

        // Assert: Bob and Carol receive message
        // TODO: Verify message propagation
    }

    [Fact]
    public async Task Multi_Client_Scene_Formation()
    {
        // Arrange: Create scene
        var sceneId = "scene:label:test-label";

        // Act: All clients join scene
        await alice!.HttpClient.PostAsJsonAsync("/api/virtualsoulfind/scenes/join", new { sceneId });
        await bob!.HttpClient.PostAsJsonAsync("/api/virtualsoulfind/scenes/join", new { sceneId });
        await carol!.HttpClient.PostAsJsonAsync("/api/virtualsoulfind/scenes/join", new { sceneId });

        // Wait for DHT announcements
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert: All clients see each other in scene
        var aliceMembers = await alice.HttpClient.GetAsync($"/api/virtualsoulfind/scenes/{sceneId}/members");
        // TODO: Verify 3 members returned
    }

    [Fact]
    public async Task Multi_Client_Quality_Score_Aggregation()
    {
        // Arrange: Multiple clients share same file with different quality
        var lossless = AudioFixtures.GetTestFile("flac-lossless");
        var lossy = AudioFixtures.GetTestFile("mp3-320");
        var transcode = AudioFixtures.GetTestFile("mp3-128-transcode");

        await alice!.AddSharedFileAsync(lossless.Filename, lossless.Content);
        await bob!.AddSharedFileAsync(lossy.Filename, lossy.Content);
        await carol!.AddSharedFileAsync(transcode.Filename, transcode.Content);

        // Wait for capture, normalization, shadow index
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act: Query canonical variant
        // TODO: Query /api/virtualsoulfind/canonical/{mbid}

        // Assert: FLAC selected as canonical
        // Assert: Transcode detected and scored lower
        // TODO: Implement assertions
    }
}
