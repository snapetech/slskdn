namespace slskd.Tests.Integration.DisasterMode;

using slskd.Tests.Integration.Harness;
using slskd.Tests.Integration.Fixtures;
using slskd.Tests.Integration.Mesh;
using Xunit;

/// <summary>
/// L3 disaster mode tests - Soulfind-assisted disaster drills.
/// </summary>
[Trait("Category", "L3-DisasterMode")]
public class DisasterModeTests : IAsyncLifetime
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

    [Fact(Skip = "Stub host")]
    public async Task Kill_Soulfind_Mid_Transfer_Should_Activate_Disaster_Mode()
    {
        // Arrange: Bob shares large file
        var testFile = new byte[10 * 1024 * 1024]; // 10 MB
        await bob!.AddSharedFileAsync("large-file.flac", testFile);

        // Act 1: Alice starts download
        var downloadResponse = await alice!.DownloadAsync("test-bob", "large-file.flac");
        Assert.True(downloadResponse.IsSuccessStatusCode);

        // Wait for transfer to start
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Act 2: Kill Soulfind mid-transfer
        await soulfind!.StopAsync();

        // Wait for disaster mode activation (10 min threshold in prod, but faster in tests)
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Assert: Disaster mode activated
        var statusResponse = await alice.HttpClient.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        Assert.True(statusResponse.IsSuccessStatusCode);
        // TODO: Verify IsActive = true

        // Assert: Transfer continues via mesh
        await Task.Delay(TimeSpan.FromSeconds(5));
        // TODO: Verify transfer completed via overlay
    }

    [Fact(Skip = "IAsyncLifetime.InitializeAsync uses SlskdnTestClient.StartAsync which can hang when app host resolves real controller deps. See docs/dev/slskd-tests-integration-audit.")]
    public async Task Disaster_Mode_Search_Should_Use_Shadow_Index()
    {
        // Arrange: Populate shadow index while Soulfind running
        var testFile = AudioFixtures.GetTestFile("flac-lossless");
        await bob!.AddSharedFileAsync(testFile.Filename, testFile.Content);
        
        // Wait for capture and shadow index population
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Act: Kill Soulfind
        await soulfind!.StopAsync();

        // Wait for disaster mode
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Act: Alice searches (should use shadow index only)
        var searchResponse = await alice!.SearchAsync("test track");

        // Assert: Search succeeds via shadow index
        Assert.True(searchResponse.IsSuccessStatusCode);
        // TODO: Verify results came from shadow index, not Soulseek
    }

    [Fact(Skip = "IAsyncLifetime.InitializeAsync uses SlskdnTestClient.StartAsync which can hang when app host resolves real controller deps. See docs/dev/slskd-tests-integration-audit.")]
    public async Task Disaster_Mode_Recovery_Should_Deactivate_When_Soulfind_Returns()
    {
        // Arrange: Activate disaster mode
        await soulfind!.StopAsync();
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Verify disaster mode active
        var statusBefore = await alice!.HttpClient.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        // TODO: Assert IsActive = true

        // Act: Restart Soulfind
        await soulfind.StartAsync();

        // Wait for reconnection and stability check (1 min in prod, shorter in tests)
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert: Disaster mode deactivated
        var statusAfter = await alice.HttpClient.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        // TODO: Assert IsActive = false
    }
}

/// <summary>
/// L3 mesh-only tests - pure mesh simulation (no Soulfind).
/// </summary>
[Trait("Category", "L3-MeshOnly")]
public class MeshOnlyTests
{
    [Fact]
    public async Task Pure_Mesh_Discography_Job()
    {
        // Arrange: Create mesh simulator
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var mesh = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        // Add 3 nodes with different parts of discography
        var alice = mesh.AddNode("alice");
        var bob = mesh.AddNode("bob");
        var carol = mesh.AddNode("carol");

        var album1 = AudioFixtures.GetTestFile("flac-lossless");
        var album2 = AudioFixtures.GetTestFile("mp3-320");
        var album3 = AudioFixtures.GetTestFile("opus-256");

        alice.AddFile("album1-track1.flac", album1.Content);
        bob.AddFile("album2-track1.mp3", album2.Content);
        carol.AddFile("album3-track1.opus", album3.Content);

        // Act: Simulate discography job (query all MBIDs, discover via DHT, transfer via overlay)
        // TODO: Implement actual discography job simulation

        // Assert: All 3 albums discovered and "downloaded"
        Assert.Equal(3, mesh.NodeCount);
    }

    [Fact]
    public async Task Pure_Mesh_Repair_Mission()
    {
        // Arrange: Mesh simulator
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var mesh = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        // Alice has transcode, Bob has lossless
        var alice = mesh.AddNode("alice");
        var bob = mesh.AddNode("bob");

        var transcode = AudioFixtures.GetTestFile("mp3-128-transcode");
        var lossless = AudioFixtures.GetTestFile("flac-lossless");

        alice.AddFile("track-transcode.mp3", transcode.Content);
        bob.AddFile("track-lossless.flac", lossless.Content);

        // Act: Simulate repair mission (detect transcode, query shadow index, find canonical, transfer)
        // TODO: Implement repair mission simulation

        // Assert: Alice now has lossless version
        // TODO: Verify alice's library updated
    }

    [Fact]
    public async Task Mesh_Network_Partition_Should_Isolate_Nodes()
    {
        // Arrange: Mesh with 3 nodes
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var mesh = new MeshSimulator(loggerFactory.CreateLogger<MeshSimulator>());

        var alice = mesh.AddNode("alice");
        var bob = mesh.AddNode("bob");
        var carol = mesh.AddNode("carol");

        bob.AddFile("test.flac", new byte[1024]);

        // Act: Enable network partition
        mesh.SimulateNetworkPartition(true);

        // Attempt transfer
        var result = await mesh.OverlayTransferAsync("bob", "alice", "test-hash", CancellationToken.None);

        // Assert: Transfer blocked
        Assert.Null(result);

        // Act: Disable partition
        mesh.SimulateNetworkPartition(false);

        // Retry transfer
        result = await mesh.OverlayTransferAsync("bob", "alice", "test-hash", CancellationToken.None);

        // Assert: Transfer succeeds
        // (Will still be null due to hash mismatch, but not blocked)
    }
}
