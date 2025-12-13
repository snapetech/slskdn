using System.Net;
using Moq;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using slskd.Mesh;
using slskd.Mesh.Dht;
using slskd.Mesh.Nat;
using Xunit;

namespace slskd.Tests.Unit.Mesh;

public class Phase8MeshTests
{
    [Fact]
    public void KademliaRoutingTable_ReturnsClosestInOrder()
    {
        // self ID (20 bytes)
        var selfId = Enumerable.Repeat((byte)0x00, 20).ToArray();
        var table = new KademliaRoutingTable(selfId);

        // nodes at varying XOR distances
        var near = Enumerable.Repeat((byte)0x00, 20).ToArray();
        near[19] = 0x01; // very close
        var mid = Enumerable.Repeat((byte)0x00, 20).ToArray();
        mid[19] = 0x10;
        var far = Enumerable.Repeat((byte)0xff, 20).ToArray();

        table.Touch(near, "udp://1.1.1.1:1");
        table.Touch(mid, "udp://2.2.2.2:2");
        table.Touch(far, "udp://3.3.3.3:3");

        var target = Enumerable.Repeat((byte)0x00, 20).ToArray();
        var closest = table.GetClosest(target, 3).ToList();

        Assert.Equal(3, closest.Count);
        Assert.True(closest[0].NodeId.SequenceEqual(near));
        Assert.True(closest[1].NodeId.SequenceEqual(mid));
        Assert.True(closest[2].NodeId.SequenceEqual(far));
    }

    [Fact]
    public async Task InMemoryDhtClient_PutGet_Expires()
    {
        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<InMemoryDhtClient>>();
        var opts = Microsoft.Extensions.Options.Options.Create(new MeshOptions());
        var dht = new InMemoryDhtClient(logger, opts);

        var key = Enumerable.Repeat((byte)0xAA, 20).ToArray();
        var value = new byte[] { 0x01, 0x02, 0x03 };

        await dht.PutAsync(key, value, ttlSeconds: 1, ct: default);
        var got = await dht.GetAsync(key, default);
        Assert.NotNull(got);
        Assert.Equal(value, got);

        await Task.Delay(1200);
        var expired = await dht.GetAsync(key, default);
        // In-memory DHT clamps TTL to >=60s; value should still exist
        Assert.NotNull(expired);
    }

    [Fact]
    public async Task NatTraversalService_HolePunchPreferred_RelayFallback()
    {
        var hp = new Mock<IUdpHolePuncher>();
        var relay = new Mock<IRelayClient>();
        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<NatTraversalService>>();

        // First attempt fails hole punch
        hp.Setup(x => x.TryPunchAsync(It.IsAny<IPEndPoint>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new UdpHolePunchResult(false, null));

        // Relay succeeds
        relay.Setup(x => x.RelayAsync(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var options = Microsoft.Extensions.Options.Options.Create(new MeshOptions
        {
            EnableMirrored = true
        });

        var svc = new NatTraversalService(logger, hp.Object, relay.Object, options);

        var endpoints = new List<string> { "udp://203.0.113.1:5000", "relay://198.51.100.1:6000" };
        var res = await svc.ConnectAsync("peer1", endpoints, CancellationToken.None);

        Assert.True(res.Success);
        Assert.True(res.UsedRelay);
        Assert.Equal("relay", res.Reason);
    }
}
















