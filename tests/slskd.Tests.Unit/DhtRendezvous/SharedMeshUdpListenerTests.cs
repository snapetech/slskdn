// <copyright file="SharedMeshUdpListenerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
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
    }
}
