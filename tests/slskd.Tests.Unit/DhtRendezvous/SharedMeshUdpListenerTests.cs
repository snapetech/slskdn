// <copyright file="SharedMeshUdpListenerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Net.Sockets;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
using slskd.Mesh.Overlay;
using Xunit;

public class SharedMeshUdpListenerTests
{
    [Theory]
    [InlineData(new byte[] { 0xc3, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0xd0, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0x64, 0x31, 0x3a }, false)]
    [InlineData(new byte[] { 0x6c, 0x31, 0x3a }, false)]
    [InlineData(new byte[] { }, false)]
    public void IsQuicInitialPacket_OnlyMatchesQuicLongHeaderInitial(byte[] datagram, bool expected)
    {
        Assert.Equal(expected, SharedMeshUdpListener.IsQuicInitialPacket(datagram));
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0x7f, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0xc3, 0x00, 0x00 }, false)]
    [InlineData(new byte[] { 0x96, 0xa4, 0x70, 0x69, 0x6e, 0x67 }, false)]
    [InlineData(new byte[] { }, false)]
    public void IsQuicShortHeaderPacket_MatchesShortHeaderRange(byte[] datagram, bool expected)
    {
        Assert.Equal(expected, SharedMeshUdpListener.IsQuicShortHeaderPacket(datagram));
    }

    [Theory]
    [InlineData(new byte[] { 0x64, 0x31, 0x3a }, true)]
    [InlineData(new byte[] { 0x96, 0xa4, 0x70, 0x69, 0x6e, 0x67 }, false)]
    [InlineData(new byte[] { 0xc3, 0x00, 0x00 }, false)]
    [InlineData(new byte[] { }, false)]
    public void IsDhtPacket_OnlyMatchesBencodedDictionary(byte[] datagram, bool expected)
    {
        Assert.Equal(expected, SharedMeshUdpListener.IsDhtPacket(datagram));
    }

    [Fact]
    public async Task SharedListener_RoutesDhtToMessageReceivedAndQuicToBackend()
    {
        using var backend = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var listener = new SharedMeshUdpListener(
            new IPEndPoint(IPAddress.Loopback, 0),
            (IPEndPoint)backend.Client.LocalEndPoint!,
            NullLogger<SharedMeshUdpListener>.Instance);
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var dhtReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.MessageReceived += (payload, _) => dhtReceived.TrySetResult(payload.ToArray());
        listener.Start();

        var publicEndpoint = listener.LocalEndPoint;
        var dhtPacket = new byte[] { 0x64, 0x31, 0x3a, 0x79 };
        await client.SendAsync(dhtPacket, publicEndpoint);

        Assert.Equal(dhtPacket, await dhtReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        var quicPacket = new byte[] { 0xc3, 0x00, 0x00, 0x01 };
        await client.SendAsync(quicPacket, publicEndpoint);
        var backendResult = await backend.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(quicPacket, backendResult.Buffer);

        var quicResponse = new byte[] { 0x40, 0x02 };
        await backend.SendAsync(quicResponse, backendResult.RemoteEndPoint);
        var clientResult = await client.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(quicResponse, clientResult.Buffer);
        Assert.Equal(publicEndpoint, clientResult.RemoteEndPoint);

        var quicShortHeaderPacket = new byte[] { 0x41, 0x00, 0x02 };
        await client.SendAsync(quicShortHeaderPacket, publicEndpoint);
        var shortHeaderBackendResult = await backend.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(quicShortHeaderPacket, shortHeaderBackendResult.Buffer);
    }

    [Fact]
    public async Task SharedListener_RoutesUdpOverlayEnvelopeToDispatcher()
    {
        var dispatcher = new CapturingDispatcher();
        using var listener = new SharedMeshUdpListener(
            new IPEndPoint(IPAddress.Loopback, 0),
            quicBackendEndPoint: null,
            NullLogger<SharedMeshUdpListener>.Instance,
            dispatcher);
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        listener.Start();

        var envelope = new ControlEnvelope
        {
            Type = OverlayControlTypes.Ping,
            Payload = Array.Empty<byte>(),
        };
        var payload = MessagePackSerializer.Serialize(envelope);
        await client.SendAsync(payload, listener.LocalEndPoint);

        var received = await dispatcher.Received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(OverlayControlTypes.Ping, received.Type);
    }

    private sealed class CapturingDispatcher : IControlDispatcher
    {
        public TaskCompletionSource<ControlEnvelope> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default)
        {
            Received.TrySetResult(envelope);
            return Task.FromResult(true);
        }

        public Task<bool> HandleAsync(ControlEnvelope envelope, slskd.Mesh.Dht.MeshPeerDescriptor peerDescriptor, string peerId, CancellationToken ct = default)
        {
            return HandleAsync(envelope, ct);
        }
    }
}
