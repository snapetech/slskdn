// <copyright file="SearchResponseMergerTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Search;

using System.Collections.Generic;
using System.Linq;
using slskd.Search;
using Xunit;

/// <summary>
/// Unit tests for SearchResponseMerger.Deduplicate: mesh-disabled/0-peers same as baseline, mesh adds without altering Soulseek, dedup by (Username, Filename, Size).
/// </summary>
public class SearchResponseMergerTests
{
    private static Response R(string username, params (string Filename, long Size)[] files)
    {
        var flist = files.Select(f => new File { Filename = f.Filename, Size = f.Size, Extension = ".flac", Code = 1, IsLocked = false }).ToList();
        return new Response
        {
            Username = username,
            Token = 0,
            HasFreeUploadSlot = true,
            UploadSpeed = 0,
            QueueLength = 0,
            FileCount = flist.Count,
            Files = flist,
            LockedFileCount = 0,
            LockedFiles = new List<File>(),
        };
    }

    [Fact]
    public void Deduplicate_MeshEmpty_ReturnsSoulseekOnly()
    {
        var soulseek = new[] { R("u1", ("a.flac", 100)) };
        var mesh = new List<Response>();

        var merged = SearchResponseMerger.Deduplicate(soulseek, mesh);

        Assert.Single(merged);
        Assert.Equal("u1", merged[0].Username);
        Assert.Single(merged[0].Files);
        Assert.Equal("a.flac", merged[0].Files!.First().Filename);
    }

    [Fact]
    public void Deduplicate_MeshOnly_ReturnsMeshOnly()
    {
        var soulseek = Enumerable.Empty<Response>();
        var mesh = new List<Response> { R("u2", ("b.flac", 200)) };

        var merged = SearchResponseMerger.Deduplicate(soulseek, mesh);

        Assert.Single(merged);
        Assert.Equal("u2", merged[0].Username);
        Assert.Single(merged[0].Files);
        Assert.Equal("b.flac", merged[0].Files!.First().Filename);
    }

    [Fact]
    public void Deduplicate_MeshAddsWithoutAlteringSoulseek()
    {
        var soulseek = new[] { R("u1", ("a.flac", 100)) };
        var mesh = new List<Response> { R("u2", ("b.flac", 200)) };

        var merged = SearchResponseMerger.Deduplicate(soulseek, mesh);

        Assert.Equal(2, merged.Count);
        Assert.Equal("u1", merged[0].Username);
        Assert.Single(merged[0].Files);
        Assert.Equal("a.flac", merged[0].Files!.First().Filename);
        Assert.Equal("u2", merged[1].Username);
        Assert.Single(merged[1].Files);
        Assert.Equal("b.flac", merged[1].Files!.First().Filename);
    }

    [Fact]
    public void Deduplicate_SameUsernameFilenameSize_KeepsFirst()
    {
        var soulseek = new[] { R("u1", ("same.flac", 100)) };
        var mesh = new List<Response> { R("u1", ("same.flac", 100)) };

        var merged = SearchResponseMerger.Deduplicate(soulseek, mesh);

        Assert.Single(merged);
        Assert.Equal("u1", merged[0].Username);
        Assert.Single(merged[0].Files);
    }

    [Fact]
    public void Deduplicate_DifferentUsernames_SameFilenameSize_BothKept()
    {
        var soulseek = new[] { R("u1", ("x.flac", 100)) };
        var mesh = new List<Response> { R("u2", ("x.flac", 100)) };

        var merged = SearchResponseMerger.Deduplicate(soulseek, mesh);

        Assert.Equal(2, merged.Count);
    }
}
