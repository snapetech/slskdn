// <copyright file="PodsMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.PodCore;
using Xunit;

public class PodsMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var service = new PodsMeshService(
            Mock.Of<ILogger<PodsMeshService>>(),
            Mock.Of<IPodService>(),
            Mock.Of<IPodMessaging>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "pods",
                Method = "SensitiveCustomPodsMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown method", reply.ErrorMessage);
        Assert.DoesNotContain("SensitiveCustomPodsMethod", reply.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_PostMessage_TrimsIdsAndIncludesPodIdInMessage()
    {
        var podMessaging = new Mock<IPodMessaging>();
        podMessaging
            .Setup(service => service.SendAsync(It.IsAny<PodMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new PodsMeshService(
            Mock.Of<ILogger<PodsMeshService>>(),
            Mock.Of<IPodService>(),
            podMessaging.Object);

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "pods",
                Method = "PostMessage",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    PodId = " pod:00000000000000000000000000000001 ",
                    ChannelId = " general ",
                    Body = "hello",
                    Signature = " sig "
                })
            },
            new MeshServiceContext { RemotePeerId = " peer-1 " },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.OK, reply.StatusCode);
        podMessaging.Verify(
            svc => svc.SendAsync(
                It.Is<PodMessage>(message =>
                    message.PodId == "pod:00000000000000000000000000000001" &&
                    message.ChannelId == "general" &&
                    message.SenderPeerId == "peer-1" &&
                    message.Signature == "sig"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallAsync_GetMessages_TrimsIdsBeforeDispatch()
    {
        var podMessaging = new Mock<IPodMessaging>();
        podMessaging
            .Setup(service => service.GetMessagesAsync("pod:00000000000000000000000000000001", "general", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PodMessage>());

        var service = new PodsMeshService(
            Mock.Of<ILogger<PodsMeshService>>(),
            Mock.Of<IPodService>(),
            podMessaging.Object);

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "pods",
                Method = "GetMessages",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    PodId = " pod:00000000000000000000000000000001 ",
                    ChannelId = " general ",
                    SinceTimestamp = 10L
                })
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.OK, reply.StatusCode);
        podMessaging.Verify(service => service.GetMessagesAsync("pod:00000000000000000000000000000001", "general", 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleStreamAsync_GetMessagesRequest_SendsMessagesAndCloses()
    {
        var expectedMessages = new[]
        {
            new PodMessage
            {
                MessageId = "msg-1",
                PodId = "pod:00000000000000000000000000000001",
                ChannelId = "general",
                SenderPeerId = "peer-1",
                Body = "hello",
                TimestampUnixMs = 10
            }
        };

        var podMessaging = new Mock<IPodMessaging>();
        podMessaging
            .Setup(service => service.GetMessagesAsync("pod:00000000000000000000000000000001", "general", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessages);

        var service = new PodsMeshService(
            Mock.Of<ILogger<PodsMeshService>>(),
            Mock.Of<IPodService>(),
            podMessaging.Object);

        var stream = new TestMeshServiceStream(JsonSerializer.SerializeToUtf8Bytes(new
        {
            PodId = " pod:00000000000000000000000000000001 ",
            ChannelId = " general ",
            SinceTimestamp = 10L
        }));

        await service.HandleStreamAsync(
            stream,
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.True(stream.Closed);
        Assert.Single(stream.SentPayloads);
        var sentMessages = JsonSerializer.Deserialize<PodMessage[]>(stream.SentPayloads[0]);
        Assert.NotNull(sentMessages);
        Assert.Single(sentMessages);
        Assert.Equal("msg-1", sentMessages[0].MessageId);
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
