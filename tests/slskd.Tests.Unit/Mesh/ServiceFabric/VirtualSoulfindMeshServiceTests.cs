// <copyright file="VirtualSoulfindMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.VirtualSoulfind.ShadowIndex;
using Xunit;

public class VirtualSoulfindMeshServiceTests
{
    [Fact]
    public async Task HandleStreamAsync_QueryByMbidRequest_SendsSafeResultAndCloses()
    {
        var shadowIndexQuery = new Mock<IShadowIndexQuery>();
        shadowIndexQuery
            .Setup(query => query.QueryAsync("12345678-1234-1234-1234-123456789012", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShadowIndexQueryResult
            {
                MBID = "12345678-1234-1234-1234-123456789012",
                TotalPeerCount = 3,
                CanonicalVariants = new List<VariantHint>
                {
                    new() { Codec = "FLAC", BitrateKbps = 1000, SizeBytes = 12345, QualityScore = 0.99 }
                },
                LastUpdated = DateTimeOffset.UtcNow,
                PeerIds = new List<string> { "peer-a", "peer-b" }
            });

        var service = new VirtualSoulfindMeshService(
            Mock.Of<ILogger<VirtualSoulfindMeshService>>(),
            shadowIndexQuery.Object);

        var stream = new TestMeshServiceStream(JsonSerializer.SerializeToUtf8Bytes(new
        {
            MBID = " 12345678-1234-1234-1234-123456789012 "
        }));

        await service.HandleStreamAsync(
            stream,
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.True(stream.Closed);
        Assert.Single(stream.SentPayloads);
        var payload = JsonDocument.Parse(stream.SentPayloads[0]);
        Assert.Equal("12345678-1234-1234-1234-123456789012", payload.RootElement.GetProperty("MBID").GetString());
        Assert.Equal(3, payload.RootElement.GetProperty("PeerCount").GetInt32());
        Assert.False(payload.RootElement.TryGetProperty("PeerIds", out _));
    }

    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var service = new VirtualSoulfindMeshService(
            Mock.Of<ILogger<VirtualSoulfindMeshService>>(),
            Mock.Of<IShadowIndexQuery>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "shadow-index",
                Method = "TotallyCustomSensitiveMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown method", reply.ErrorMessage);
        Assert.DoesNotContain("Sensitive", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCallAsync_QueryBatch_InvalidMbids_ReturnsSanitizedError()
    {
        var service = new VirtualSoulfindMeshService(
            Mock.Of<ILogger<VirtualSoulfindMeshService>>(),
            Mock.Of<IShadowIndexQuery>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "shadow-index",
                Method = "QueryBatch",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                {
                    MBIDs = new[] { "valid-mbid", "../etc/passwd", "another-bad\\value" }
                })
            },
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, reply.StatusCode);
        Assert.Equal("Invalid MBID list", reply.ErrorMessage);
        Assert.DoesNotContain("passwd", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestMeshServiceStream : MeshServiceStream
    {
        private readonly byte[] _requestPayload;

        public TestMeshServiceStream(byte[] requestPayload)
        {
            _requestPayload = requestPayload;
        }

        public bool Closed { get; private set; }

        public List<byte[]> SentPayloads { get; } = new();

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            SentPayloads.Add(data.ToArray());
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<byte[]?>(_requestPayload);
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Closed = true;
            return Task.CompletedTask;
        }
    }
}
