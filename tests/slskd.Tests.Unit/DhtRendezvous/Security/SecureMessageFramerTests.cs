// <copyright file="SecureMessageFramerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.DhtRendezvous.Security;

using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Security;
using Xunit;

public class SecureMessageFramerTests
{
    [Fact]
    public async Task ReadRawMessageAsync_WhenMessageIsFramed_ReturnsPayload()
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new PingMessage { Timestamp = 123 });
        await using var stream = new MemoryStream();
        await WriteFramedPayloadAsync(stream, payload);
        stream.Position = 0;

        var framer = new SecureMessageFramer(stream);

        var raw = await framer.ReadRawMessageAsync();

        Assert.Equal(payload, raw);
        Assert.Equal(OverlayMessageType.Ping, SecureMessageFramer.ExtractMessageType(raw));
    }

    [Fact]
    public async Task ReadRawMessageAsync_WhenPeerSendsUnframedJson_ReturnsPayload()
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new PingMessage { Timestamp = 456 });
        await using var stream = new MemoryStream(payload);

        var framer = new SecureMessageFramer(stream);

        var raw = await framer.ReadRawMessageAsync();
        var ping = SecureMessageFramer.DeserializeMessage<PingMessage>(raw);

        Assert.Equal(payload, raw);
        Assert.Equal(456, ping.Timestamp);
    }

    [Fact]
    public async Task ReadMessageAsync_WhenPeerSendsUnframedJson_DeserializesPayload()
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new PingMessage { Timestamp = 789 });
        await using var stream = new MemoryStream(payload);

        var framer = new SecureMessageFramer(stream);

        var ping = await framer.ReadMessageAsync<PingMessage>();

        Assert.Equal(789, ping.Timestamp);
    }

    [Fact]
    public async Task ReadRawMessageAsync_WhenLengthPrefixIsInvalidAndNotJson_ThrowsProtocolViolation()
    {
        var invalidLength = new byte[SecureMessageFramer.HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(invalidLength, SecureMessageFramer.MaxMessageSize + 1);
        await using var stream = new MemoryStream(invalidLength);

        var framer = new SecureMessageFramer(stream);

        var ex = await Assert.ThrowsAsync<ProtocolViolationException>(() => framer.ReadRawMessageAsync());

        Assert.Equal($"Message too large: {SecureMessageFramer.MaxMessageSize + 1} > {SecureMessageFramer.MaxMessageSize}", ex.Message);
    }

    [Fact]
    public async Task ReadMessageAsync_WhenJsonIsMalformed_ThrowsSanitizedProtocolViolation()
    {
        var payload = Encoding.UTF8.GetBytes("{bad json");
        await using var stream = new MemoryStream();
        await WriteFramedPayloadAsync(stream, payload);
        stream.Position = 0;

        var framer = new SecureMessageFramer(stream);

        var ex = await Assert.ThrowsAsync<ProtocolViolationException>(() => framer.ReadMessageAsync<TestMessage>());

        Assert.Equal("Invalid JSON", ex.Message);
        Assert.DoesNotContain("BytePositionInLine", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeserializeMessage_WhenJsonIsMalformed_ThrowsSanitizedProtocolViolation()
    {
        var payload = Encoding.UTF8.GetBytes("{bad json");

        var ex = Assert.Throws<ProtocolViolationException>(() => SecureMessageFramer.DeserializeMessage<TestMessage>(payload));

        Assert.Equal("Invalid JSON", ex.Message);
        Assert.DoesNotContain("LineNumber", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteFramedPayloadAsync(Stream stream, byte[] payload)
    {
        var header = new byte[SecureMessageFramer.HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
    }

    private sealed record TestMessage(string Type);
}
