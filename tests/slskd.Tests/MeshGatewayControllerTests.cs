// <copyright file="MeshGatewayControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// PR-08: MeshGatewayController body handling — bounded read, 413 when over MaxRequestBodyBytes.
/// </summary>
public class MeshGatewayControllerTests
{
    [Fact]
    public async Task POST_body_over_MaxRequestBodyBytes_returns_413()
    {
        using var factory = new MeshGatewayTestHostFactory(maxRequestBodyBytes: 100);
        using var client = factory.CreateClient();

        var body = new byte[101];
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync("/mesh/http/pods/someMethod", content);

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    [Fact]
    public async Task POST_body_under_limit_returns_200_and_forwards_payload_to_client()
    {
        using var factory = new MeshGatewayTestHostFactory(maxRequestBodyBytes: 100);
        using var client = factory.CreateClient();

        var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync("/mesh/http/pods/someMethod", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stub = factory.Services.GetRequiredService<StubMeshServiceClient>();
        Assert.Equal(5, stub.LastPayload.Length);
        Assert.Equal(body, stub.LastPayload);
        Assert.Equal("pods", stub.LastServiceName);
        Assert.Equal("someMethod", stub.LastMethod);
    }

    /// <summary>
    /// PR-08 optional: Chunked POST with body succeeds (bounded read supports chunked when ContentLength is null).
    /// </summary>
    [Fact]
    public async Task POST_chunked_body_under_limit_returns_200()
    {
        using var factory = new MeshGatewayTestHostFactory(maxRequestBodyBytes: 100);
        using var client = factory.CreateClient();

        var body = new byte[] { 0x0a, 0x0b, 0x0c };
        var stream = new NonSeekableReadStream(body);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync("/mesh/http/pods/someMethod", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stub = factory.Services.GetRequiredService<StubMeshServiceClient>();
        Assert.Equal(3, stub.LastPayload.Length);
        Assert.Equal(body, stub.LastPayload);
    }
}

/// <summary>Stream with unknown length to force chunked Transfer-Encoding (PR-08 chunked test).</summary>
internal sealed class NonSeekableReadStream : Stream
{
    private readonly byte[] _data;
    private int _position;

    public NonSeekableReadStream(byte[] data) => _data = data;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _data.Length) return 0;
        int toRead = Math.Min(count, _data.Length - _position);
        Array.Copy(_data, _position, buffer, offset, toRead);
        _position += toRead;
        return toRead;
    }
}
