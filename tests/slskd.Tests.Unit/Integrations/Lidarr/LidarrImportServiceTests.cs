// <copyright file="LidarrImportServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.Lidarr;

using System.Text.Json;
using slskd.Events;
using slskd.Integrations.Lidarr;
using Xunit;

public class LidarrImportServiceTests
{
    [Fact]
    public async Task ImportCompletedDirectoryAsync_WithSafeCandidate_QueuesManualImport()
    {
        var client = new FakeLidarrClient
        {
            Candidates =
            [
                SafeCandidate(),
            ],
        };
        var service = CreateService(
            client,
            new Options.IntegrationOptions.LidarrOptions
            {
                Enabled = true,
                Url = "http://lidarr.test",
                ApiKey = "key",
                AutoImportCompleted = true,
                ImportMode = "copy",
                ImportReplaceExistingFiles = true,
            });

        var result = await service.ImportCompletedDirectoryAsync("/downloads/music/Artist/Album");

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.SafeCandidateCount);
        Assert.Equal(42, result.CommandId);
        Assert.Equal("Copy", result.ImportMode);
        Assert.Single(client.ImportedFiles);
        Assert.True(client.ImportedFiles[0].ReplaceExistingFiles);
        Assert.Equal("Copy", client.LastImportMode);
    }

    [Fact]
    public async Task ImportCompletedDirectoryAsync_WithAmbiguousCandidate_SkipsImport()
    {
        var client = new FakeLidarrClient
        {
            Candidates =
            [
                RejectedCandidate(),
            ],
        };
        var service = CreateService(client, EnabledImportOptions());

        var result = await service.ImportCompletedDirectoryAsync("/downloads/music/Artist/Album");

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(0, result.SafeCandidateCount);
        Assert.Equal("Lidarr candidates had rejections or ambiguous matches", result.SkippedReason);
        Assert.Empty(client.ImportedFiles);
    }

    [Fact]
    public async Task ImportCompletedDirectoryAsync_MapsOnlyPathBoundaryMatches()
    {
        var client = new FakeLidarrClient();
        var service = CreateService(
            client,
            new Options.IntegrationOptions.LidarrOptions
            {
                Enabled = true,
                Url = "http://lidarr.test",
                ApiKey = "key",
                AutoImportCompleted = true,
                ImportPathFrom = "/downloads/music",
                ImportPathTo = "/lidarr/inbox",
            });

        await service.ImportCompletedDirectoryAsync("/downloads/music2/Artist/Album");

        Assert.Equal(Path.GetFullPath("/downloads/music2/Artist/Album"), client.LastCandidateFolder);
    }

    [Fact]
    public async Task ImportCompletedDirectoryAsync_MapsChildPath()
    {
        var client = new FakeLidarrClient();
        var service = CreateService(
            client,
            new Options.IntegrationOptions.LidarrOptions
            {
                Enabled = true,
                Url = "http://lidarr.test",
                ApiKey = "key",
                AutoImportCompleted = true,
                ImportPathFrom = "/downloads/music",
                ImportPathTo = "/lidarr/inbox",
            });

        await service.ImportCompletedDirectoryAsync("/downloads/music/Artist/Album");

        Assert.Equal("/lidarr/inbox/Artist/Album", client.LastCandidateFolder);
    }

    private static LidarrImportService CreateService(FakeLidarrClient client, Options.IntegrationOptions.LidarrOptions lidarrOptions)
        => new(
            client,
            new EventBus(null!),
            new TestOptionsMonitor<Options>(new Options
            {
                Integration = new Options.IntegrationOptions
                {
                    Lidarr = lidarrOptions,
                },
            }));

    private static Options.IntegrationOptions.LidarrOptions EnabledImportOptions()
        => new()
        {
            Enabled = true,
            Url = "http://lidarr.test",
            ApiKey = "key",
            AutoImportCompleted = true,
        };

    private static LidarrManualImportResource SafeCandidate()
        => new()
        {
            Id = 123,
            Path = "/downloads/music/Artist/Album/01 Track.flac",
            Artist = new LidarrArtistResource { Id = 1, ArtistName = "Artist" },
            Album = new LidarrAlbumResource { Id = 2, Title = "Album" },
            AlbumReleaseId = 3,
            Tracks = [new LidarrTrackResource { Id = 4, Title = "Track" }],
            Quality = JsonSerializer.Deserialize<JsonElement>("{}"),
        };

    private static LidarrManualImportResource RejectedCandidate()
        => new()
        {
            Id = 123,
            Path = "/downloads/music/Artist/Album/01 Track.flac",
            Artist = new LidarrArtistResource { Id = 1, ArtistName = "Artist" },
            Album = new LidarrAlbumResource { Id = 2, Title = "Album" },
            AlbumReleaseId = 3,
            Tracks = [new LidarrTrackResource { Id = 4, Title = "Track" }],
            Quality = JsonSerializer.Deserialize<JsonElement>("{}"),
            Rejections = [JsonSerializer.Deserialize<JsonElement>("\"ambiguous\"")],
        };

    private sealed class FakeLidarrClient : ILidarrClient
    {
        public IReadOnlyList<LidarrManualImportResource> Candidates { get; init; } = [];

        public string LastCandidateFolder { get; private set; } = string.Empty;

        public string LastImportMode { get; private set; } = string.Empty;

        public List<LidarrManualImportResource> ImportedFiles { get; } = [];

        public Task<LidarrSystemStatus> GetSystemStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new LidarrSystemStatus());

        public Task<IReadOnlyList<LidarrWantedAlbum>> GetWantedMissingAsync(int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LidarrWantedAlbum>>([]);

        public Task<IReadOnlyList<LidarrManualImportResource>> GetManualImportCandidatesAsync(
            string folder,
            bool filterExistingFiles,
            bool replaceExistingFiles,
            CancellationToken cancellationToken = default)
        {
            LastCandidateFolder = folder;
            return Task.FromResult(Candidates);
        }

        public Task<LidarrCommandResponse> StartManualImportAsync(
            IReadOnlyList<LidarrManualImportResource> files,
            string importMode,
            bool replaceExistingFiles,
            CancellationToken cancellationToken = default)
        {
            LastImportMode = importMode;
            ImportedFiles.AddRange(files);
            return Task.FromResult(new LidarrCommandResponse { Id = 42, Name = "ManualImport", Status = "queued" });
        }

        public Task<LidarrCommandResponse> StartCommandAsync(string name, object payload, CancellationToken cancellationToken = default)
            => Task.FromResult(new LidarrCommandResponse { Id = 42, Name = name, Status = "queued" });
    }
}
