namespace slskd.Tests.Unit.Signals;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Signals;
using Xunit;

public class SignalBusTests
{
    private readonly Mock<ILogger<SignalBus>> loggerMock;
    private readonly Mock<IOptionsMonitor<SignalSystemOptions>> optionsMonitorMock;
    private readonly SignalSystemOptions options;

    public SignalBusTests()
    {
        loggerMock = new Mock<ILogger<SignalBus>>();
        optionsMonitorMock = new Mock<IOptionsMonitor<SignalSystemOptions>>();
        options = new SignalSystemOptions
        {
            Enabled = true,
            DeduplicationCacheSize = 1000,
            DefaultTtl = TimeSpan.FromMinutes(5)
        };
        optionsMonitorMock.Setup(x => x.CurrentValue).Returns(options);
    }

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Act
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);

        // Assert
        Assert.NotNull(signalBus);
    }

    [Fact]
    public async Task SendAsync_ShouldRouteToChannelHandler()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        var channelHandlerMock = new Mock<ISignalChannelHandler>();
        channelHandlerMock.Setup(x => x.CanSendTo(It.IsAny<string>())).Returns(true);
        channelHandlerMock.Setup(x => x.SendAsync(It.IsAny<Signal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        signalBus.RegisterChannelHandler(SignalChannel.Mesh, channelHandlerMock.Object);

        var signal = CreateTestSignal(SignalChannel.Mesh);

        // Act
        await signalBus.SendAsync(signal);

        // Assert
        channelHandlerMock.Verify(x => x.SendAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldTryNextChannel_WhenFirstChannelFails()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        
        var failingHandlerMock = new Mock<ISignalChannelHandler>();
        failingHandlerMock.Setup(x => x.CanSendTo(It.IsAny<string>())).Returns(true);
        failingHandlerMock.Setup(x => x.SendAsync(It.IsAny<Signal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Channel failure"));

        var succeedingHandlerMock = new Mock<ISignalChannelHandler>();
        succeedingHandlerMock.Setup(x => x.CanSendTo(It.IsAny<string>())).Returns(true);
        succeedingHandlerMock.Setup(x => x.SendAsync(It.IsAny<Signal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        signalBus.RegisterChannelHandler(SignalChannel.Mesh, failingHandlerMock.Object);
        signalBus.RegisterChannelHandler(SignalChannel.BtExtension, succeedingHandlerMock.Object);

        var signal = CreateTestSignal(SignalChannel.Mesh, SignalChannel.BtExtension);

        // Act
        await signalBus.SendAsync(signal);

        // Assert
        failingHandlerMock.Verify(x => x.SendAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
        succeedingHandlerMock.Verify(x => x.SendAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldSkipChannel_WhenCannotSendTo()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        
        var unavailableHandlerMock = new Mock<ISignalChannelHandler>();
        unavailableHandlerMock.Setup(x => x.CanSendTo(It.IsAny<string>())).Returns(false);

        var availableHandlerMock = new Mock<ISignalChannelHandler>();
        availableHandlerMock.Setup(x => x.CanSendTo(It.IsAny<string>())).Returns(true);
        availableHandlerMock.Setup(x => x.SendAsync(It.IsAny<Signal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        signalBus.RegisterChannelHandler(SignalChannel.Mesh, unavailableHandlerMock.Object);
        signalBus.RegisterChannelHandler(SignalChannel.BtExtension, availableHandlerMock.Object);

        var signal = CreateTestSignal(SignalChannel.Mesh, SignalChannel.BtExtension);

        // Act
        await signalBus.SendAsync(signal);

        // Assert
        unavailableHandlerMock.Verify(x => x.SendAsync(It.IsAny<Signal>(), It.IsAny<CancellationToken>()), Times.Never);
        availableHandlerMock.Verify(x => x.SendAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldNotSend_WhenSignalExpired()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        var channelHandlerMock = new Mock<ISignalChannelHandler>();
        signalBus.RegisterChannelHandler(SignalChannel.Mesh, channelHandlerMock.Object);

        var expiredSignal = new Signal(
            signalId: Guid.NewGuid().ToString("N"),
            fromPeerId: "peer-1",
            toPeerId: "peer-2",
            sentAt: DateTimeOffset.UtcNow.AddHours(-1), // Sent 1 hour ago
            type: "Test.Signal",
            body: new Dictionary<string, object>(),
            ttl: TimeSpan.FromMinutes(5), // TTL is only 5 minutes
            preferredChannels: new[] { SignalChannel.Mesh }
        );

        // Act
        await signalBus.SendAsync(expiredSignal);

        // Assert
        channelHandlerMock.Verify(x => x.SendAsync(It.IsAny<Signal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldReceiveSignals()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        var receivedSignals = new List<Signal>();
        
        await signalBus.SubscribeAsync((signal, ct) =>
        {
            receivedSignals.Add(signal);
            return Task.CompletedTask;
        });

        var channelHandlerMock = new Mock<ISignalChannelHandler>();
        channelHandlerMock.Setup(x => x.StartReceivingAsync(It.IsAny<Func<Signal, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        signalBus.RegisterChannelHandler(SignalChannel.Mesh, channelHandlerMock.Object);

        var testSignal = CreateTestSignal(SignalChannel.Mesh);

        // Act - Simulate receiving a signal directly (testing internal method)
        // In real usage, channel handlers call this
        await signalBus.OnSignalReceivedAsync(testSignal, CancellationToken.None);

        // Assert
        Assert.Single(receivedSignals);
        Assert.Equal(testSignal.SignalId, receivedSignals[0].SignalId);
    }

    [Fact]
    public async Task OnSignalReceived_ShouldDeduplicateSignals()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        var receivedSignals = new List<Signal>();
        
        await signalBus.SubscribeAsync((signal, ct) =>
        {
            receivedSignals.Add(signal);
            return Task.CompletedTask;
        });

        var channelHandlerMock = new Mock<ISignalChannelHandler>();
        channelHandlerMock.Setup(x => x.StartReceivingAsync(It.IsAny<Func<Signal, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        signalBus.RegisterChannelHandler(SignalChannel.Mesh, channelHandlerMock.Object);

        var testSignal = CreateTestSignal(SignalChannel.Mesh);

        // Act - Simulate receiving the same signal twice
        await signalBus.OnSignalReceivedAsync(testSignal, CancellationToken.None);
        await signalBus.OnSignalReceivedAsync(testSignal, CancellationToken.None); // Duplicate

        // Assert - Should only receive once due to deduplication
        Assert.Single(receivedSignals);
    }

    [Fact]
    public async Task OnSignalReceived_ShouldDropExpiredSignals()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        var receivedSignals = new List<Signal>();
        
        await signalBus.SubscribeAsync((signal, ct) =>
        {
            receivedSignals.Add(signal);
            return Task.CompletedTask;
        });

        var channelHandlerMock = new Mock<ISignalChannelHandler>();
        channelHandlerMock.Setup(x => x.StartReceivingAsync(It.IsAny<Func<Signal, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        signalBus.RegisterChannelHandler(SignalChannel.Mesh, channelHandlerMock.Object);

        var expiredSignal = new Signal(
            signalId: Guid.NewGuid().ToString("N"),
            fromPeerId: "peer-1",
            toPeerId: "peer-2",
            sentAt: DateTimeOffset.UtcNow.AddHours(-1),
            type: "Test.Signal",
            body: new Dictionary<string, object>(),
            ttl: TimeSpan.FromMinutes(5),
            preferredChannels: new[] { SignalChannel.Mesh }
        );

        // Act
        await signalBus.OnSignalReceivedAsync(expiredSignal, CancellationToken.None);

        // Assert - Expired signal should be dropped
        Assert.Empty(receivedSignals);
    }

    [Fact]
    public void RegisterChannelHandler_ShouldStartReceiving()
    {
        // Arrange
        var signalBus = new SignalBus(loggerMock.Object, optionsMonitorMock.Object);
        var channelHandlerMock = new Mock<ISignalChannelHandler>();
        channelHandlerMock.Setup(x => x.StartReceivingAsync(It.IsAny<Func<Signal, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        signalBus.RegisterChannelHandler(SignalChannel.Mesh, channelHandlerMock.Object);

        // Assert
        channelHandlerMock.Verify(x => x.StartReceivingAsync(It.IsAny<Func<Signal, CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Signal CreateTestSignal(params SignalChannel[] channels)
    {
        return new Signal(
            signalId: Guid.NewGuid().ToString("N"),
            fromPeerId: "peer-1",
            toPeerId: "peer-2",
            sentAt: DateTimeOffset.UtcNow,
            type: "Test.Signal",
            body: new Dictionary<string, object> { ["test"] = "value" },
            ttl: TimeSpan.FromMinutes(5),
            preferredChannels: channels.Length > 0 ? channels.ToList() : new List<SignalChannel> { SignalChannel.Mesh }
        );
    }
}

