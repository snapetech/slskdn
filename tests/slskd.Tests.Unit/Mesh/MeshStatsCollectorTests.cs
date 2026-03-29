// <copyright file="MeshStatsCollectorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Dht;
using Xunit;

public class MeshStatsCollectorTests
{
    [Fact]
    public async Task GetStatsAsync_UsesDirectInMemoryDhtRegistration()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        var dht = new InMemoryDhtClient(NullLogger<InMemoryDhtClient>.Instance, Microsoft.Extensions.Options.Options.Create(new MeshOptions()));
        dht.AddNode(Enumerable.Repeat((byte)0x02, 20).ToArray(), "udp://203.0.113.11:5000");

        serviceProvider.Setup(sp => sp.GetService(typeof(INatDetector))).Returns(Mock.Of<INatDetector>());
        serviceProvider.Setup(sp => sp.GetService(typeof(InMemoryDhtClient))).Returns(dht);

        var collector = new MeshStatsCollector(NullLogger<MeshStatsCollector>.Instance, serviceProvider.Object);

        var stats = await collector.GetStatsAsync();

        Assert.Equal(1, stats.ActiveDhtSessions);
    }
}
