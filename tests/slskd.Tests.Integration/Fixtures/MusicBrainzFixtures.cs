namespace slskd.Tests.Integration.Fixtures;

using slskd.Integrations.MusicBrainz;

/// <summary>
/// MusicBrainz stub responses for testing.
/// </summary>
public static class MusicBrainzFixtures
{
    /// <summary>
    /// Get stubbed MusicBrainz recording response.
    /// </summary>
    public static MbRecording GetTestRecording(string id = "test-recording-1")
    {
        return new MbRecording
        {
            Id = id,
            Title = "Around The World",
            Length = 429000, // 7:09
            ArtistCredit = new List<MbArtistCredit>
            {
                new MbArtistCredit
                {
                    Name = "Daft Punk",
                    Artist = new MbArtist
                    {
                        Id = "test-artist-1",
                        Name = "Daft Punk",
                        SortName = "Daft Punk"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get stubbed MusicBrainz release response.
    /// </summary>
    public static MbRelease GetTestRelease(string id = "test-release-1")
    {
        return new MbRelease
        {
            Id = id,
            Title = "Homework",
            Date = "1997-01-20",
            Country = "FR",
            LabelInfo = new List<MbLabelInfo>
            {
                new MbLabelInfo
                {
                    Label = new MbLabel
                    {
                        Id = "test-label-1",
                        Name = "Virgin Records"
                    }
                }
            },
            Media = new List<MbMedium>
            {
                new MbMedium
                {
                    Position = 1,
                    Format = "CD",
                    TrackCount = 16,
                    Tracks = new List<MbTrack>
                    {
                        new MbTrack
                        {
                            Id = "test-track-1",
                            Position = 4,
                            Number = "4",
                            Title = "Around The World",
                            Length = 429000,
                            Recording = GetTestRecording()
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get stubbed search results.
    /// </summary>
    public static MbSearchResults<MbRecording> GetTestSearchResults()
    {
        return new MbSearchResults<MbRecording>
        {
            Count = 1,
            Offset = 0,
            Results = new List<MbRecording>
            {
                GetTestRecording()
            }
        };
    }

    /// <summary>
    /// Get JSON fixture for recording.
    /// </summary>
    public static string GetRecordingJson(string id = "test-recording-1")
    {
        return $$"""
        {
          "id": "{{id}}",
          "title": "Around The World",
          "length": 429000,
          "artist-credit": [
            {
              "name": "Daft Punk",
              "artist": {
                "id": "test-artist-1",
                "name": "Daft Punk",
                "sort-name": "Daft Punk"
              }
            }
          ]
        }
        """;
    }

    /// <summary>
    /// Get JSON fixture for release.
    /// </summary>
    public static string GetReleaseJson(string id = "test-release-1")
    {
        return $$"""
        {
          "id": "{{id}}",
          "title": "Homework",
          "date": "1997-01-20",
          "country": "FR",
          "label-info": [
            {
              "label": {
                "id": "test-label-1",
                "name": "Virgin Records"
              }
            }
          ],
          "media": [
            {
              "position": 1,
              "format": "CD",
              "track-count": 16,
              "tracks": [
                {
                  "id": "test-track-1",
                  "position": 4,
                  "number": "4",
                  "title": "Around The World",
                  "length": 429000
                }
              ]
            }
          ]
        }
        """;
    }
}

/// <summary>
/// Mock MusicBrainz client for testing.
/// </summary>
public class MockMusicBrainzClient : IMusicBrainzClient
{
    private readonly Dictionary<string, MbRecording> recordings = new();
    private readonly Dictionary<string, MbRelease> releases = new();

    public MockMusicBrainzClient()
    {
        // Add default test data
        var recording = MusicBrainzFixtures.GetTestRecording();
        var release = MusicBrainzFixtures.GetTestRelease();
        
        recordings[recording.Id] = recording;
        releases[release.Id] = release;
    }

    public Task<MbRecording?> GetRecordingAsync(string mbid, CancellationToken ct = default)
    {
        recordings.TryGetValue(mbid, out var recording);
        return Task.FromResult(recording);
    }

    public Task<MbRelease?> GetReleaseAsync(string mbid, CancellationToken ct = default)
    {
        releases.TryGetValue(mbid, out var release);
        return Task.FromResult(release);
    }

    public Task<MbSearchResults<MbRecording>?> SearchRecordingAsync(string query, CancellationToken ct = default)
    {
        return Task.FromResult<MbSearchResults<MbRecording>?>(MusicBrainzFixtures.GetTestSearchResults());
    }

    public Task<MbSearchResults<MbRelease>?> SearchReleaseAsync(string query, CancellationToken ct = default)
    {
        return Task.FromResult<MbSearchResults<MbRelease>?>(new MbSearchResults<MbRelease>
        {
            Count = 1,
            Results = new List<MbRelease> { MusicBrainzFixtures.GetTestRelease() }
        });
    }

    public void AddRecording(MbRecording recording)
    {
        recordings[recording.Id] = recording;
    }

    public void AddRelease(MbRelease release)
    {
        releases[release.Id] = release;
    }
}

/// <summary>
/// Test helper to inject mock MusicBrainz client.
/// </summary>
public static class MusicBrainzTestHelper
{
    public static IServiceCollection AddMockMusicBrainz(this IServiceCollection services)
    {
        services.AddSingleton<IMusicBrainzClient, MockMusicBrainzClient>();
        return services;
    }
}
