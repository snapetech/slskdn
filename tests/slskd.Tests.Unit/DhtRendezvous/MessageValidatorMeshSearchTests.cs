// <copyright file="MessageValidatorMeshSearchTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Linq;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Security;
using Xunit;

/// <summary>
/// Unit tests for MessageValidator.ValidateMeshSearchReq and ValidateMeshSearchResp.
/// </summary>
public class MessageValidatorMeshSearchTests
{
    private static MeshSearchRequestMessage ValidRequest() => new()
    {
        RequestId = Guid.NewGuid().ToString("N"),
        SearchText = "test",
        MaxResults = 50,
    };

    private static MeshSearchResponseMessage ValidResponse() => new()
    {
        RequestId = Guid.NewGuid().ToString("N"),
        Files = new List<MeshSearchFileDto> { new() { Filename = "a.flac", Size = 100 } },
    };

    [Fact]
    public void ValidateMeshSearchReq_WhenNull_ReturnsInvalid()
    {
        var r = MessageValidator.ValidateMeshSearchReq(null);
        Assert.False(r.IsValid);
        Assert.Contains("null", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenRequestIdEmpty_ReturnsInvalid()
    {
        var m = ValidRequest();
        m.RequestId = "";
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.False(r.IsValid);
        Assert.Contains("request_id", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenRequestIdNotGuid_ReturnsInvalid()
    {
        var m = ValidRequest();
        m.RequestId = "not-a-guid";
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.False(r.IsValid);
        Assert.Contains("GUID", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenSearchTextEmpty_ReturnsInvalid()
    {
        var m = ValidRequest();
        m.SearchText = "   ";
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.False(r.IsValid);
        Assert.Contains("search_text", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenSearchTextTooLong_ReturnsInvalid()
    {
        var m = ValidRequest();
        m.SearchText = new string('x', MessageValidator.MaxMeshSearchTextLength + 1);
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.False(r.IsValid);
        Assert.Contains("too long", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenMaxResultsZero_ReturnsInvalid()
    {
        var m = ValidRequest();
        m.MaxResults = 0;
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.False(r.IsValid);
        Assert.Contains("max_results", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenMaxResultsOverLimit_ReturnsInvalid()
    {
        var m = ValidRequest();
        m.MaxResults = MessageValidator.MaxMeshSearchMaxResults + 1;
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.False(r.IsValid);
        Assert.Contains("max_results", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchReq_WhenValid_ReturnsSuccess()
    {
        var m = ValidRequest();
        var r = MessageValidator.ValidateMeshSearchReq(m);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void ValidateMeshSearchResp_WhenNull_ReturnsInvalid()
    {
        var r = MessageValidator.ValidateMeshSearchResp(null);
        Assert.False(r.IsValid);
        Assert.Contains("null", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchResp_WhenRequestIdNotGuid_ReturnsInvalid()
    {
        var m = ValidResponse();
        m.RequestId = "x";
        var r = MessageValidator.ValidateMeshSearchResp(m);
        Assert.False(r.IsValid);
        Assert.Contains("request_id", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchResp_WhenFilesCountOverLimit_ReturnsInvalid()
    {
        var m = ValidResponse();
        m.Files = Enumerable.Range(0, MessageValidator.MaxMeshSearchResponseFiles + 1)
            .Select(i => new MeshSearchFileDto { Filename = $"f{i}.flac", Size = 1 })
            .ToList();
        var r = MessageValidator.ValidateMeshSearchResp(m);
        Assert.False(r.IsValid);
        Assert.Contains("files", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchResp_WhenFileHasEmptyFilename_ReturnsInvalid()
    {
        var m = ValidResponse();
        m.Files = new List<MeshSearchFileDto> { new() { Filename = "", Size = 100 } };
        var r = MessageValidator.ValidateMeshSearchResp(m);
        Assert.False(r.IsValid);
        Assert.Contains("filename", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchResp_WhenFileSizeNegative_ReturnsInvalid()
    {
        var m = ValidResponse();
        m.Files = new List<MeshSearchFileDto> { new() { Filename = "a.flac", Size = -1 } };
        var r = MessageValidator.ValidateMeshSearchResp(m);
        Assert.False(r.IsValid);
        Assert.Contains("size", r.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMeshSearchResp_WhenValid_ReturnsSuccess()
    {
        var m = ValidResponse();
        var r = MessageValidator.ValidateMeshSearchResp(m);
        Assert.True(r.IsValid);
    }
}
