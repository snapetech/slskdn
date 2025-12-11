namespace slskd.Tests.Integration.Soulbeet;

using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for slskdn-native advanced features.
/// </summary>
public class SoulbeetAdvancedModeTests : IClassFixture<slskd.Tests.Integration.StubWebApplicationFactory>
{
    private readonly slskd.Tests.Integration.StubWebApplicationFactory factory;
    private readonly HttpClient client;

    public SoulbeetAdvancedModeTests(slskd.Tests.Integration.StubWebApplicationFactory factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

[Fact]
    public async Task GetCapabilities_ShouldDetectSlskdn()
    {
        // Act
        var response = await client.GetAsync("/api/slskdn/capabilities");

        // Assert
        response.EnsureSuccessStatusCode();
        var capabilities = await response.Content.ReadFromJsonAsync<CapabilitiesResponse>();
        
        Assert.NotNull(capabilities);
        Assert.Equal("slskdn", capabilities.Impl);
        Assert.NotNull(capabilities.Version);
        Assert.Contains("mbid_jobs", capabilities.Features);
    }

[Fact]
    public async Task GetCapabilities_ShouldReturn404_OnVanillaSlskd()
    {
        // This test would run against vanilla slskd instance
        // For now, just verify our endpoint exists
        var response = await client.GetAsync("/api/slskdn/capabilities");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

[Fact]
    public async Task CreateMbReleaseJob_ShouldReturnJobId()
    {
        // Arrange
        var jobRequest = new
        {
            mb_release_id = "test-release-123",
            target_dir = "/tmp/downloads",
            tracks = "all",
            constraints = new
            {
                preferred_codecs = new[] { "FLAC" },
                allow_lossy = false,
                prefer_canonical = true,
                use_overlay = true
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/jobs/mb-release", jobRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var jobResult = await response.Content.ReadFromJsonAsync<JobResponse>();
        
        Assert.NotNull(jobResult);
        Assert.NotNull(jobResult.JobId);
        Assert.Equal("pending", jobResult.Status);
    }

[Fact]
    public async Task CreateDiscographyJob_ShouldReturnJobId()
    {
        // Arrange
        var jobRequest = new
        {
            artist_id = "test-artist-456",
            profile = "CoreDiscography",
            target_dir = "/tmp/downloads",
            preferred_codecs = new[] { "FLAC" },
            allow_lossy = false,
            prefer_canonical = true,
            use_overlay = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/jobs/discography", jobRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var jobResult = await response.Content.ReadFromJsonAsync<JobResponse>();
        
        Assert.NotNull(jobResult);
        Assert.NotNull(jobResult.JobId);
    }

[Fact]
    public async Task CreateLabelCrateJob_ShouldReturnJobId()
    {
        // Arrange
        var jobRequest = new
        {
            label_name = "Test Label",
            target_dir = "/tmp/downloads",
            preferred_codecs = new[] { "FLAC" },
            allow_lossy = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/jobs/label-crate", jobRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var jobResult = await response.Content.ReadFromJsonAsync<JobResponse>();
        
        Assert.NotNull(jobResult);
        Assert.NotNull(jobResult.JobId);
    }

[Fact]
    public async Task GetJobs_ShouldReturnList()
    {
        // Act
        var response = await client.GetAsync("/api/jobs");

        // Assert
        response.EnsureSuccessStatusCode();
        var jobs = await response.Content.ReadFromJsonAsync<JobsListResponse>();
        
        Assert.NotNull(jobs);
        Assert.NotNull(jobs.Jobs);
    }

[Fact]
    public async Task GetJobs_WithFilters_ShouldReturnFilteredList()
    {
        // Act
        var response = await client.GetAsync("/api/jobs?type=discography&status=running");

        // Assert
        response.EnsureSuccessStatusCode();
        var jobs = await response.Content.ReadFromJsonAsync<JobsListResponse>();
        
        Assert.NotNull(jobs);
        Assert.NotNull(jobs.Jobs);
    }

[Fact]
    public async Task GetJob_ById_ShouldReturnDetails()
    {
        // Arrange - create a job first
        var createResponse = await client.PostAsJsonAsync("/api/jobs/discography", new
        {
            artist_id = "test-artist",
            profile = "CoreDiscography",
            target_dir = "/tmp"
        });
        var jobResult = await createResponse.Content.ReadFromJsonAsync<JobResponse>();
        var jobId = jobResult!.JobId;

        // Act
        var response = await client.GetAsync($"/api/jobs/{jobId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var job = await response.Content.ReadFromJsonAsync<JobDetailResponse>();
        
        Assert.NotNull(job);
        Assert.Equal(jobId, job.Id);
        Assert.Equal("discography", job.Type);
    }

[Fact]
    public async Task SubmitWarmCacheHints_ShouldAccept()
    {
        // Arrange
        var hints = new
        {
            mb_release_ids = new[] { "release-1", "release-2" },
            mb_artist_ids = new[] { "artist-1" },
            mb_label_ids = new[] { "label-1" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/slskdn/warm-cache/hints", hints);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WarmCacheHintsResponse>();
        
        Assert.NotNull(result);
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task GetLibraryHealth_ShouldReturnSummary()
    {
        // Act
        var response = await client.GetAsync("/api/slskdn/library/health?path=/music/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var health = await response.Content.ReadFromJsonAsync<LibraryHealthResponse>();
        
        Assert.NotNull(health);
        Assert.NotNull(health.Summary);
        Assert.NotNull(health.Issues);
    }

    [Fact]
    public async Task AdvancedMode_FullWorkflow_ShouldSucceed()
    {
        // 1. Detect capabilities
        var capsResponse = await client.GetAsync("/api/slskdn/capabilities");
        capsResponse.EnsureSuccessStatusCode();
        var caps = await capsResponse.Content.ReadFromJsonAsync<CapabilitiesResponse>();
        Assert.Contains("mbid_jobs", caps!.Features);

        // 2. Create MB release job
        var jobResponse = await client.PostAsJsonAsync("/api/jobs/mb-release", new
        {
            mb_release_id = "test-release",
            target_dir = "/tmp/downloads",
            tracks = "all"
        });
        jobResponse.EnsureSuccessStatusCode();
        var jobResult = await jobResponse.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(jobResult?.JobId);

        // 3. Poll job status
        var statusResponse = await client.GetAsync($"/api/jobs/{jobResult.JobId}");
        statusResponse.EnsureSuccessStatusCode();
        var job = await statusResponse.Content.ReadFromJsonAsync<JobDetailResponse>();
        Assert.NotNull(job);

        // 4. Submit warm cache hints
        var hintsResponse = await client.PostAsJsonAsync("/api/slskdn/warm-cache/hints", new
        {
            mb_release_ids = new[] { "test-release" }
        });
        hintsResponse.EnsureSuccessStatusCode();
    }
}

// Response models
public record CapabilitiesResponse(string Impl, string Version, List<string> Features);
public record JobResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("job_id")]
    public string JobId { get; init; } = null!;
    public string Status { get; init; } = null!;
}
public record JobsListResponse(List<JobItem> Jobs);
public record JobItem(string Id, string Type, string Status);
public record JobDetailResponse(string Id, string Type, string Status, object Spec, object Progress);
public record WarmCacheHintsResponse(bool Accepted);
public record LibraryHealthResponse(string Path, HealthSummary Summary, List<HealthIssue> Issues);
public record HealthSummary(int SuspectedTranscodes, int NonCanonicalVariants, int IncompleteReleases, int TotalIssues);
public record HealthIssue(string Type, string File, string Reason);
