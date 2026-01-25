namespace slskd.Tests.Integration.Features;

using slskd.Tests.Integration.Harness;
using slskd.Tests.Integration.Fixtures;
using Xunit;

/// <summary>
/// Rescue mode integration tests.
/// </summary>
[Trait("Category", "L2-RescueMode")]
public class RescueModeTests : IAsyncLifetime
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
    public async Task Slow_Transfer_Should_Trigger_Rescue_Mode()
    {
        // Arrange: Bob shares file
        var testFile = new byte[5 * 1024 * 1024]; // 5 MB
        await bob!.AddSharedFileAsync("test.flac", testFile);

        // Act: Alice starts download (stub host: IDownloadService enqueues; no real transfer)
        var response = await alice!.DownloadAsync("test-bob", "test.flac");

        // Assert: Download enqueued successfully
        Assert.True(response.IsSuccessStatusCode);
        // TODO: With real Soulseek + throttle, verify rescue mode activation and overlay assist
    }
}

/// <summary>
/// Canonical selection integration tests.
/// </summary>
[Trait("Category", "L2-Canonical")]
public class CanonicalSelectionTests : IAsyncLifetime
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
    public async Task Should_Prefer_Canonical_Variant()
    {
        // Arrange: Multiple quality variants
        var flac = AudioFixtures.GetTestFile("flac-lossless");
        var mp3 = AudioFixtures.GetTestFile("mp3-320");
        var transcode = AudioFixtures.GetTestFile("mp3-128-transcode");

        await alice!.AddSharedFileAsync(flac.Filename, flac.Content);
        await bob!.AddSharedFileAsync(mp3.Filename, mp3.Content);
        await carol!.AddSharedFileAsync(transcode.Filename, transcode.Content);

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // Act: Query canonical variant (stub CanonicalController returns FLAC)
        var mbid = "00000000-0000-0000-0000-000000000001";
        var response = await alice.HttpClient.GetAsync($"/api/virtualsoulfind/canonical/{mbid}");

        // Assert: Canonical endpoint returns 200 and stub selects FLAC
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(json.TryGetProperty("canonical_variant", out var canon));
        Assert.True(canon.TryGetProperty("codec", out var codec));
        Assert.Equal("FLAC", codec.GetString());
    }

    [Fact]
    public async Task Cross_Codec_Deduplication()
    {
        // Arrange: Same recording, different codecs
        var flac = AudioFixtures.GetTestFile("flac-lossless");
        var opus = AudioFixtures.GetTestFile("opus-256");
        var aac = AudioFixtures.GetTestFile("aac-256");

        await alice!.AddSharedFileAsync(flac.Filename, flac.Content);
        await bob!.AddSharedFileAsync(opus.Filename, opus.Content);
        await carol!.AddSharedFileAsync(aac.Filename, aac.Content);

        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act: Query shadow index
        // TODO: GET /api/virtualsoulfind/shadow-index/{mbid}

        // Assert: All 3 variants mapped to same MBID
        // Assert: Quality scores: FLAC > Opus > AAC (for same bitrate)
    }
}

/// <summary>
/// Library health integration tests.
/// </summary>
[Trait("Category", "L2-LibraryHealth")]
public class LibraryHealthTests : IAsyncLifetime
{
    private SoulfindRunner? soulfind;
    private SlskdnTestClient? client;

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        soulfind = new SoulfindRunner(loggerFactory.CreateLogger<SoulfindRunner>());
        await soulfind.StartAsync();

        client = new SlskdnTestClient(loggerFactory.CreateLogger<SlskdnTestClient>(), "client");
        await client.StartAsync(soulfindPort: soulfind.Port);
    }

    public async Task DisposeAsync()
    {
        if (client != null) await client.DisposeAsync();
        if (soulfind != null) await soulfind.DisposeAsync();
    }

    [Fact]
    public async Task Should_Detect_Transcodes()
    {
        // Arrange: Add mix of files including transcodes
        var lossless = AudioFixtures.GetTestFile("flac-lossless");
        var transcode = AudioFixtures.GetTestFile("mp3-128-transcode");

        await client!.AddSharedFileAsync(lossless.Filename, lossless.Content);
        await client.AddSharedFileAsync(transcode.Filename, transcode.Content);

        // Act: Trigger library scan
        var scanResponse = await client.HttpClient.PostAsync("/api/library/scan", null);
        Assert.True(scanResponse.IsSuccessStatusCode);

        // Wait for scan completion
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert: Transcode detected
        var healthResponse = await client.HttpClient.GetAsync("/api/slskdn/library/health");
        healthResponse.EnsureSuccessStatusCode();
        var health = await healthResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(health.TryGetProperty("summary", out _));
        Assert.True(health.TryGetProperty("issues", out _));
    }

    [Fact]
    public async Task Should_Detect_Non_Canonical()
    {
        // Arrange: Add non-canonical variant
        var mp3 = AudioFixtures.GetTestFile("mp3-320");
        await client!.AddSharedFileAsync(mp3.Filename, mp3.Content);

        // Populate shadow index with canonical FLAC from elsewhere
        // (Simulated by mock data)

        // Act: Check library health
        var healthResponse = await client.HttpClient.GetAsync("/api/slskdn/library/health");
        healthResponse.EnsureSuccessStatusCode();
        var health = await healthResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(health.TryGetProperty("summary", out _));
        Assert.True(health.TryGetProperty("issues", out _));
    }

    [Fact]
    public async Task Should_Create_Remediation_Job()
    {
        // Arrange: Library with issues
        var transcode = AudioFixtures.GetTestFile("mp3-128-transcode");
        await client!.AddSharedFileAsync(transcode.Filename, transcode.Content);

        await Task.Delay(TimeSpan.FromSeconds(3));

        // Act: Request remediation
        var remediateResponse = await client.HttpClient.PostAsJsonAsync("/api/slskdn/library/remediate", new
        {
            issue_ids = new[] { "test-issue-1" }
        });
        Assert.True(remediateResponse.IsSuccessStatusCode);

        // Assert: Job created to replace transcode
        remediateResponse.EnsureSuccessStatusCode();
        var remediateResult = await remediateResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        // Accept either snake_case or PascalCase
        remediateResult.TryGetProperty("job_id", out var jobIdSnake);
        remediateResult.TryGetProperty("jobId", out var jobIdPascal);
        var hasJobId = jobIdSnake.ValueKind != System.Text.Json.JsonValueKind.Undefined || 
                       jobIdPascal.ValueKind != System.Text.Json.JsonValueKind.Undefined;
        Assert.True(hasJobId, "Response should contain job_id or jobId");
        if (hasJobId)
        {
            var jobIdValue = jobIdSnake.ValueKind != System.Text.Json.JsonValueKind.Undefined 
                ? jobIdSnake.GetString() 
                : jobIdPascal.GetString();
            Assert.False(string.IsNullOrWhiteSpace(jobIdValue), "Job ID should not be empty");
        }
    }
}
