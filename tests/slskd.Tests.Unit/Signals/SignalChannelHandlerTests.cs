// <copyright file="SignalChannelHandlerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Signals;

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using slskd.Signals;
using Xunit;

public class SignalChannelHandlerTests
{
    [Fact]
    public async Task MeshSignalChannelHandler_StartReceivingTwice_DoesNotDuplicateDelivery()
    {
        var sender = new TestMeshMessageSender();
        var handler = new MeshSignalChannelHandler(
            NullLogger<MeshSignalChannelHandler>.Instance,
            CreateOptions(),
            sender,
            "local-peer");

        var deliveries = 0;
        Task OnSignalReceived(Signal _, CancellationToken __)
        {
            deliveries++;
            return Task.CompletedTask;
        }

        await handler.StartReceivingAsync(OnSignalReceived, CancellationToken.None);
        await handler.StartReceivingAsync(OnSignalReceived, CancellationToken.None);

        await sender.RaiseAsync(new SlskdnSignalMessage
        {
            SignalId = "sig-1",
            FromPeerId = "remote-peer",
            ToPeerId = "local-peer",
            Type = "test",
            Body = "{}",
            SentAt = DateTimeOffset.UtcNow,
            Ttl = TimeSpan.FromMinutes(1),
        });

        Assert.Equal(1, deliveries);
    }

    [Fact]
    public async Task BtExtensionSignalChannelHandler_StartReceivingTwice_DoesNotDuplicateDelivery()
    {
        var sender = new TestBtExtensionSender();
        var handler = new BtExtensionSignalChannelHandler(
            NullLogger<BtExtensionSignalChannelHandler>.Instance,
            CreateOptions(),
            sender,
            "local-peer");

        var deliveries = 0;
        Task OnSignalReceived(Signal _, CancellationToken __)
        {
            deliveries++;
            return Task.CompletedTask;
        }

        await handler.StartReceivingAsync(OnSignalReceived, CancellationToken.None);
        await handler.StartReceivingAsync(OnSignalReceived, CancellationToken.None);

        var signal = new Signal(
            signalId: "sig-1",
            fromPeerId: "remote-peer",
            toPeerId: "local-peer",
            sentAt: DateTimeOffset.UtcNow,
            type: "test",
            body: new Dictionary<string, object>(),
            ttl: TimeSpan.FromMinutes(1),
            preferredChannels: new[] { SignalChannel.BtExtension });

        await sender.RaiseAsync(
            new SlskdnExtensionMessage
            {
                Kind = SlskdnSignalKind.SignalEnvelope,
                Payload = JsonSerializer.Serialize(signal),
            },
            "remote-peer");

        Assert.Equal(1, deliveries);
    }

    private static IOptionsMonitor<SignalSystemOptions> CreateOptions()
    {
        var monitor = new TestOptionsMonitor<SignalSystemOptions>(new SignalSystemOptions
        {
            MeshChannel = new SignalChannelOptions
            {
                Enabled = true,
            },
            BtExtensionChannel = new SignalChannelOptions
            {
                Enabled = true,
                RequireActiveSession = false,
            },
        });

        return monitor;
    }

    private sealed class TestMeshMessageSender : IMeshMessageSender
    {
        public event Func<SlskdnSignalMessage, CancellationToken, Task>? OnSlskdnSignalReceived;

        public Task SendToPeerAsync(string peerId, object message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RaiseAsync(SlskdnSignalMessage message, CancellationToken cancellationToken = default)
        {
            return OnSlskdnSignalReceived?.Invoke(message, cancellationToken) ?? Task.CompletedTask;
        }
    }

    private sealed class TestBtExtensionSender : IBtExtensionSender
    {
        public event Func<SlskdnExtensionMessage, string, CancellationToken, Task>? OnSlskdnExtensionMessageReceived;

        public bool HasActiveSession(string peerId) => true;

        public Task SendExtensionMessageAsync(string peerId, SlskdnExtensionMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RaiseAsync(SlskdnExtensionMessage message, string fromPeerId, CancellationToken cancellationToken = default)
        {
            return OnSlskdnExtensionMessageReceived?.Invoke(message, fromPeerId, cancellationToken) ?? Task.CompletedTask;
        }
    }
}
