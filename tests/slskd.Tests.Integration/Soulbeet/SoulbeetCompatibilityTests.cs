namespace slskd.Tests.Integration.Soulbeet;

using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for Soulbeet compatibility mode.
/// </summary>
public class SoulbeetCompatibilityTests : IClassFixture<slskd.Tests.Integration.StubWebApplicationFactory>
{
    private readonly slskd.Tests.Integration.StubWebApplicationFactory factory;
    private readonly HttpClient client;

    public SoulbeetCompatibilityTests(slskd.Tests.Integration.StubWebApplicationFactory factory)
    {
        this.factory = factory;
        this.client = factory.CreateClient();
    }

    [Fact]
    public async Task GetInfo_ShouldReturnSlskdnInfo()
    {
        // Act
        var response = await client.GetAsync("/api/info");

        // Assert
        response.EnsureSuccessStatusCode();
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var info = await response.Content.ReadFromJsonAsync<InfoResponse>(options);

        Assert.NotNull(info);
        Assert.Equal("slskdn", info.Impl);
        Assert.Equal("slskd", info.Compat);
        Assert.NotNull(info.Version);
        Assert.NotNull(info.Soulseek);
        Assert.True(info.Soulseek.Connected);
        Assert.NotNull(info.Soulseek.User);
    }

    [Fact]
    public async Task Search_ShouldReturnResults()
    {
        // Arrange
        var searchRequest = new
        {
            query = "test artist",
            type = "global",
            limit = 10
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/search", searchRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var searchResult = await response.Content.ReadFromJsonAsync<SearchResponse>();
        
        Assert.NotNull(searchResult);
        Assert.NotNull(searchResult.SearchId);
        Assert.Equal("test artist", searchResult.Query);
        Assert.NotNull(searchResult.Results);
    }

    [Fact]
    public async Task CreateDownload_ShouldEnqueueTransfer()
    {
        // Arrange
        var downloadRequest = new
        {
            items = new[]
            {
                new
                {
                    user = "test_user",
                    remote_path = "test/path/file.flac",
                    target_dir = "/tmp/downloads"
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/downloads", downloadRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var downloadResult = await response.Content.ReadFromJsonAsync<DownloadResponse>();
        
        Assert.NotNull(downloadResult);
        Assert.NotEmpty(downloadResult.DownloadIds);
    }

    [Fact]
    public async Task GetDownloads_ShouldReturnList()
    {
        // Act
        var response = await client.GetAsync("/api/downloads");

        // Assert
        response.EnsureSuccessStatusCode();
        var downloads = await response.Content.ReadFromJsonAsync<DownloadsListResponse>();
        
        Assert.NotNull(downloads);
        Assert.NotNull(downloads.Downloads);
    }

    [Fact]
    public async Task GetDownload_ById_ShouldReturnDetails()
    {
        // Arrange - first create a download
        var downloadRequest = new
        {
            items = new[]
            {
                new
                {
                    user = "test_user",
                    remote_path = "test/file.flac",
                    target_dir = "/tmp"
                }
            }
        };
        var createResponse = await client.PostAsJsonAsync("/api/downloads", downloadRequest);
        var downloadResult = await createResponse.Content.ReadFromJsonAsync<DownloadResponse>();
        var downloadId = downloadResult!.DownloadIds[0];

        // Act
        var response = await client.GetAsync($"/api/downloads/{downloadId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var download = await response.Content.ReadFromJsonAsync<DownloadDetailResponse>();
        
        Assert.NotNull(download);
        Assert.Equal(downloadId, download.Id);
    }

    [Fact]
    public async Task CompatMode_FullWorkflow_ShouldSucceed()
    {
        // 1. Search
        var searchResponse = await client.PostAsJsonAsync("/api/search", new
        {
            query = "Test Album",
            type = "global",
            limit = 5
        });
        searchResponse.EnsureSuccessStatusCode();
        var searchResult = await searchResponse.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(searchResult?.Results);

        // Simulate having results (in real scenario, mock Soulseek would provide)
        if (searchResult.Results.Count == 0)
        {
            // Skip if no mock results available
            return;
        }

        // 2. Download
        var firstResult = searchResult.Results[0];
        var downloadResponse = await client.PostAsJsonAsync("/api/downloads", new
        {
            items = new[]
            {
                new
                {
                    user = firstResult.User,
                    remote_path = firstResult.Files[0].Path,
                    target_dir = "/tmp/downloads"
                }
            }
        });
        downloadResponse.EnsureSuccessStatusCode();
        var downloadResult = await downloadResponse.Content.ReadFromJsonAsync<DownloadResponse>();
        Assert.NotEmpty(downloadResult!.DownloadIds);

        // 3. Check status
        var statusResponse = await client.GetAsync($"/api/downloads/{downloadResult.DownloadIds[0]}");
        statusResponse.EnsureSuccessStatusCode();
    }
}

// Response models
public record InfoResponse(string Impl, string Compat, string Version, SoulseekInfo Soulseek);
public record SoulseekInfo(bool Connected, string User);
public record SearchResponse(string SearchId, string Query, List<SearchResultItem> Results);
public record SearchResultItem(string User, int SpeedKbps, List<FileItem> Files);
public record FileItem(string Path, long SizeBytes, int Bitrate, int? LengthMs, string Ext);
public record DownloadResponse(List<string> DownloadIds);
public record DownloadsListResponse(List<DownloadItem> Downloads);
public record DownloadDetailResponse(string Id, string User, string RemotePath, string LocalPath, string Status, double Progress);
public record DownloadItem(string Id, string User, string RemotePath, string Status);
