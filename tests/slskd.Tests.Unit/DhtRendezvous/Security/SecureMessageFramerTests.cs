// <copyright file="SecureMessageFramerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Security;

using System.IO;
using System.Text;
using System.Text.Json;
using slskd.DhtRendezvous.Security;
using Xunit;

public class SecureMessageFramerTests
{
    [Fact]
    public async Task ReadMessageAsync_WhenJsonIsMalformed_ThrowsSanitizedProtocolViolation()
    {
        var payload = Encoding.UTF8.GetBytes("{bad json");
        await using var stream = new MemoryStream();
        await stream.WriteAsync(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(payload.Length)));
        await stream.WriteAsync(payload);
        stream.Position = 0;

        var framer = new SecureMessageFramer(stream);

        var ex = await Assert.ThrowsAsync<ProtocolViolationException>(() => framer.ReadMessageAsync<TestMessage>());

        Assert.Equal("Invalid JSON", ex.Message);
        Assert.DoesNotContain("BytePositionInLine", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeserializeMessage_WhenJsonIsMalformed_ThrowsSanitizedProtocolViolation()
    {
        var framer = new SecureMessageFramer(new MemoryStream());
        var payload = Encoding.UTF8.GetBytes("{bad json");

        var ex = Assert.Throws<ProtocolViolationException>(() => framer.DeserializeMessage<TestMessage>(payload));

        Assert.Equal("Invalid JSON", ex.Message);
        Assert.DoesNotContain("LineNumber", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TestMessage(string Type);
}
