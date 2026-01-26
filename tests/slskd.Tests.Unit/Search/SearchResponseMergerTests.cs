// <copyright file="SearchResponseMergerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Search;

using System.Collections.Generic;
using System.Linq;
using slskd.Search;
using Xunit;

public class SearchResponseMergerTests
{
    [Fact]
    public void Deduplicate_NormalizesFilenames()
    {
        var soulseekResponses = new List<Response>
        {
            new Response
            {
                Username = "user1",
                Files = new List<File>
                {
                    new File { Filename = "Test\\File.mp3", Size = 1000 },
                    new File { Filename = "UPPER.mp3", Size = 2000 }
                }
            }
        };
        var meshResponses = new List<Response>
        {
            new Response
            {
                Username = "user2",
                Files = new List<File>
                {
                    new File { Filename = "test/file.mp3", Size = 1000 }, // Same after normalization
                    new File { Filename = "upper.mp3", Size = 2000 } // Same after normalization
                }
            }
        };

        var result = SearchResponseMerger.Deduplicate(soulseekResponses, meshResponses);

        // Should deduplicate based on normalized filename
        Assert.Equal(2, result.Count);
        // user1's files should be kept (first occurrence)
        var user1Files = result.First().Files;
        Assert.Contains(user1Files, f => f.Filename == "Test\\File.mp3");
        Assert.Contains(user1Files, f => f.Filename == "UPPER.mp3");
    }

    [Fact]
    public void Deduplicate_HandlesCaseInsensitive()
    {
        // The merger deduplicates across ALL responses (soulseek + mesh) based on (Username, normalized filename, size)
        // This test verifies that normalization is applied (lowercase conversion)
        // Test with files in separate responses to verify cross-response deduplication
        var soulseekResponses = new List<Response>
        {
            new Response
            {
                Username = "user1",
                Files = new List<File> { new File { Filename = "Song.mp3", Size = 1000 } },
                Token = 1,
                HasFreeUploadSlot = false,
                UploadSpeed = 0,
                QueueLength = 0
            }
        };
        var meshResponses = new List<Response>
        {
            new Response
            {
                Username = "user1", // Same user
                Files = new List<File> { new File { Filename = "song.mp3", Size = 1000 } }, // Same after normalization
                Token = 2,
                HasFreeUploadSlot = false,
                UploadSpeed = 0,
                QueueLength = 0
            }
        };

        var result = SearchResponseMerger.Deduplicate(soulseekResponses, meshResponses);

        // The implementation processes each response and deduplicates files globally
        // Both "Song.mp3" and "song.mp3" normalize to "song.mp3" for user1
        // First response (soulseek) processes first:
        //   - File "Song.mp3" normalizes to "song.mp3"
        //   - seenByFilename.Add(("user1", "song.mp3", 1000)) -> returns true (first time)
        //   - File is kept, response is added to result
        // Second response (mesh) processes:
        //   - File "song.mp3" normalizes to "song.mp3"
        //   - seenByFilename.Add(("user1", "song.mp3", 1000)) -> should return false (already seen!)
        //   - File is NOT kept, keptFiles.Count = 0
        //   - Response is NOT added to result (only responses with keptFiles > 0 are added)
        // So we should get 1 response (the first one), not 2
        Assert.Single(result); // Only one response should be returned (second was empty and not added)
        Assert.Equal("user1", result[0].Username);
        Assert.Single(result[0].Files); // Only one file kept (deduplicated)
        Assert.Equal("Song.mp3", result[0].Files.First().Filename); // First occurrence kept
    }

    [Fact]
    public void Deduplicate_HandlesPathSeparators()
    {
        // Path separator normalization works within the same user
        // This test verifies that path separators are normalized (backslash to forward slash)
        var soulseekResponses = new List<Response>
        {
            new Response
            {
                Username = "user1",
                Files = new List<File>
                {
                    new File { Filename = "Music\\Song.mp3", Size = 1000 },
                    new File { Filename = "Music/Song.mp3", Size = 1000 } // Same after normalization
                },
                Token = 1,
                HasFreeUploadSlot = false,
                UploadSpeed = 0,
                QueueLength = 0
            }
        };
        var meshResponses = new List<Response>();

        var result = SearchResponseMerger.Deduplicate(soulseekResponses, meshResponses);

        // The implementation should normalize path separators and deduplicate
        // Both "Music\Song.mp3" and "Music/Song.mp3" normalize to "music/song.mp3", so they should be deduplicated
        Assert.Single(result); // One response should be returned
        var user1Response = result.First();
        Assert.Equal("user1", user1Response.Username);
        // The files should be deduplicated (normalized paths match)
        // First file "Music\Song.mp3" is added (normalized: "music/song.mp3")
        // Second file "Music/Song.mp3" normalizes to "music/song.mp3" which is already seen, so it's skipped
        Assert.Single(user1Response.Files); // Only one file kept (deduplicated)
    }

    [Fact]
    public void Deduplicate_DifferentSizes_KeepsBoth()
    {
        var soulseekResponses = new List<Response>
        {
            new Response
            {
                Username = "user1",
                Files = new List<File>
                {
                    new File { Filename = "song.mp3", Size = 1000 }
                }
            }
        };
        var meshResponses = new List<Response>
        {
            new Response
            {
                Username = "user2",
                Files = new List<File>
                {
                    new File { Filename = "song.mp3", Size = 2000 } // Different size
                }
            }
        };

        var result = SearchResponseMerger.Deduplicate(soulseekResponses, meshResponses);

        // Different sizes should not be deduplicated
        Assert.Equal(2, result.Count);
        Assert.Single(result.First().Files);
        Assert.Single(result.Skip(1).First().Files);
    }

    [Fact]
    public void Deduplicate_EmptyResponses_ReturnsEmpty()
    {
        var result = SearchResponseMerger.Deduplicate(new List<Response>(), new List<Response>());

        Assert.Empty(result);
    }
}
