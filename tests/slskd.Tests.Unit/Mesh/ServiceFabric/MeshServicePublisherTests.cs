// <copyright file="MeshServicePublisherTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh.Dht;
using slskd.Mesh.ServiceFabric;
using Xunit;

public class MeshServicePublisherTests
{
    [Fact]
    public async Task PublishAllServicesAsync_PublishesServiceGroupAndReverseIdIndex()
    {
        var dhtClient = new Mock<IMeshDhtClient>();
        var logger = new Mock<ILogger<MeshServicePublisher>>();
        var options = Options.Create(new MeshServiceFabricOptions
        {
            DescriptorTtlSeconds = 600,
        });

        var publisher = new MeshServicePublisher(logger.Object, dhtClient.Object, options);
        var descriptor = CreateTestDescriptor("pods", "peer-1");
        publisher.RegisterService(descriptor);

        var method = typeof(MeshServicePublisher).GetMethod(
            "PublishAllServicesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task)method!.Invoke(publisher, new object[] { CancellationToken.None })!;
        await task.ConfigureAwait(false);

        dhtClient.Verify(
            x => x.PutAsync("svc:pods", It.IsAny<object?>(), 600, It.IsAny<CancellationToken>()),
            Times.Once);
        dhtClient.Verify(
            x => x.PutAsync($"svcid:{descriptor.ServiceId}", It.Is<MeshServiceDescriptor>(d => d.ServiceId == descriptor.ServiceId), 600, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MeshServiceDescriptor CreateTestDescriptor(string serviceName, string ownerPeerId)
    {
        var now = DateTimeOffset.UtcNow;
        return new MeshServiceDescriptor
        {
            ServiceId = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId),
            ServiceName = serviceName,
            Version = "1.0.0",
            OwnerPeerId = ownerPeerId,
            Endpoint = new MeshServiceEndpoint
            {
                Protocol = "quic",
                Host = ownerPeerId,
                Port = 5000,
            },
            Metadata = new Dictionary<string, string>(),
            CreatedAt = now,
            ExpiresAt = now.AddHours(1),
            Signature = Array.Empty<byte>(),
        };
    }
}
